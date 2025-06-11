using FastColoredTextBoxNS;
using SQLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Linq;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolTip;
using System.Diagnostics.Tracing;

namespace ApiTester
{
    //TODO: uložit a načíst splitter a location hodnoty do sqlite, nastavit nějaké rozumné výchozí hodnoty hlavně velikosti okna

    public partial class Form1 : Form
    {
        private SQLiteAsyncConnection settingsConn = new("settings.sqlite");
        private SQLiteAsyncConnection sessionsConn;
        public static ServerCertificate serverCertificate = new();
        private DataTable dtSessions = new();
        private static Setting _settings = new();



        public Form1()
        {
            InitializeComponent();


            this.Text = "API Tester  v" + this.ProductVersion;

            typeof(DataGridView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, dataGridView1, new object[] { true });

            dtSessions.Columns.Add("Id", typeof(int));
            dtSessions.Columns.Add("DateTime", typeof(DateTime));
            dtSessions.Columns.Add("ResponseStatusCode", typeof(int));
            //dtSessions.Columns.Add("Method", typeof(string));
            dtSessions.Columns.Add("UriHost", typeof(string));
            dtSessions.Columns.Add("UriAbsolutePath", typeof(string));
            dtSessions.Columns.Add("Note", typeof(string));
            dtSessions.Columns.Add("Group", typeof(string));

            comboBox_settings_profiles.DisplayMember = "ProfileName";
            comboBox_settings_profiles.ValueMember = "Id";

            LoadSettings();
            LoadCertificates();

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

            var items_url = new List<AutocompleteItem>
            {
                new AutocompleteItem("https://"),
                new AutocompleteItem("https://apigw-dev.skoda.vwg/"),
                new AutocompleteItem("https://apigw-test.skoda.vwg/"),
                new AutocompleteItem("https://apigw-prod.skoda.vwg/"),
                new AutocompleteItem("https://gw-samb-dev.skoda-api.com/"),
                new AutocompleteItem("https://gw-samb-test.skoda-api.com/"),
                new AutocompleteItem("https://gw-samb-prod.skoda-api.com/")
            };
            request_url_AutocompleteMenu.Items.SetAutocompleteItems(items_url);

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSplitterData();
        }


