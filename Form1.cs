using FastColoredTextBoxNS;
using FastColoredTextBoxNS.Text;
using FastColoredTextBoxNS.Types;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Linq;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        private readonly SqliteStore settingsConn = new("settings.sqlite");
        private SqliteStore sessionsConn;
        private static ServerCertificate serverCertificate = new();
        private static Setting _settings = new();

        /// <summary>Read-only access for the sync stores - the profile the sync runs against.</summary>
        internal static Setting CurrentSettings => _settings;

        //The grid is populated row by row rather than bound to a DataTable. WinForms data
        //binding throws NotSupportedException once the app is trimmed, which Native AOT
        //implies, so this list is the source of truth and the grid is a view over it.
        private readonly List<SessionRow> allRows = new();

        //What CellValueNeeded serves. Built by RefreshView out of allRows, the filters, and
        //the view toggles; the grid holds no rows of its own.
        private List<SessionRow> viewRows = new();

        //The request payload per session id, loaded once per duplicates-view session and held
        //until any session changes. Request bodies and headers are too heavy to keep in the
        //row model permanently just in case the view is opened.
        private Dictionary<int, (string Query, string Headers, string Body)> duplicatesKeys;

        private string textFilter = string.Empty;
        private string groupFilter = string.Empty;

        /// <summary>
        /// One grid row's worth of a session. Mirrors the columns created in CreateGridColumns.
        /// </summary>
        private sealed class SessionRow
        {
            public int Id { get; init; }
            public object Timestamp { get; init; }

            //Null while a request is still running - there is no status code yet, and the
            //column has to stay empty rather than read as an HTTP 0.
            public int? StatusCode { get; init; }
            public string MethodAndHost { get; init; }

            //The host on its own - MethodAndHost is for display, and grouping needs the
            //host without the method when the path is empty.
            public string Host { get; init; }
            public string Path { get; init; }

            public string Note { get; set; }
            public string Group { get; set; }

            /// <summary>
            /// A placeholder for a request that has not come back yet, not a stored session.
            /// </summary>
            public bool IsPending { get; init; }

            /// <summary>Not a session at all - a label row naming a group of sessions.</summary>
            public bool IsGroupHeader { get; init; }

            /// <summary>Header text's tail: the computed key; the count is tacked on at render.</summary>
            public int GroupCount { get; init; }

            //Grouping by URL is purely a view concern, and SessionRow is the grid's view model:
            //the prefix the group's header already shows, so the row displays only the rest.
            public string PathPrefixStripped { get; set; }

            //Recomputed by RefreshView: the path column as rendered, prefix stripped if the
            //row's group header already shows it. Precomputed because CellValueNeeded serves
            //it over and over while a cell is in view.
            public string DisplayPath { get; set; }

            //Header rows render the group key and count where sessions show method + host.
            public string DisplayMethodAndHost { get; set; }

            //Timestamp is object because Stamp keeps the raw string when parsing fails; sorting
            //needs a real date either way, so unparsed values collapse to the minimum.
            public DateTime SortStamp => Timestamp is DateTime stamp ? stamp : DateTime.MinValue;

            public static SessionRow From(Session s)
            {
                return new SessionRow
                {
                    Id = s.Id,
                    Timestamp = Stamp(s.DateTime),
                    StatusCode = s.ResponseStatusCode,
                    MethodAndHost = s.Method + " " + s.UriHost,
                    Host = s.UriHost,
                    Path = s.UriAbsolutePath,
                    Note = s.Note,
                    Group = s.Group
                };
            }

            /// <summary>
            /// Builds a row from the <see cref="SessionRowProjection"/> columns. The grid needs
            /// eight of a session's thirty-odd fields, and reading whole sessions to fill it
            /// meant loading every stored response body as well.
            /// </summary>
            public static SessionRow FromProjection(object[] values)
            {
                return new SessionRow
                {
                    Id = SqliteStore.AsInt(values[0]),
                    Timestamp = Stamp(SqliteStore.AsString(values[1])),
                    StatusCode = SqliteStore.AsInt(values[2]),
                    MethodAndHost = SqliteStore.AsString(values[3]) + " " + SqliteStore.AsString(values[4]),
                    Host = SqliteStore.AsString(values[4]),
                    Path = SqliteStore.AsString(values[5]),
                    Note = SqliteStore.AsString(values[6]),
                    Group = SqliteStore.AsString(values[7])
                };
            }

            //Stored as an ISO-ish string; parse so the column sorts and formats as a date.
            private static object Stamp(string value)
                => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)
                    ? parsed
                    : value;
        }

        //A path segment that names an API version - /v0/, /v12/, case-insensitive. Two requests
        //whose paths differ only below such a segment are versions of the same group.
        private static readonly System.Text.RegularExpressions.Regex VersionSegment =
            new("^v\\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        /// <summary>
        /// What a session groups under when "Group by URL" is on: the path, cut just below the
        /// first version segment so /api/v1/users/1 and /api/v1/users/2 share a key. Paths
        /// without a version segment group by the full path, and an empty path by the host -
        /// there is nothing more specific to group them by. The query never takes part.
        /// </summary>
        private static string GroupKey(string host, string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return host ?? string.Empty;

            //Find where the version segment ends; everything from there on is per-request.
            int searchFrom = 0;

            foreach (string segment in path.Split('/'))
            {
                if (VersionSegment.IsMatch(segment))
                {
                    int end = searchFrom + segment.Length;
                    return path.Substring(0, end);
                }

                searchFrom += segment.Length + 1;
            }

            return path;
        }

        /// <summary>
        /// The same rows <see cref="FilteredRows"/> returned, optionally regrouped by URL path.
        /// A group header is a SessionRow like the others except nothing must ever treat it as
        /// a session - its id sits below the pending rows' range so neither row-restore nor
        /// delete can pick it up.
        /// </summary>
        private List<SessionRow> OrderedRows(IEnumerable<SessionRow> rows)
        {
            var materialized = rows as List<SessionRow> ?? rows.ToList();

            //Duplicates is a flat view over what the caller already filtered: grouping on top
            //of it would bury the members of one run in their path groups.
            if (checkBox_duplicates.Checked) materialized = OnlyDuplicates(materialized, duplicatesKeys);

            //Flat view: order by timestamp. Id order only approximates it - synced-in or
            //imported sessions were created elsewhere and their ids know nothing about when.
            if (!checkBox_group_url.Checked || checkBox_duplicates.Checked)
                return materialized.OrderBy(r => r.SortStamp).ToList();

            var groups = materialized
                .GroupBy(r => GroupKey(r.Host, r.Path))
                .Select(g => (
                    Key: g.Key,
                    Rows: g.OrderBy(r => r.SortStamp).ToList(),
                    Newest: g.Max(r => r.SortStamp)))
                .OrderBy(g => g.Newest)
                .ToList();

            //A row groups under the full path unless a version segment truncated the key -
            //only then is there anything to strip from the displayed path.
            foreach (var group in groups)
            {
                if (group.Rows.Count == 0) continue;

                string prefix = group.Key;
                SessionRow first = group.Rows[0];

                //The key is the whole path or the bare host - nothing to strip. Clearing
                //matters across repaints: a row can change groups between RefreshGrid calls.
                if (group.Key == first.Path || string.IsNullOrEmpty(first.Path) || first.Path == "/")
                {
                    foreach (SessionRow row in group.Rows) row.PathPrefixStripped = null;
                    continue;
                }

                foreach (SessionRow row in group.Rows) row.PathPrefixStripped = prefix;
            }

            var ordered = new List<SessionRow>(materialized.Count + groups.Count);

            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];

                ordered.Add(new SessionRow
                {
                    Id = int.MinValue + i,
                    Timestamp = string.Empty,
                    IsGroupHeader = true,
                    Host = group.Key,
                    GroupCount = group.Rows.Count
                });

                ordered.AddRange(group.Rows);
            }

            return ordered;
        }

        /// <summary>
        /// Set while the grid is being updated in code. CellValueChanged cannot tell a user
        /// edit from a programmatic one, and persisting the latter overwrites the note.
        /// </summary>
        private bool suppressNoteUpdates;

        /// <summary>
        /// Set while the grid is being rebuilt, when the current row moves for reasons that have
        /// nothing to do with the user.
        /// </summary>
        private bool suppressRowEnterDisplay;

        /// <summary>
        /// The session the request and response panes are showing. Lets RowEnter tell a real
        /// change of row from the current row being re-established after a repaint - reloading
        /// the panes would throw away the user's scroll position in them.
        /// </summary>
        private int displayedSessionId;

        //Extra URL suggestions are read from this file next to the executable. It is gitignored -
        //internal hostnames do not belong in source control.
        private const string UrlSuggestionsFile = "urls.txt";


        public Form1()
        {
            InitializeComponent();


            this.Text = "API Tester  v" + this.ProductVersion;

            typeof(DataGridView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, dataGridView1, new object[] { true }, CultureInfo.InvariantCulture);

            CreateGridColumns();

            //No DisplayMember: the combo holds ProfileItem, whose ToString is the profile name.
            //DisplayMember goes through the binding stack, which is unavailable when trimmed.

            LoadCertificates();
            SetupAutocomplete();
            SetupSync();
            SetupFilesTab();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            try
            {
                await LoadSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void SetupAutocomplete()
        {
            //add autocomplete to request headers
            var request_headers_AutocompleteMenu = new AutocompleteMenu(textBox_request_headers);
            request_headers_AutocompleteMenu.Items.MaximumSize = new Size(300, 200);
            request_headers_AutocompleteMenu.Items.Width = 300;
            request_headers_AutocompleteMenu.MinFragmentLength = 1;

            var items_headers = new List<AutocompleteItem>
            {
                new AutocompleteItem("Accept: */*\nConnection: keep-alive\nContent-Type: application/json\nUser-Agent: ApiTester\nOcp-Apim-Subscription-Key: ", 0, "_Default headers"),
                new AutocompleteItem("Content-Type: application/json"),
                new AutocompleteItem("Content-Type: text/xml"),
                new AutocompleteItem("Content-Type: application/x-www-form-urlencoded"),
                new AutocompleteItem("Ocp-Apim-Subscription-Key: "),
                new AutocompleteItem("Authorization: Bearer "),
                new AutocompleteItem("Accept: */*"),
                new AutocompleteItem("Accept-Encoding: gzip, deflate, br"),
                new AutocompleteItem("Connection: keep-alive"),
                new AutocompleteItem("User-Agent: ApiTester")
            };

            request_headers_AutocompleteMenu.Items.SetAutocompleteItems(items_headers);

            //add autocomplete to request url
            var request_url_AutocompleteMenu = new AutocompleteMenu(textBox_request_url);
            request_url_AutocompleteMenu.Items.MaximumSize = new Size(300, 200);
            request_url_AutocompleteMenu.Items.Width = 300;
            request_url_AutocompleteMenu.MinFragmentLength = 1;

            var items_url = new List<AutocompleteItem> { new AutocompleteItem("https://") };

            //Environment specific hostnames live in a local, untracked file - one URL per line.
            try
            {
                string suggestionsPath = Path.Combine(AppContext.BaseDirectory, UrlSuggestionsFile);

                if (File.Exists(suggestionsPath))
                {
                    foreach (string line in File.ReadAllLines(suggestionsPath))
                    {
                        if (!string.IsNullOrWhiteSpace(line)) items_url.Add(new AutocompleteItem(line.Trim()));
                    }
                }
            }
            catch (IOException)
            {
                //Suggestions are optional - a missing or locked file is not worth interrupting startup.
            }

            request_url_AutocompleteMenu.Items.SetAutocompleteItems(items_url);
        }

        private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (splitterDataSaved) return;

            CleanFilesStaging();

            //The window closes before a fire-and-forget save completes, so cancel this close,
            //persist, then close for real.
            e.Cancel = true;

            try
            {
                await SaveSplitterData();

                //Last chance to publish this session's changes; whatever is left stays marked
                //dirty and goes out on the next start.
                await FlushSyncOnClose();
            }
            catch (Exception)
            {
                //Losing the window layout must not block shutdown.
            }

            splitterDataSaved = true;
            Close();
        }

        private bool splitterDataSaved;


        //A session has more than thirty columns, one of them the compressed response body. The
        //grid needs these eight, and asking for only them keeps a reload off the megabytes.
        //Rows deleted locally are kept until the container has taken the delete - they are not
        //sessions any more and must not show up.
        private const string SessionRowProjection =
            "select Id, \"DateTime\", ResponseStatusCode, Method, UriHost, UriAbsolutePath, Note, \"Group\""
            + " from Session where Deleted = 0 order by Id";

        //Only read when the duplicates view is on - RequestHeaders and RequestBody are the
        //heaviest text a session stores, and the grid's model must not hold them for every
        //session just in case the view is opened.
        private const string DuplicatesProjection =
            "select Id, UriQuery, RequestHeaders, RequestBody from Session where Deleted = 0";

        public async Task LoadSessions()
        {
            CursorWait(true);

            try
            {
                try
                {
                    if (sessionsConn is not null)
                    {
                        //Also adds the sync columns to a database written before them.
                        await EnsureSyncSchema();
                        await ReloadSessionRows();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

                await LoadGroups();

                SelectLastGridRow();

                //display content of the newly saved session
                if (dataGridView1.RowCount > 0) await DisplaySession(dataGridView1.RowCount - 1);

                //Catches up with the other instances, and publishes anything this one still owes.
                RequestSync();
            }
            finally
            {
                CursorWait(false);
            }
        }

        /// <summary>
        /// Rebuilds the row model from the database and repaints the grid, keeping the user's
        /// place. Used both on load and after a sync brings something in.
        /// </summary>
        private async Task ReloadSessionRows()
        {
            var rows = await sessionsConn.RawRowsAsync(SessionRowProjection);

            //Placeholders for requests in flight are not in the database - they would be lost.
            var pending = allRows.Where(r => r.IsPending).ToList();

            allRows.Clear();
            foreach (object[] values in rows) allRows.Add(SessionRow.FromProjection(values));
            allRows.AddRange(pending);

            //The ids the duplicates cache knows about changed.
            duplicatesKeys = null;

            await RefreshGrid();
        }

        /// <summary>
        /// Declares the grid's columns once. Nothing is auto generated from a data source,
        /// so the column set and order are fixed here rather than by a DataTable's schema.
        /// </summary>
        private void CreateGridColumns()
        {
            dataGridView1.AutoGenerateColumns = false;

            //The grid asks for each visible cell instead of holding a materialized copy of
            //every row - the only way a repaint stays cheap once the session count grows.
            dataGridView1.VirtualMode = true;

            //Wrapping would make every row measure its height against possibly long notes.
            dataGridView1.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            dataGridView1.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dataGridView1.Columns.Clear();

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Id", HeaderText = "Id", ValueType = typeof(int), Visible = false, ReadOnly = true
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                //Fixed width. Any AllCells-ish mode asks CellValueNeeded for values not yet on
                //screen - and DisplayedCells resizes as scrolling brings wider stamps into
                //view, so every scroll reshuffles the columns under the user's eyes. The saved
                //width lands on load; this is just the before-profile default.
                Name = "DateTime", HeaderText = "DateTime", ValueType = typeof(DateTime),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 130, ReadOnly = true
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ResponseStatusCode", HeaderText = "ResponseStatusCode", ValueType = typeof(int),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 35, MinimumWidth = 35, ReadOnly = true
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "UriHost", HeaderText = "UriHost", ValueType = typeof(string),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet, ReadOnly = true
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                //Same fixed-width reason as DateTime: URLs vary wildly, and an auto-sizing
                //column between the fixed ones and the fill column makes scrolling renegotiate
                //every column.
                Name = "UriAbsolutePath", HeaderText = "UriAbsolutePath", ValueType = typeof(string),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None, Width = 260, ReadOnly = true
            });

            //The only editable column.
            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Note", HeaderText = "Note", ValueType = typeof(string),
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            dataGridView1.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Group", HeaderText = "Group", ValueType = typeof(string), Visible = false
            });
        }

        //Reads what OnlyDuplicates compares. Runs once per toggle-on and after any session
        //change while the view is on; null means "not loaded", and the checkers that reset it
        //mean "loaded but stale".
        private async Task LoadDuplicatesKeys()
        {
            var keys = new Dictionary<int, (string, string, string)>();

            foreach (object[] row in await sessionsConn.RawRowsAsync(DuplicatesProjection))
            {
                keys[SqliteStore.AsInt(row[0])] =
                    (SqliteStore.AsString(row[1]), SqliteStore.AsString(row[2]), SqliteStore.AsString(row[3]));
            }

            duplicatesKeys = keys;
        }

        //The view a rebuild of the grid starts from. Stored rather than recomputed per cell:
        //CellValueNeeded asks one cell at a time and has no business re-filtering per question.
        private void RefreshView()
        {
            List<SessionRow> rows = OrderedRows(FilteredRows());

            foreach (SessionRow r in rows)
            {
                if (r.IsGroupHeader)
                {
                    //A header's label is the group key with its member count, and it belongs
                    //in the path column - that is the column it is a label for.
                    r.DisplayPath = r.Host + " (" + r.GroupCount.ToString(CultureInfo.CurrentCulture) + ")";
                    continue;
                }

                //The header already shows the key, so the row shows only what follows it.
                r.DisplayPath = r.PathPrefixStripped is not null
                    && r.Path is not null
                    && r.Path.StartsWith(r.PathPrefixStripped, StringComparison.Ordinal)
                        ? r.Path.Substring(r.PathPrefixStripped.Length)
                        : r.Path;

                r.DisplayMethodAndHost = r.MethodAndHost;
            }

            viewRows = rows;
            dataGridView1.RowCount = rows.Count;
        }

        private SessionRow ViewRow(int index)
            => index >= 0 && index < viewRows.Count ? viewRows[index] : null;

        /// <summary>
        /// The rows currently passing the text and group filters, in their stored order.
        /// View selection (duplicates only) and sorting/grouping happen in OrderedRows.
        /// </summary>
        private IEnumerable<SessionRow> FilteredRows()
        {
            IEnumerable<SessionRow> rows = allRows;

            if (textFilter.Length > 0)
            {
                rows = rows.Where(r =>
                    Contains(r.Note, textFilter) ||
                    Contains(r.MethodAndHost, textFilter) ||
                    Contains(r.Path, textFilter));
            }

            if (groupFilter.Length > 0)
            {
                rows = rows.Where(r => Contains(r.Group, groupFilter));
            }

            return rows;
        }

        /// <summary>
        /// Everything about a request that makes it unique. Two rows sharing this key sent the
        /// same request; the duplicates view lists all but the oldest of each such run as
        /// deletion candidates. Headers take part as stored - whitespace and casing included.
        /// A value tuple: joining text with a separator cannot work here, because the header
        /// text itself contains newlines and field boundaries would blur into false matches.
        /// </summary>
        private static (string MethodAndHost, string Path, string Query, string Body, string Headers) DuplicityKey(
            SessionRow r, Dictionary<int, (string Query, string Headers, string Body)> keys)
            => (r.MethodAndHost, r.Path,
                keys.GetValueOrDefault(r.Id).Query ?? string.Empty,
                keys.GetValueOrDefault(r.Id).Body ?? string.Empty,
                keys.GetValueOrDefault(r.Id).Headers ?? string.Empty);

        /// <summary>
        /// Keeps only rows whose request was sent more than once, dropping the oldest of each
        /// run - that one is the original the duplicates repeat. Pending rows have no stored
        /// request to compare, so they never participate.
        /// </summary>
        private static List<SessionRow> OnlyDuplicates(List<SessionRow> rows, Dictionary<int, (string, string, string)> keys)
        {
            var runs = rows
                .Where(r => !r.IsPending && keys.ContainsKey(r.Id))
                .GroupBy(r => DuplicityKey(r, keys))
                .Where(g => g.Skip(1).Any())
                .ToList();

            //"Oldest" by timestamp, not grid order: a synced-in or imported session's id says
            //nothing about when the request was made, but the duplicate run still has one
            //original and it is the earliest one.
            return runs.SelectMany(g => g.OrderBy(r => r.SortStamp).Skip(1)).ToList();
        }

        private static bool Contains(string haystack, string needle)
            => haystack is not null && haystack.Contains(needle, StringComparison.CurrentCultureIgnoreCase);

        /// <summary>
        /// Adds a newly saved session to the model and the grid.
        /// </summary>
        private async Task AppendSessionRow(Session session)
        {
            allRows.Add(SessionRow.From(session));
            duplicatesKeys = null;
            await RefreshGrid();
        }

        //A request in flight is represented by a row in allRows carrying a negative id: the
        //grid renders it like any other row, and nothing can mistake it for a stored session.
        private int nextPendingId = -1;

        //A fast request would only flash a row, so the placeholder appears once the request
        //has taken longer than this, then ticks its elapsed time.
        private const int PendingRowDelayMs = 750;
        private const int PendingRowTickMs = 1000;

        private static readonly Color PendingRowColor = Color.FromArgb(255, 248, 220);
        private Font pendingRowFont;

        /// <summary>
        /// One request still waiting for a response.
        /// </summary>
        private sealed class PendingRequest
        {
            public SessionRow Row { get; init; }
            public Stopwatch Elapsed { get; } = Stopwatch.StartNew();

            /// <summary>Whether the placeholder made it into the grid before the response arrived.</summary>
            public bool Shown { get; set; }
            public bool Finished { get; set; }
        }

        /// <summary>
        /// Registers a request that has just been sent, so a long running one becomes visible
        /// in the grid instead of showing up only as a wait cursor.
        /// </summary>
        private PendingRequest BeginPendingRow(string httpMethod, Uri requestUri)
        {
            var pending = new PendingRequest
            {
                Row = new SessionRow
                {
                    Id = nextPendingId--,
                    Timestamp = DateTime.Now,
                    StatusCode = null,
                    MethodAndHost = httpMethod + " " + requestUri.Host,
                    Host = requestUri.Host,
                    Path = requestUri.AbsolutePath,
                    Note = PendingNote(TimeSpan.Zero),
                    Group = comboBox_group.Text,
                    IsPending = true
                }
            };

            //Deliberately not awaited - it runs alongside the request and ends with it.
            _ = TrackPendingRow(pending);

            return pending;
        }

        private static string PendingNote(TimeSpan elapsed)
            => "pending... " + ((int)elapsed.TotalSeconds).ToString(CultureInfo.CurrentCulture) + " s";

        /// <summary>
        /// Shows the placeholder once the request turns out to be slow and keeps its elapsed
        /// time ticking. Every await resumes on the UI thread through the WinForms
        /// synchronization context, so the grid is only ever touched from there.
        /// </summary>
        private async Task TrackPendingRow(PendingRequest pending)
        {
            try
            {
                await Task.Delay(PendingRowDelayMs);

                while (!pending.Finished)
                {
                    if (!pending.Shown)
                    {
                        pending.Shown = true;
                        allRows.Add(pending.Row);
                        await RefreshGrid();
                    }
                    else
                    {
                        UpdatePendingNote(pending);
                    }

                    await Task.Delay(PendingRowTickMs);
                }
            }
            catch (ObjectDisposedException)
            {
                //The form was closed while a request was still running.
            }
        }

        /// <summary>
        /// Drops the placeholder. Called from the request's finally block, and idempotent so a
        /// request that failed early is not removed twice.
        /// </summary>
        private async void EndPendingRow(PendingRequest pending)
        {
            if (pending is null || pending.Finished) return;

            pending.Finished = true;
            pending.Elapsed.Stop();

            //Never made it into the grid - nothing to take back out.
            if (!pending.Shown) return;

            allRows.Remove(pending.Row);
            await RefreshGrid();
        }

        private void UpdatePendingNote(PendingRequest pending)
        {
            pending.Row.Note = PendingNote(pending.Elapsed.Elapsed);

            //Only the one cell is invalidated - rebuilding the whole view every second would
            //fight with the user's scrolling and selection.
            int index = viewRows.FindIndex(r => r.Id == pending.Row.Id);
            if (index < 0) return;

            suppressNoteUpdates = true;
            try { dataGridView1.InvalidateCell(dataGridView1.Columns["Note"].Index, index); }
            finally { suppressNoteUpdates = false; }
        }

        private Font headerRowFont;

        /// <summary>Shared header tints - see CellFormatting for why they are shared.</summary>
        private DataGridViewCellStyle headerRowStyle;

        /// <summary>Shared pending tints, same reason.</summary>
        private DataGridViewCellStyle pendingRowStyle;

        /// <summary>
        /// Rebuilds the visible grid rows from <see cref="allRows"/> and the active filters.
        /// Virtual mode: the grid holds no rows, only a count; CellValueNeeded serves each
        /// visible cell from <see cref="viewRows"/> when it is asked.
        /// </summary>
        private async Task RefreshGrid()
        {
            if (checkBox_duplicates.Checked && duplicatesKeys is null)
            {
                await LoadDuplicatesKeys();
            }

            //Changing RowCount moves the current cell, and the sync repaints the grid while the
            //user is reading. Reloading the panes from under them is not acceptable, so RowEnter
            //is muted here and the row that was current is put back afterwards.
            suppressRowEnterDisplay = true;

            int currentId = 0;

            if (dataGridView1.CurrentCell is not null)
            {
                currentId = ViewRow(dataGridView1.CurrentCell.RowIndex)?.Id ?? 0;
            }

            int currentIndex = dataGridView1.CurrentCell?.RowIndex ?? 0;

            //Only a stored session or a live placeholder is worth putting back. A group
            //header's id collides with neither, and its row is rebuilt from scratch with
            //every regrouping anyway.
            bool currentIsReal = currentId > 0 || allRows.Any(r => r.IsPending && r.Id == currentId);

            int restoredIndex = -1;

            try
            {
                RefreshView();

                if (currentIsReal) restoredIndex = viewRows.FindIndex(r => r.Id == currentId);

                if (restoredIndex >= 0) SelectViewRow(restoredIndex);
            }
            finally
            {
                suppressRowEnterDisplay = false;
            }

            //The row that was current is gone - filtered out, deleted here, or deleted by another
            //instance. Move to where it was and let RowEnter load whatever is there now.
            if (restoredIndex < 0 && dataGridView1.RowCount > 0)
            {
                SelectViewRow(Math.Clamp(currentIndex, 0, dataGridView1.RowCount - 1));
            }
        }

        /// <summary>
        /// Selects one virtual row and makes it current. SetSelectedRowCore, the API that keeps
        /// the row shared, is protected; Rows[i].Selected materializes just this row, which is
        /// acceptable for the row or two a user ever keeps selected.
        /// </summary>
        private void SelectViewRow(int index)
        {
            dataGridView1.ClearSelection();
            dataGridView1.CurrentCell = dataGridView1[dataGridView1.Columns["Note"].Index, index];
            dataGridView1.Rows[index].Selected = true;
        }

        private void DataGridView1_CellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            SessionRow r = ViewRow(e.RowIndex);
            if (r is null) return;

            //Header rows carry only their label; the other columns stay empty.
            e.Value = e.ColumnIndex switch
            {
                0 => r.Id,
                1 => r.IsGroupHeader ? null : r.Timestamp,
                2 => r.IsGroupHeader ? null : r.StatusCode,
                3 => r.DisplayMethodAndHost,
                //A header's label is the group key; it sits in the path column because that is
                //what it groups by. DisplayPath carries the label for headers, the path for rows.
                4 => r.DisplayPath,
                5 => r.IsGroupHeader ? null : r.Note,
                _ => r.IsGroupHeader ? null : r.Group
            };
        }

        private void DataGridView1_CellValuePushed(object sender, DataGridViewCellValueEventArgs e)
        {
            SessionRow r = ViewRow(e.RowIndex);
            if (r is null || r.IsGroupHeader) return;

            //The Note column is the only editable one; the store itself happens in
            //CellValueChanged, which the grid raises after the push.
            r.Note = e.Value as string;
        }

        private void DataGridView1_NewRowNeeded(object sender, DataGridViewRowEventArgs e) { }

        private async Task LoadGroups()
        {
            try
            {
                if (sessionsConn is null) return;

                //Only the group names, not every session that carries one.
                var rows = await sessionsConn.RawRowsAsync(
                    "select distinct \"Group\" from Session where Deleted = 0 and \"Group\" is not null and \"Group\" <> ''");

                comboBox_group.Items.Clear();
                comboBox_group.Items.Add("");

                comboBox_filter_group.Items.Clear();
                comboBox_filter_group.Items.Add("");

                foreach (string group in rows.Select(r => SqliteStore.AsString(r[0]))
                                             .OrderBy(g => g, StringComparer.CurrentCultureIgnoreCase))
                {
                    comboBox_group.Items.Add(group);
                    comboBox_filter_group.Items.Add(group);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void Button_request_send_Click(object sender, EventArgs e)
        {
            await SendRequestConsolidate();
        }

        private async void DataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            //Ctrl+R, so that typing an "r" into a cell does not fire a request.
            if (e.KeyCode == Keys.R && e.Control)
            {
                e.Handled = true;
                await SendRequestConsolidate();
            }
        }

        public async Task SendRequestConsolidate()
        {
            if (!int.TryParse(toolStripTextBox_repeat.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int repeat) || repeat < 1)
            {
                MessageBox.Show("Repeat must be a whole number of at least 1.");
                return;
            }

            CursorWait(true);

            try
            {
                for (int y = 0; y < repeat; y++)
                {
                    await SendRequest(
                        textBox_request_body.Text,
                        textBox_request_headers.Text,
                        comboBox_http_method.Text,
                        textBox_request_url.Text,
                        toolStripComboBox_http_version.Text,
                        comboBox_certificates.Text
                        );
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                CursorWait(false);
            }
        }

        public void LoadCertificates()
        {
            using X509Store store = new(StoreName.My, StoreLocation.CurrentUser);

            store.Open(OpenFlags.ReadOnly);

            foreach (X509Certificate2 certificate in store.Certificates)
            {
                if (certificate.SubjectName.Name.Contains('*') == false) comboBox_certificates.Items.Add(certificate.SubjectName.Name);
            }
        }

        private async void DataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            //The rows are removed from allRows and the grid is rebuilt from that, so the grid
            //must not delete the row itself - it would be removing a row that is already gone.
            e.Cancel = true;

            CursorWait(true);

            try
            {
                //Virtual mode has no per-row objects to read the selection from - enumerate
                //selected indexes, which keeps every row shared. e.Row is the uncommitted row
                //Del was pressed on, so it doubles as the fallback for an empty selection.
                int selectedCount = dataGridView1.Rows.GetRowCount(DataGridViewElementStates.Selected);
                var indexes = new List<int>(selectedCount);

                for (int i = 0; i < selectedCount; i++)
                {
                    indexes.Add(dataGridView1.SelectedRows[i].Index);
                }

                if (indexes.Count == 0 && e.Row is not null && e.Row.Index >= 0) indexes.Add(e.Row.Index);
                if (indexes.Count == 0) return;

                //Selection lands just above the topmost row that goes away, rather than
                //jumping back to the top of the table.
                int previous = indexes.Min() - 1;

                //A negative id is a request still in flight, not a stored session - there is
                //nothing to delete, and the placeholder goes away when the request finishes.
                var ids = indexes
                    .Select(i => ViewRow(i)?.Id ?? 0)
                    .Where(id => id > 0)
                    .ToList();

                if (ids.Count == 0) return;

                foreach (int id in ids)
                {
                    await DeleteSession(id, repaint: false);
                }

                //One sweep and one repaint for the whole selection - rebuilding the grid per
                //deleted row took most of the time on a multi-delete.
                var gone = new HashSet<int>(ids);
                allRows.RemoveAll(r => gone.Contains(r.Id));
                await RefreshGrid();

                await SelectGridRow(previous);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                CursorWait(false);
            }
        }

        /// <summary>
        /// Moves the current row, clamped to the rows that are actually there, and displays it.
        /// </summary>
        private async Task SelectGridRow(int index)
        {
            if (dataGridView1.RowCount == 0) return;

            index = Math.Clamp(index, 0, dataGridView1.RowCount - 1);

            //Rebuilding the grid already put the current cell on the first row, so moving it
            //raises RowEnter, which displays the session. When the target is that same row
            //nothing changes and the session has to be displayed here instead.
            bool alreadyCurrent = dataGridView1.CurrentCell?.RowIndex == index;

            //Setting the current cell scrolls it into view; Note is always visible.
            SelectViewRow(index);

            if (alreadyCurrent) await DisplaySession(index);
        }

        /// <summary>
        /// Deletes one stored session. The caller batches: with <paramref name="repaint"/> off
        /// the model and grid are brought up to date once the whole batch is through, instead
        /// of rebuilding the grid after every row.
        /// </summary>
        private async Task DeleteSession(int Id, bool repaint = true)
        {
            try
            {
                if (SyncConfigured)
                {
                    //Kept as a tombstone until the container has taken the delete, so the other
                    //instances get told about it. Hidden from the grid in the meantime.
                    await sessionsConn.ExecuteAsync(
                        "update Session set Deleted = 1, Dirty = 1, UpdatedUtc = $now where Id = $id",
                        ("$now", SyncRow.NowUtc()), ("$id", Id));

                    RequestSync();
                }
                else
                {
                    await sessionsConn.DeleteAsync<Session>(Id);
                }

                //Drop it from the model; the grid is rebuilt from that.
                if (repaint)
                {
                    allRows.RemoveAll(r => r.Id == Id);
                    await RefreshGrid();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void DataGridView1_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            //Repainting the grid moves the current cell; that is not the user changing rows.
            if (suppressRowEnterDisplay) return;

            //Group headers and pending placeholders are no sessions; entering one leaves the
            //panes on whatever session they already showed.
            if (ViewRow(e.RowIndex) is not { Id: > 0 }) return;

            try
            {
                await DisplaySession(e.RowIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private static X509Certificate2 FindCert(X509Store store, string subject)
        {
            foreach (var cert in store.Certificates)
                if (cert.SubjectName.Name.Equals(subject,
                    StringComparison.OrdinalIgnoreCase))
                    return cert;
            return null;
        }


        /// <summary>
        /// The tints a row carries by kind - header, placeholder, ordinary. Styles are shared
        /// instances per kind: the virtual grid materializes a real row only for cells in view,
        /// so these reach visible rows through CellFormatting rather than being copied onto
        /// every row. Note editability is enforced in CellBeginEdit, which cancels the editor.
        /// </summary>
        private void DataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            SessionRow r = ViewRow(e.RowIndex);
            if (r is null) return;

            if (r.IsGroupHeader)
            {
                headerRowFont ??= new Font(dataGridView1.Font, FontStyle.Bold);
                headerRowStyle ??= new DataGridViewCellStyle
                {
                    BackColor = SystemColors.ControlLight,
                    SelectionBackColor = SystemColors.ControlLight,
                    SelectionForeColor = SystemColors.WindowText,
                    Font = headerRowFont,
                    //Explicit, not inherited: a header left at the column's default centering
                    //sits in the middle of the method+host column and reads as misaligned.
                    Alignment = DataGridViewContentAlignment.MiddleLeft
                };

                e.CellStyle = headerRowStyle;
                return;
            }

            if (r.IsPending)
            {
                pendingRowFont ??= new Font(dataGridView1.Font, FontStyle.Italic);
                pendingRowStyle ??= new DataGridViewCellStyle
                {
                    BackColor = PendingRowColor,
                    SelectionBackColor = PendingRowColor,
                    SelectionForeColor = SystemColors.WindowText,
                    Font = pendingRowFont
                };

                e.CellStyle = pendingRowStyle;
            }
        }

        /// <summary>
        /// Note is the one editable column, and only on a real session row - headers and
        /// pending placeholders have no stored row for an edit to land in.
        /// </summary>
        private void DataGridView1_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            SessionRow r = ViewRow(e.RowIndex);

            if (r is null || r.IsGroupHeader || r.IsPending) e.Cancel = true;
        }

        private void DataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (this.dataGridView1.Columns["ResponseStatusCode"].Index == e.ColumnIndex && e.RowIndex >= 0)
            {
                SessionRow source = ViewRow(e.RowIndex);
                if (source is null || source.StatusCode is not int statusCode) return;

                if ((statusCode >= 100) && (statusCode < 300))
                {
                    e.CellStyle.BackColor = Color.Green;
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.SelectionBackColor = Color.Green;
                    e.CellStyle.SelectionForeColor = Color.White;
                }

                if ((statusCode >= 300) && (statusCode < 400))
                {
                    e.CellStyle.BackColor = Color.YellowGreen;
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.SelectionBackColor = Color.YellowGreen;
                    e.CellStyle.SelectionForeColor = Color.White;
                }

                if ((statusCode >= 400) && (statusCode < 600))
                {
                    e.CellStyle.BackColor = Color.Red;
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.SelectionBackColor = Color.Red;
                    e.CellStyle.SelectionForeColor = Color.White;
                }
            }
        }

        /// <summary>
        /// Insert or update Note to the database
        /// </summary>
        private async void DataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            //Fires for programmatic writes too - saving the group edit here would store the
            //group name as the note.
            if (suppressNoteUpdates) return;
            if (e.ColumnIndex != dataGridView1.Columns["Note"].Index) return;

            try
            {
                int clickedId = ViewRow(e.RowIndex)?.Id ?? 0;

                //Headers are read-only and pending rows have no stored row yet, but belt and
                //braces: only a real session id may be persisted.
                if (clickedId <= 0) return;

                Session session = await sessionsConn.FindAsync<Session>(clickedId);
                if (session is null) return;

                session.Note = ViewRow(e.RowIndex)?.Note;
                session.UpdatedUtc = SyncRow.NowUtc();
                session.Dirty = true;

                await sessionsConn.UpdateAsync(session);

                //Keep the model in step - the grid is rebuilt from it, and a repaint would
                //otherwise put the old note back.
                foreach (SessionRow r in allRows.Where(r => r.Id == clickedId)) r.Note = session.Note;

                RequestSync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }



        private async Task SaveSplitterData()
        {
            _settings.splitContainer5 = splitContainer5_reqres.SplitterDistance;
            _settings.splitContainer1 = splitContainer1_main_form.SplitterDistance;

            _settings.LocationX = Location.X;
            _settings.LocationY = Location.Y;
            _settings.SizeHeight = Size.Height;
            _settings.SizeWidth = Size.Width;


            //Name-addressed: the column's position has changed before, and an index that used
            //to land on UriHost once that was the fourth column silently saved the wrong one.
            _settings.DataGridViewCol3Width = dataGridView1.Columns["UriHost"].Width;
            _settings.DataGridViewDateTimeWidth = dataGridView1.Columns["DateTime"].Width;
            _settings.DataGridViewPathWidth = dataGridView1.Columns["UriAbsolutePath"].Width;
            _settings.DataGridViewStatusCodeWidth = dataGridView1.Columns["ResponseStatusCode"].Width;

            _settings.GroupByUrl = checkBox_group_url.Checked;

            if (_settings.Id != 0) await settingsConn.UpdateAsync(_settings);
        }

        private void ApplySavedSplitterData()
        {
            Location = new Point(_settings.LocationX, _settings.LocationY);
            Size = new Size(_settings.SizeWidth, _settings.SizeHeight);

            //A saved distance from a larger window can exceed the current bounds.
            SetSplitterDistance(splitContainer5_reqres, _settings.splitContainer5);
            SetSplitterDistance(splitContainer1_main_form, _settings.splitContainer1);
        }

        private static void SetSplitterDistance(SplitContainer container, int distance)
        {
            int span = container.Orientation == Orientation.Vertical ? container.Width : container.Height;
            int max = span - container.Panel2MinSize - container.SplitterWidth;

            if (max <= container.Panel1MinSize) return;

            container.SplitterDistance = Math.Clamp(distance, container.Panel1MinSize, max);
        }




        private void Button_text_utils_Click(object sender, EventArgs e)
        {
            using TextUtilsForm textUtils = new();
            textUtils.ShowDialog();
        }

        private readonly TextStyle primary = new(Brushes.Brown, null, FontStyle.Regular);
        private readonly TextStyle secondary = new(Brushes.RoyalBlue, null, FontStyle.Regular);
        private readonly TextStyle blueStyle = new(Brushes.Blue, null, FontStyle.Underline);

        private void TextBox_request_headers_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.ChangedRange.ClearStyle(primary);
            e.ChangedRange.ClearStyle(secondary);
            e.ChangedRange.SetStyle(secondary, "[a-zA-Z]+.*[a-zA-Z]+:", System.Text.RegularExpressions.RegexOptions.Multiline);
            e.ChangedRange.SetStyle(primary, "^.*:.*$", System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        private void TextBox_response_headers_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.ChangedRange.ClearStyle(primary);
            e.ChangedRange.ClearStyle(secondary);
            e.ChangedRange.ClearStyle(blueStyle);
            e.ChangedRange.SetStyle(secondary, "[a-zA-Z]+.*[a-zA-Z]+:", System.Text.RegularExpressions.RegexOptions.Multiline);
            e.ChangedRange.SetStyle(primary, "^.*:.*$", System.Text.RegularExpressions.RegexOptions.Multiline);
            e.ChangedRange.SetStyle(blueStyle, @"(http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?", System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        //makes link in the response headers clickable
        private bool CharIsHyperlink(Place place)
        {
            if (place.iLine < 0 || place.iLine >= textBox_response_headers.LinesCount) return false;
            if (place.iChar >= textBox_response_headers.GetLineLength(place.iLine)) return false;

            return textBox_response_headers.GetStylesOfChar(place).Contains(blueStyle);
        }

        private void TextBox_response_headers_MouseMove(object sender, MouseEventArgs e)
        {
            var p = textBox_response_headers.PointToPlace(e.Location);
            if (CharIsHyperlink(p))
                textBox_response_headers.Cursor = Cursors.Hand;
            else
                textBox_response_headers.Cursor = Cursors.IBeam;
        }

        private void TextBox_response_headers_MouseDown(object sender, MouseEventArgs e)
        {
            var p = textBox_response_headers.PointToPlace(e.Location);
            if (!CharIsHyperlink(p)) return;

            var url = textBox_response_headers.GetRange(p, p).GetFragment(@"[\S]").Text;

            //The URL comes from a server controlled response header - only hand http(s) to the shell.
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;

            try
            {
                //UseShellExecute defaults to false on .NET Core, which cannot launch a URL.
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                MessageBox.Show("Could not open " + uri.AbsoluteUri + "\n\n" + ex.Message);
            }
        }

        //----

        private void TextBox_request_url_TextChanged(object sender, TextChangedEventArgs e)
        {
            e.ChangedRange.ClearStyle(primary);
            e.ChangedRange.ClearStyle(secondary);
            e.ChangedRange.SetStyle(secondary, "[a-zA-Z]+://", System.Text.RegularExpressions.RegexOptions.Multiline);
            e.ChangedRange.SetStyle(primary, "/", System.Text.RegularExpressions.RegexOptions.Multiline);
            e.ChangedRange.SetStyle(primary, "\\?", System.Text.RegularExpressions.RegexOptions.Multiline);
            e.ChangedRange.SetStyle(primary, "=", System.Text.RegularExpressions.RegexOptions.Multiline);
            e.ChangedRange.SetStyle(primary, "&", System.Text.RegularExpressions.RegexOptions.Multiline);
        }

        private void Button_clearAll_Click(object sender, EventArgs e)
        {
            textBox_request_body.Clear();
            textBox_request_headers.Clear();
            textBox_request_url.Clear();
            textBox_response_body.Clear();
            textBox_response_headers.Clear();
        }



        private async void Button_saveGroup_Click(object sender, EventArgs e)
        {
            try
            {
                if (!int.TryParse(label_displayed_Id.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int Id))
                {
                    MessageBox.Show("Select a session first.");
                    return;
                }

                string group = comboBox_group.Text;

                var session = await sessionsConn.FindAsync<Session>(Id);

                if (session is null) return;

                session.Group = group;
                session.UpdatedUtc = SyncRow.NowUtc();
                session.Dirty = true;

                await sessionsConn.UpdateAsync(session);

                RequestSync();

                //Update the model and repaint. RefreshGrid suppresses note persistence while
                //it populates, so this cannot be mistaken for the user editing the note column.
                foreach (SessionRow r in allRows.Where(r => r.Id == Id)) r.Group = group;
                await RefreshGrid();

                await LoadGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        //Filtering is plain string matching over the model. The old DataView.RowFilter took a
        //SQL-ish expression, which meant escaping user input to avoid a syntax error on a quote.
        private async void TextBox_filter_TextChanged(object sender, EventArgs e)
        {
            textFilter = textBox_filter.Text.Trim();
            await RefreshGrid();
        }

        private async void ComboBox_filter_group_SelectedIndexChanged(object sender, EventArgs e)
        {
            groupFilter = comboBox_filter_group.Text.Trim();
            await RefreshGrid();
        }

        private async void CheckBox_group_url_CheckedChanged(object sender, EventArgs e)
        {
            await RefreshGrid();
        }

        private async void CheckBox_duplicates_CheckedChanged(object sender, EventArgs e)
        {
            await RefreshGrid();
        }

        /// <summary>
        /// Syncs on demand. The same work the app does by itself - the button is for when the
        /// user wants to watch it happen, or to retry after a failure.
        /// </summary>
        private async void button_blob_Click(object sender, EventArgs e)
        {
            CursorWait(true);

            try
            {
                await SyncNow(verbose: true);
            }
            finally
            {
                CursorWait(false);
            }
        }

        private async void button_blob_list_Click(object sender, EventArgs e)
        {
            try
            {
                if (!BlobConfigured(complain: true)) return;

                //Reports this instance's own id, which lives in the local sync tables.
                await EnsureSyncSchema();

                await GetBlobList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void dataGridView1_RowContextMenuStripNeeded(object sender, DataGridViewRowContextMenuStripNeededEventArgs e)
        {
            if (e.RowIndex >= 0)
            { e.ContextMenuStrip = contextMenuStrip1; }

        }

        private async void copyToToolStripMenuItem1_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            await copySessionToNewProfile(e);
        }

        private async void CopyToToolStripMenuItem_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            await copySessionToNewProfile(e);
        }

        private async Task copySessionToNewProfile(ToolStripItemClickedEventArgs e)
        {
            try
            {
                if (!int.TryParse(e.ClickedItem.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out int selectedId)) return;

                //get target profile
                await settingsConn.EnsureTableAsync<Setting>();
                Setting targetProfile = await settingsConn.FindAsync<Setting>(selectedId);

                if (targetProfile is null || string.IsNullOrWhiteSpace(targetProfile.Endpoint))
                {
                    MessageBox.Show("The selected profile has no database configured.");
                    return;
                }

                //get selected session
                if (dataGridView1.CurrentCell is null || ViewRow(dataGridView1.CurrentCell.RowIndex) is not { } currentRow)
                {
                    MessageBox.Show("Select a session first.");
                    return;
                }

                int Id = currentRow.Id;

                //Pending rows and group headers look like rows but there is no stored
                //session behind them to copy.
                if (Id <= 0)
                {
                    MessageBox.Show("That request has not finished yet.");
                    return;
                }

                var session = await sessionsConn.FindAsync<Session>(Id);
                if (session is null) return;

                CursorWait(true);

                //Write through a separate connection. Reassigning sessionsConn would silently
                //point the whole app at the other profile's database.
                var targetConn = new SqliteStore(targetProfile.Endpoint);

                try
                {
                    await targetConn.EnsureTableAsync<Session>();

                    //Let the target assign its own key, otherwise the copy collides with an
                    //existing row of the same id.
                    session.Id = 0;

                    //A copy is a session of its own, and the target profile has not published it.
                    //Its own instance picks it up the next time that profile is opened.
                    session.Uid = NewUid();
                    session.UpdatedUtc = SyncRow.NowUtc();
                    session.Dirty = true;
                    session.Uploaded = false;
                    session.Deleted = false;

                    await targetConn.InsertAsync(session);
                }
                finally
                {
                    await targetConn.CloseAsync();
                }

                CursorWait(false);
                MessageBox.Show("Session copied to profile \"" + targetProfile.ProfileName + "\".");
            }
            catch (Exception ex)
            {
                CursorWait(false);
                MessageBox.Show(ex.Message);
            }
        }
    }

}