        public async void LoadSessions()
        {
            CursorWait(true);

            dataGridView1.SuspendLayout();
            dataGridView1.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            try
            {
                await sessionsConn.CreateTableAsync<RequestTelemetry>();
                await sessionsConn.CreateTableAsync<Session>();


                AsyncTableQuery<Session> query = sessionsConn.Table<Session>();

                List<Session> allSessions = query.ToListAsync().Result.Select(x => new Session
                {
                    Id = x.Id,
                    DateTime = x.DateTime,
                    ResponseStatusCode = x.ResponseStatusCode,
                    Method = x.Method,
                    UriHost = x.UriHost,
                    UriAbsolutePath = x.UriAbsolutePath,
                    Note = x.Note,
                    Group = x.Group,
                }).ToList();

                dtSessions.Clear();

                foreach (Session s in allSessions)
                {
                    dtSessions.Rows.Add(new object[] { s.Id, s.DateTime, s.ResponseStatusCode, s.Method + " " + s.UriHost, s.UriAbsolutePath, s.Note, s.Group });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            dataGridView1.DataSource = dtSessions;

            //Id
            dataGridView1.Columns[0].Visible = false;

            //DateTime
            dataGridView1.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            //dataGridView1.Columns[1].Width = 110;
            //dataGridView1.Columns[1].MinimumWidth = 110;
            dataGridView1.Columns[1].ReadOnly = true;

            //ResponseStatusCode
            dataGridView1.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            dataGridView1.Columns[2].Width = 35;
            dataGridView1.Columns[2].MinimumWidth = 35;
            dataGridView1.Columns[2].ReadOnly = true;

            //Method
            //dataGridView1.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
            //dataGridView1.Columns[3].ReadOnly = true;

            //UriHost
            dataGridView1.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;
            dataGridView1.Columns[3].ReadOnly = true;
            dataGridView1.Columns[3].Width = _settings.dataGridView1_col3_width;

            //UriAbsolutePath
            dataGridView1.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
            dataGridView1.Columns[4].ReadOnly = true;
       
            //Note
            dataGridView1.Columns[5].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            //Group
            dataGridView1.Columns[6].Visible = false;


            dataGridView1.ResumeLayout();

            await LoadGroups();

            //Scroll down to the newest record
            if (dataGridView1.RowCount > 0) dataGridView1.FirstDisplayedScrollingRowIndex = dataGridView1.RowCount - 1;

            //Select a new row
            if (dataGridView1.RowCount > 0) dataGridView1.Rows[dataGridView1.Rows.Count - 1].Selected = true;


            //display content of the newly saved session
            if (dataGridView1.RowCount > 0) await DisplaySession(dataGridView1.Rows.Count - 1);

            CursorWait(false);
        }

        private async Task LoadGroups()
        {
            try
            {
                await sessionsConn.CreateTableAsync<Session>();

                AsyncTableQuery<Session> query = sessionsConn.Table<Session>();
                var sessions = await query.ToListAsync();

                comboBox_group.Items.Clear();
                comboBox_group.Items.Insert(0, "");

                comboBox_filter_group.Items.Clear();
                comboBox_filter_group.Items.Insert(0, "");


                foreach (Session s in sessions)
                {
                    if ((s.Group != null) && (!comboBox_group.Items.Contains(s.Group))) comboBox_group.Items.Add(s.Group);
                    if ((s.Group != null) && (!comboBox_filter_group.Items.Contains(s.Group))) comboBox_filter_group.Items.Add(s.Group);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void Button_request_send_Click(object sender, EventArgs e)
        {
            SendRequestConsolidate();
        }

        private void DataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.R) SendRequestConsolidate();
        }

        public async void SendRequestConsolidate()
        {
            //Works only when MaxDegreeOfParallelism = 1 - need to separate actual call from UI
            //var options = new ParallelOptions()
            //{
            //    MaxDegreeOfParallelism = 1
            //};

            //Parallel.For(0, Convert.ToInt32(numericUpDown_request.Text), options, (i) =>
            //{
            //    SendRequest();
            //});

            CursorWait(true);

            for (int y = 0; y < Convert.ToInt32(toolStripTextBox_repeat.Text); y++)
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

            CursorWait(false);
        }

        public async Task LoadCertificates()
        {
            X509Store store = new(StoreName.My, StoreLocation.CurrentUser);

            store.Open(OpenFlags.ReadOnly);

            foreach (X509Certificate2 certificate in store.Certificates)
            {
                if (certificate.SubjectName.Name.Contains('*') == false) comboBox_certificates.Items.Add(certificate.SubjectName.Name);
            }
        }

        private async void DataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            CursorWait(true);

            foreach (DataGridViewRow row in dataGridView1.SelectedRows)
            {
                if (dataGridView1.SelectedRows.Count > 0)
                {
                    DeleteSession((int)row.Cells[0].Value);
                    Thread.Sleep(10);
                }
            }
            CursorWait(false);
        }

        private async Task DeleteSession(int Id)
        {
            try
            {
                //Delete from settingsConn
                var query = sessionsConn.Table<Session>().Where(s => s.Id == Id);
                var session = await query.FirstOrDefaultAsync();
                if (session != null)
                {
                    await sessionsConn.DeleteAsync(session);
                    int localVersion = Convert.ToInt32(await sessionsConn.ExecuteScalarAsync<int>("pragma user_version;"));
                    await sessionsConn.ExecuteAsync($"pragma user_version = " + (localVersion + 1) + ";");
                }

                //Delete from datagrid
                foreach (DataRow dr in dtSessions.Rows)
                {
                    if (dr["Id"].ToString() == Id.ToString())
                        dr.Delete();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                CursorWait(false);
            }
        }

        private async void DataGridView1_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex > 0)
            {
                await DisplaySession(e.RowIndex);
            }
        }

        private static X509Certificate2 FindCert(X509Store store, string subject)
        {
            foreach (var cert in store.Certificates)
                if (cert.SubjectName.Name.Equals(subject,
                    StringComparison.CurrentCultureIgnoreCase))
                    return cert;
            return null;
        }


        private void DataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (this.dataGridView1.Columns["ResponseStatusCode"].Index == e.ColumnIndex && e.RowIndex >= 0)
            {
                if (((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value >= 100) && ((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value < 300))
                {
                    e.CellStyle.BackColor = Color.Green;
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.SelectionBackColor = Color.Green;
                    e.CellStyle.SelectionForeColor = Color.White;
                }

                if (((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value >= 300) && ((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value < 400))
                {
                    e.CellStyle.BackColor = Color.YellowGreen;
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.SelectionBackColor = Color.YellowGreen;
                    e.CellStyle.SelectionForeColor = Color.White;
                }

                if (((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value >= 400) && ((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value < 600))
                {
                    e.CellStyle.BackColor = Color.Red;
                    e.CellStyle.ForeColor = Color.White;
                    e.CellStyle.SelectionBackColor = Color.Red;
                    e.CellStyle.SelectionForeColor = Color.White;
                }
            }

            //if (e.ColumnIndex >= 0 && e.RowIndex >= 0 && this.dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Selected)
            //{
            //    e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            //    e.CellStyle.SelectionBackColor = e.CellStyle.BackColor;
            //}
        }

        /// <summary>
        /// Insert or update Note to the database
        /// </summary>
        private async void DataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                string clickedId = dataGridView1.Rows[e.RowIndex].Cells["Id"].Value.ToString();

                Session session = new();
                try
                {
                    var query = sessionsConn.Table<Session>().Where(s => s.Id.Equals(clickedId));
                    session = await query.FirstAsync();

                    session.Note = (string)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

                    await sessionsConn.UpdateAsync(session);
                    int localVersion = Convert.ToInt32(await sessionsConn.ExecuteScalarAsync<int>("pragma user_version;"));
                    await sessionsConn.ExecuteAsync($"pragma user_version = " + (localVersion + 1) + ";");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }



        private async void SaveSplitterData()
        {
            //_settings.splitContainer6 = splitContainer6_main_right.SplitterDistance;
            _settings.splitContainer5 = splitContainer5_reqres.SplitterDistance;
            //_settings.splitContainer3 = splitContainer4.SplitterDistance;
            //_settings.splitContainer2 = splitContainer2_request.SplitterDistance;
            _settings.splitContainer1 = splitContainer1_main_form.SplitterDistance;

            _settings.LocationX = Location.X;
            _settings.LocationY = Location.Y;
            _settings.SizeHeight = Size.Height;
            _settings.SizeWidth = Size.Width;


            _settings.dataGridView1_col3_width = dataGridView1.Columns[3].Width;

            await settingsConn.UpdateAsync(_settings);
        }

        private void ApplySavedSplitterData()
        {

            Location = new Point(_settings.LocationX, _settings.LocationY);
            Size = new Size(_settings.SizeWidth, _settings.SizeHeight);

            //splitContainer6_main_right.SplitterDistance = _settings.splitContainer6;
            splitContainer5_reqres.SplitterDistance = _settings.splitContainer5;
            //splitContainer4.SplitterDistance = _settings.splitContainer3;
            //splitContainer2_request.SplitterDistance = _settings.splitContainer2;
            splitContainer1_main_form.SplitterDistance = _settings.splitContainer1;

            //not initialized here yet
            //dataGridView1.Columns[3].Width = _settings.dataGridView1_col3_width;
        }




        private void Button_text_utils_Click(object sender, EventArgs e)
        {
            Form_text_utils form_Text_Utils = new();
            form_Text_Utils.ShowDialog();
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
            var mask = textBox_response_headers.GetStyleIndexMask(new Style[] { blueStyle });
            if (place.iChar < textBox_response_headers.GetLineLength(place.iLine))
                if ((textBox_response_headers[place].style & mask) != 0)
                    return true;

            return false;
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
            if (CharIsHyperlink(p))
            {
                var url = textBox_response_headers.GetRange(p, p).GetFragment(@"[\S]").Text;
                System.Diagnostics.Process.Start(url);
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
            //comboBox_certificates.Items.Clear();
        }

        

        private async void Button_saveGroup_Click(object sender, EventArgs e)
        {
            //updatovat aktuálně zobrazenou session v settingsConn
            int Id = Convert.ToInt32(label_displayed_Id.Text);
            string group = comboBox_group.Text;

            var query = sessionsConn.Table<Session>().Where(s => s.Id == Id);
            var session = await query.FirstAsync();

            if (session != null) session.Group = group;

            await sessionsConn.UpdateAsync(session);
            int localVersion = Convert.ToInt32(await sessionsConn.ExecuteScalarAsync<int>("pragma user_version;"));
            await sessionsConn.ExecuteAsync($"pragma user_version = " + (localVersion + 1) + ";");


            //update datagridview
            foreach (DataRow dr in dtSessions.Rows)
            {
                if (dr["Id"].ToString() == Id.ToString())
                    dr["Group"] = group;
            }

            LoadGroups();
        }

        private void TextBox_filter_TextChanged(object sender, EventArgs e)
        {
            if (textBox_filter.Text.Length > 0)
            {
                // dtSessions.DefaultView.RowFilter = string.Format("[{0}] LIKE '%{1}%'", "Note", textBox_filter.Text);
                dtSessions.DefaultView.RowFilter = "Note LIKE '%" + textBox_filter.Text.Trim() + "%' OR UriHost LIKE '%" + textBox_filter.Text.Trim() + "%' OR UriAbsolutePath LIKE '%" + textBox_filter.Text.Trim() + "%'";
            }
            else
            {
                dtSessions.DefaultView.RowFilter = String.Empty;
            }
        }
        private void ComboBox_filter_group_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox_filter_group.Text.Length > 0)
            {
                dtSessions.DefaultView.RowFilter = "Group LIKE '%" + comboBox_filter_group.Text.Trim() + "%'";
            }
            else
            {
                dtSessions.DefaultView.RowFilter = String.Empty;
            }
        }

        private async void button_blob_Click(object sender, EventArgs e)
        {
            listBox_blob.Items.Clear();


            FileInfo localdb = new FileInfo(_settings.Endpoint);

            //get version of db in Azure Blob
            int blobVersion = GetBlobVersion(localdb.Name);
            listBox_blob.Items.Add("blobVersion: " + blobVersion.ToString());

            //Read user_version pragma from local db
            var localVersion = Convert.ToInt32(await sessionsConn.ExecuteScalarAsync<int>("pragma user_version;"));
            listBox_blob.Items.Add("localVersion: " + localVersion.ToString());
            await sessionsConn.CloseAsync();

            if (blobVersion < localVersion)  //upload local file to blob
            {
                listBox_blob.Items.Add("Local db will be uploaded to blob.");
                await BlobUpload(localdb.FullName, localVersion);
                listBox_blob.Items.Add("Local uploaded to blob: " + localdb.FullName);

            }
            else if (blobVersion > localVersion)  //download file from blob to local
            {
                listBox_blob.Items.Add("Db from Azure blob will be downloaded.");

                string newname = localdb.FullName + "_" + DateTime.Now.ToFileTime();
                File.Move(localdb.FullName, newname);
                listBox_blob.Items.Add("Local db renamed to: " + newname);

                await BlobDownload(localdb.Name, localdb.FullName);
                listBox_blob.Items.Add("Blob downloaded as: " + localdb.Name);

            }

            LoadSessions();
        }

        private async void button_blob_list_Click(object sender, EventArgs e)
        {
            GetBlobList();

            var localVersion = Convert.ToInt32(await sessionsConn.ExecuteScalarAsync<int>("pragma user_version;"));
            listBox_blob.Items.Insert(0, "Local version: " + localVersion.ToString());

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
            //get target profile
            Setting targetProfile = new();

            AsyncTableQuery<Setting> query;

            try
            {
                await settingsConn.CreateTableAsync<Setting>();

                int selectedId = Convert.ToInt32(e.ClickedItem.Name);

                query = settingsConn.Table<Setting>().Where(s => s.Id == selectedId);
                targetProfile = await query.FirstAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }


            //get selected session
            int Id = (int)dataGridView1.CurrentRow.Cells["Id"].Value;

            var query1 = sessionsConn.Table<Session>().Where(s => s.Id == Id);
            var session = await query1.FirstOrDefaultAsync();

            //save session to the target profile
            try
            {
                //session.id = session.sqliteId.ToString();
                sessionsConn = new SQLiteAsyncConnection(targetProfile.Endpoint);
                await sessionsConn.InsertAsync(session);
            }
            catch (Exception ex)
            {
                CursorWait(false);
                MessageBox.Show(ex.Message);
            }
        }
    }

}
