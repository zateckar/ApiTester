using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace ApiTester
{
    /// <summary>
    /// The Files tab: a browser over the remote store the profile points at - the blob
    /// container, or the DevOps repository when the profile syncs with DevOps. The remote
    /// operations themselves sit behind <see cref="IFilesBackend"/> (BlobFiles.cs,
    /// DevOpsFiles.cs) - this is navigation, the transfer plans, and drag and drop.
    /// </summary>
    public partial class Form1 : Form
    {
        /// <summary>
        /// The folder being shown. Empty is the container root; anything else ends in a slash.
        /// </summary>
        private string filesPrefix = string.Empty;

        private readonly List<BlobFile> filesEntries = new();

        /// <summary>
        /// The tab has listed the container at least once. Kept so switching to it fetches a
        /// listing, while switching back to it does not.
        /// </summary>
        private bool filesLoaded;

        private bool filesBusy;
        private CancellationTokenSource filesCts;

        private int filesSortColumn;
        private bool filesSortDescending;

        //Progress arrives per chunk, which is far more often than a progress bar can show.
        private long filesProgressTick;

        /// <summary>
        /// The entries a drag that started in this list is carrying. Non-null only while that
        /// drag is running, which is also how a drop tells our own items from Explorer's.
        /// </summary>
        private List<BlobFile> filesDragSource;

        /// <summary>
        /// What went wrong while handing files to the drop target. It cannot be reported from
        /// there - the drag loop owns the screen - so it waits until the drag is over.
        /// </summary>
        private string filesDragError;

        /// <summary>Where blobs are downloaded to when they are dragged out of the window.</summary>
        private string filesStagingRoot;

        private ListViewItem filesDropTarget;

        /// <summary>
        /// The store the tab files against, chosen the same way the sync chooses its backend:
        /// the DevOps repository when the profile syncs with DevOps, the blob container
        /// otherwise. Resolved at the start of each operation, so a settings edit cannot swap
        /// the backend underneath a running transfer.
        /// </summary>
        private static IFilesBackend FilesBackendForThisRun
            => CurrentSettings.SyncWithDevOps
                ? new DevOpsFilesBackend(CurrentSettings)
                : (IFilesBackend)new AzureBlobFilesBackend();

        /// <summary>What the place a file lives in is called on the active backend.</summary>
        private static string FilesStoreNoun => CurrentSettings.SyncWithDevOps ? "repository" : "container";

        /// <summary>One file of a transfer, either direction.</summary>
        private sealed class TransferItem
        {
            public string Blob { get; init; }
            public string Local { get; init; }
            public long Size { get; init; }
        }

        private void SetupFilesTab()
        {
            listView_files.SmallImageList = BuildFileIcons();

            FilesStatus("Not listed yet.");
        }

        /// <summary>
        /// Two 16x16 glyphs, drawn rather than shipped: a folder and a page. The list needs to
        /// be readable at a glance and nothing here is worth a pair of image files.
        /// </summary>
        private ImageList BuildFileIcons()
        {
            var images = new ImageList(components) { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };

            var folder = new Bitmap(16, 16);

            using (Graphics canvas = Graphics.FromImage(folder))
            using (var back = new SolidBrush(Color.FromArgb(232, 179, 74)))
            using (var front = new SolidBrush(Color.FromArgb(252, 208, 116)))
            {
                canvas.FillRectangle(back, 1, 3, 7, 3);
                canvas.FillRectangle(front, 1, 5, 14, 9);
            }

            var file = new Bitmap(16, 16);

            using (Graphics canvas = Graphics.FromImage(file))
            using (var page = new SolidBrush(Color.FromArgb(250, 250, 250)))
            using (var edge = new Pen(Color.FromArgb(140, 140, 140)))
            using (var line = new Pen(Color.FromArgb(190, 190, 190)))
            {
                canvas.FillRectangle(page, 3, 1, 10, 14);
                canvas.DrawRectangle(edge, 3, 1, 10, 14);

                for (int y = 4; y <= 12; y += 3)
                {
                    canvas.DrawLine(line, 5, y, 11, y);
                }
            }

            //Not disposed here, deliberately: an ImageList holds on to the original image until
            //its native handle is created, which is at the first paint. Disposing now breaks it
            //then. Both go when the list does, which the form's components own.
            images.Images.Add(folder);
            images.Images.Add(file);

            return images;
        }

        /// <summary>
        /// Forgets what the tab is showing. The container, and everything in it, belongs to the
        /// profile - a different profile is a different container.
        /// </summary>
        private void ResetFilesTab()
        {
            filesPrefix = string.Empty;
            filesEntries.Clear();
            filesLoaded = false;

            if (IsDisposed || Disposing) return;

            listView_files.Items.Clear();
            textBox_files_path.Text = string.Empty;
            FilesStatus("Not listed yet.");
        }

        // ---------------------------------------------------------------- listing

        private static string NormalizePrefix(string prefix)
        {
            string value = (prefix ?? string.Empty).Trim().Replace('\\', '/').TrimStart('/');

            if (value.Length > 0 && !value.EndsWith('/')) value += "/";

            return value;
        }

        private async Task NavigateFiles(string prefix)
        {
            filesPrefix = NormalizePrefix(prefix);
            textBox_files_path.Text = filesPrefix;

            await RefreshFiles();
        }

        private Task RefreshFiles()
        {
            return RunFilesOperation("Listing", (backend, ct) => ReloadCurrent(backend, ct));
        }

        private async Task ReloadCurrent(IFilesBackend backend, CancellationToken ct)
        {
            List<BlobFile> entries = await backend.Browse(filesPrefix, ct);

            filesEntries.Clear();
            filesEntries.AddRange(entries);
            filesLoaded = true;

            PopulateFilesList();
        }

        private void PopulateFilesList()
        {
            if (IsDisposed || Disposing) return;

            filesEntries.Sort(CompareEntries);

            listView_files.BeginUpdate();

            try
            {
                ClearDropHighlight();
                listView_files.Items.Clear();

                //A row for the way out, so the parent folder is both one click and one drop away.
                if (filesPrefix.Length > 0)
                {
                    var up = new ListViewItem("..") { ImageIndex = 0, Tag = null };

                    up.SubItems.Add(string.Empty);
                    up.SubItems.Add(string.Empty);
                    up.SubItems.Add("Parent folder");

                    listView_files.Items.Add(up);
                }

                long bytes = 0;
                int folders = 0;
                bool sizesKnown = true;

                foreach (BlobFile entry in filesEntries)
                {
                    var item = new ListViewItem(entry.Name) { ImageIndex = entry.IsFolder ? 0 : 1, Tag = entry };

                    //A negative length is "the backend cannot say" (a DevOps listing carries no
                    //sizes) - the column and the total leave it out rather than printing "0 B".
                    item.SubItems.Add(entry.IsFolder || entry.Length < 0 ? string.Empty : FormatSize(entry.Length));
                    item.SubItems.Add(entry.Modified?.ToString("g", CultureInfo.CurrentCulture) ?? string.Empty);
                    item.SubItems.Add(EntryType(entry));

                    listView_files.Items.Add(item);

                    if (entry.IsFolder) folders++;
                    else if (entry.Length >= 0) bytes += entry.Length;
                    else sizesKnown = false;
                }

                int files = filesEntries.Count - folders;

                FilesStatus(folders + " folder(s), " + files + " file(s)"
                    + (sizesKnown ? ", " + FormatSize(bytes) : string.Empty)
                    + "  -  drop files here to upload, drag items out to download.");
            }
            finally
            {
                listView_files.EndUpdate();
            }
        }

        private static string EntryType(BlobFile entry)
        {
            if (entry.IsFolder) return "Folder";

            string extension = Path.GetExtension(entry.Name);

            return extension.Length > 1 ? extension.Substring(1).ToUpperInvariant() + " file" : "File";
        }

        private int CompareEntries(BlobFile left, BlobFile right)
        {
            //Folders lead whichever column is sorted on - a listing is read as a tree, not as
            //a table that happens to hold both.
            if (left.IsFolder != right.IsFolder) return left.IsFolder ? -1 : 1;

            int result = filesSortColumn switch
            {
                1 => left.Length.CompareTo(right.Length),
                2 => Nullable.Compare(left.Modified, right.Modified),
                3 => string.Compare(EntryType(left), EntryType(right), StringComparison.OrdinalIgnoreCase),
                _ => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
            };

            if (result == 0) result = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);

            return filesSortDescending ? -result : result;
        }

        // ---------------------------------------------------------------- running an operation

        /// <summary>
        /// Runs one file operation, against the backend resolved once up front: keeps a second
        /// one from starting on top of it, gives it a token the Cancel button can trip, and
        /// turns whatever it throws into a line of status.
        /// </summary>
        private async Task RunFilesOperation(string what, Func<IFilesBackend, CancellationToken, Task> work)
        {
            if (filesBusy)
            {
                FilesStatus("Busy - wait for the current transfer or cancel it.");
                return;
            }

            if (!BlobConfigured(complain: false))
            {
                FilesStatus(CurrentSettings.SyncWithDevOps
                    ? "Set the DevOps repository, branch and PAT in Settings first."
                    : "Set the storage account, container and SAS token (or account key) in Settings first.");
                return;
            }

            filesCts = new CancellationTokenSource();
            SetFilesBusy(true);

            try
            {
                await work(FilesBackendForThisRun, filesCts.Token);
            }
            catch (OperationCanceledException)
            {
                FilesStatus(what + " cancelled.");
            }
            catch (Exception ex) when (ex is BlobFileException or HttpRequestException or IOException
                                          or UnauthorizedAccessException or XmlException or NotSupportedException)
            {
                FilesStatus(ex.Message);
            }
            finally
            {
                filesCts.Dispose();
                filesCts = null;

                SetFilesBusy(false);

                if (!IsDisposed && !Disposing) progressBar_files.Value = 0;
            }
        }

        private void SetFilesBusy(bool busy)
        {
            filesBusy = busy;

            //A transfer can still be in flight while the window closes; the controls it would
            //re-enable are gone by then.
            if (IsDisposed || Disposing) return;

            toolStripButton_files_up.Enabled = !busy;
            toolStripButton_files_refresh.Enabled = !busy;
            toolStripButton_files_upload.Enabled = !busy;
            toolStripButton_files_download.Enabled = !busy;
            toolStripButton_files_newfolder.Enabled = !busy;
            toolStripButton_files_delete.Enabled = !busy;
            toolStripButton_files_cancel.Enabled = busy;

            contextMenuStrip_files.Enabled = !busy;
            textBox_files_path.Enabled = !busy;

            listView_files.Cursor = busy ? Cursors.AppStarting : Cursors.Default;
        }

        private void FilesStatus(string text)
        {
            if (IsDisposed || Disposing) return;

            label_files_status.Text = text ?? string.Empty;
        }

        private void FilesProgress(long done, long total)
        {
            if (total <= 0 || IsDisposed || Disposing) return;

            long now = Environment.TickCount64;

            //Repainting the bar for every 256kB chunk costs more than the bar is worth.
            if (now - filesProgressTick < 40 && done < total) return;

            filesProgressTick = now;

            progressBar_files.Value = (int)Math.Clamp(done * 1000 / total, 0, 1000);
        }

        private List<BlobFile> SelectedEntries()
        {
            var selection = new List<BlobFile>();

            foreach (ListViewItem item in listView_files.SelectedItems)
            {
                if (item.Tag is BlobFile entry) selection.Add(entry);
            }

            return selection;
        }

        private string FilesLocationLabel() => filesPrefix.Length == 0 ? "the " + FilesStoreNoun + " root" : "\"" + filesPrefix + "\"";

        // ---------------------------------------------------------------- toolbar and menu

        private async void Button_files_refresh_Click(object sender, EventArgs e)
        {
            try
            {
                await RefreshFiles();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void Button_files_up_Click(object sender, EventArgs e)
        {
            try
            {
                if (filesPrefix.Length == 0) return;

                await NavigateFiles(ParentPrefix(filesPrefix));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void TextBox_files_path_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (e.KeyCode != Keys.Enter) return;

                e.SuppressKeyPress = true;

                await NavigateFiles(textBox_files_path.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Button_files_cancel_Click(object sender, EventArgs e)
        {
            filesCts?.Cancel();
        }

        private async void Button_files_upload_Click(object sender, EventArgs e)
        {
            try
            {
                using var dialog = new OpenFileDialog
                {
                    Multiselect = true,
                    Title = "Upload to " + FilesLocationLabel()
                };

                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                await UploadPaths(dialog.FileNames, filesPrefix);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void Button_files_download_Click(object sender, EventArgs e)
        {
            try
            {
                await DownloadSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void Button_files_newfolder_Click(object sender, EventArgs e)
        {
            try
            {
                string name = PromptForText("New folder", "Name of the new folder in " + FilesLocationLabel() + ":", string.Empty);
                if (string.IsNullOrWhiteSpace(name)) return;

                string folder = filesPrefix + name.Trim().Replace('\\', '/').Trim('/') + "/";

                await RunFilesOperation("Creating the folder", async (backend, ct) =>
                {
                    await backend.CreateFolder(folder, ct);
                    await ReloadCurrent(backend, ct);

                    FilesStatus("Created " + folder);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void Button_files_rename_Click(object sender, EventArgs e)
        {
            try
            {
                List<BlobFile> selection = SelectedEntries();

                if (selection.Count != 1)
                {
                    FilesStatus("Select exactly one item to rename.");
                    return;
                }

                BlobFile entry = selection[0];

                string name = PromptForText("Rename", "New name for \"" + entry.Name + "\":", entry.Name);

                if (string.IsNullOrWhiteSpace(name) || string.Equals(name, entry.Name, StringComparison.Ordinal)) return;

                if (name.Contains('/', StringComparison.Ordinal) || name.Contains('\\', StringComparison.Ordinal))
                {
                    FilesStatus("A name cannot contain a slash - drag the item onto a folder to move it.");
                    return;
                }

                string destination = ParentPrefix(entry.BlobPath) + name + (entry.IsFolder ? "/" : string.Empty);

                await RunFilesOperation("Rename", async (backend, ct) =>
                {
                    FilesStatus("Renaming " + entry.Name + "...");

                    await MoveOne(backend, entry, destination, ct);
                    await ReloadCurrent(backend, ct);

                    FilesStatus("Renamed to " + name + ".");
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void Button_files_delete_Click(object sender, EventArgs e)
        {
            try
            {
                List<BlobFile> selection = SelectedEntries();

                if (selection.Count == 0)
                {
                    FilesStatus("Select what to delete first.");
                    return;
                }

                bool folders = selection.Exists(entry => entry.IsFolder);

                string what = selection.Count == 1
                    ? "\"" + selection[0].Name + "\""
                    : selection.Count.ToString(CultureInfo.CurrentCulture) + " items";

                if (MessageBox.Show(
                        "Delete " + what + " from the " + FilesStoreNoun + "?"
                        + (folders ? "\n\nA folder goes with everything in it." : string.Empty)
                        + "\n\nThis cannot be undone.",
                        "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                await RunFilesOperation("Delete", async (backend, ct) =>
                {
                    var targets = new List<string>();

                    foreach (BlobFile entry in selection)
                    {
                        if (entry.IsFolder)
                        {
                            foreach (BlobFile file in await backend.BrowseTree(entry.BlobPath, ct)) targets.Add(file.BlobPath);

                            //And the folder marker itself, if this folder has one.
                            targets.Add(entry.BlobPath);
                            continue;
                        }

                        targets.Add(entry.BlobPath);
                    }

                    int done = 0;

                    foreach (string path in targets)
                    {
                        await backend.Delete(path, ct);

                        done++;
                        FilesStatus("Deleting " + done + "/" + targets.Count + "...");
                        FilesProgress(done, targets.Count);
                    }

                    await ReloadCurrent(backend, ct);

                    FilesStatus("Deleted " + what + ".");
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void Menu_files_copy_path_Click(object sender, EventArgs e)
        {
            List<BlobFile> selection = SelectedEntries();
            if (selection.Count == 0) return;

            var text = new StringBuilder();

            foreach (BlobFile entry in selection)
            {
                text.AppendLine(entry.BlobPath);
            }

            try
            {
                Clipboard.SetText(text.ToString().TrimEnd());
                FilesStatus(selection.Count == 1 ? "Path copied." : selection.Count + " paths copied.");
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                //Something else has the clipboard open. Not worth a dialog.
                FilesStatus("The clipboard is busy - try again.");
            }
        }

        // ---------------------------------------------------------------- list interaction

        private async void ListView_files_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                await OpenSelection();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void ListView_files_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                switch (e.KeyCode)
                {
                    case Keys.F5:
                        e.Handled = true;
                        await RefreshFiles();
                        break;

                    case Keys.Delete:
                        e.Handled = true;
                        Button_files_delete_Click(sender, EventArgs.Empty);
                        break;

                    case Keys.F2:
                        e.Handled = true;
                        Button_files_rename_Click(sender, EventArgs.Empty);
                        break;

                    case Keys.Back:
                        e.Handled = true;
                        if (filesPrefix.Length > 0) await NavigateFiles(ParentPrefix(filesPrefix));
                        break;

                    case Keys.Enter:
                        e.Handled = true;
                        await OpenSelection();
                        break;

                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async Task OpenSelection()
        {
            if (listView_files.SelectedItems.Count != 1) return;

            ListViewItem item = listView_files.SelectedItems[0];

            if (item.Tag is not BlobFile entry)
            {
                //The ".." row.
                await NavigateFiles(ParentPrefix(filesPrefix));
                return;
            }

            if (entry.IsFolder)
            {
                await NavigateFiles(entry.BlobPath);
                return;
            }

            await DownloadSelection();
        }

        private void ListView_files_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == filesSortColumn) filesSortDescending = !filesSortDescending;
            else
            {
                filesSortColumn = e.Column;
                filesSortDescending = false;
            }

            PopulateFilesList();
        }

        // ---------------------------------------------------------------- upload

        private Task UploadPaths(IReadOnlyList<string> localPaths, string targetPrefix)
        {
            return RunFilesOperation("Upload", async (backend, ct) =>
            {
                var plan = new List<TransferItem>();
                var emptyFolders = new List<string>();

                foreach (string path in localPaths)
                {
                    PlanUpload(path, targetPrefix, plan, emptyFolders);
                }

                if (plan.Count == 0 && emptyFolders.Count == 0)
                {
                    FilesStatus("Nothing to upload.");
                    return;
                }

                if (!await ConfirmOverwrites(backend, plan, targetPrefix, ct))
                {
                    FilesStatus("Upload cancelled.");
                    return;
                }

                long total = 0;

                foreach (TransferItem item in plan) total += item.Size;

                long done = 0;
                int index = 0;

                foreach (TransferItem item in plan)
                {
                    index++;

                    FilesStatus("Uploading " + index + "/" + plan.Count + "  " + LastSegment(item.Blob)
                        + (item.Size >= 0 ? " (" + FormatSize(item.Size) + ")" : string.Empty));

                    long before = done;

                    await backend.Upload(item.Local, item.Blob, chunk =>
                    {
                        done += chunk;
                        FilesProgress(done, total);
                    }, ct);

                    done = before + Math.Max(item.Size, 0);
                    FilesProgress(done, total);
                }

                foreach (string folder in emptyFolders)
                {
                    await backend.CreateFolder(folder, ct);
                }

                await ReloadCurrent(backend, ct);

                FilesStatus("Uploaded " + plan.Count + " file(s), " + FormatSize(total) + ", to " + FilesLocationLabel() + ".");
            });
        }

        /// <summary>
        /// Turns one dropped or picked path into the files it stands for. A folder keeps its
        /// name and its shape - what goes up mirrors what is on disk.
        /// </summary>
        private static void PlanUpload(string localPath, string targetPrefix, List<TransferItem> plan, List<string> emptyFolders)
        {
            if (Directory.Exists(localPath))
            {
                string root = targetPrefix + new DirectoryInfo(localPath).Name + "/";
                int before = plan.Count;

                foreach (string file in Directory.EnumerateFiles(localPath, "*", SearchOption.AllDirectories))
                {
                    string relative = Path.GetRelativePath(localPath, file).Replace('\\', '/');

                    plan.Add(new TransferItem { Local = file, Blob = root + relative, Size = new FileInfo(file).Length });
                }

                //An empty folder has no file to carry it into the container, so it needs the
                //marker blob - otherwise dropping one would do nothing at all.
                if (plan.Count == before) emptyFolders.Add(root);

                return;
            }

            if (!File.Exists(localPath)) return;

            var info = new FileInfo(localPath);

            plan.Add(new TransferItem { Local = localPath, Blob = targetPrefix + info.Name, Size = info.Length });
        }

        /// <summary>
        /// An upload silently replaces what is already there, so say what is about to go. One
        /// listing of the target subtree answers it for the whole batch.
        /// </summary>
        private static async Task<bool> ConfirmOverwrites(IFilesBackend backend, List<TransferItem> plan, string targetPrefix, CancellationToken ct)
        {
            if (plan.Count == 0) return true;

            var existing = new HashSet<string>(StringComparer.Ordinal);

            foreach (BlobFile file in await backend.BrowseTree(targetPrefix, ct))
            {
                existing.Add(file.BlobPath);
            }

            int clashes = 0;

            foreach (TransferItem item in plan)
            {
                if (existing.Contains(item.Blob)) clashes++;
            }

            if (clashes == 0) return true;

            return MessageBox.Show(
                clashes + " of " + plan.Count + " file(s) already exist here and will be overwritten.\n\nContinue?",
                "Upload", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
        }

        // ---------------------------------------------------------------- download

        private async Task DownloadSelection()
        {
            List<BlobFile> selection = SelectedEntries();

            if (selection.Count == 0)
            {
                FilesStatus("Select what to download first.");
                return;
            }

            //A single file gets a save dialog, so it can be renamed on the way out. Anything
            //else has a shape of its own and only needs somewhere to land.
            if (selection.Count == 1 && !selection[0].IsFolder)
            {
                BlobFile entry = selection[0];

                using var save = new SaveFileDialog
                {
                    FileName = SafeLocalName(entry.Name),
                    Filter = "All files (*.*)|*.*"
                };

                if (save.ShowDialog(this) != DialogResult.OK) return;

                string target = save.FileName;

                await RunFilesOperation("Download", async (backend, ct) =>
                {
                    var plan = new List<TransferItem>
                    {
                        new TransferItem { Blob = entry.BlobPath, Local = target, Size = Math.Max(entry.Length, 0) }
                    };

                    await RunDownloadPlan(backend, plan, ct);

                    //The listing may not know the size (DevOps) - the file on disk always does.
                    long size = entry.Length >= 0 ? entry.Length : new FileInfo(target).Length;

                    FilesStatus("Downloaded " + entry.Name + " (" + FormatSize(size) + ") to " + target);
                });

                return;
            }

            using var browse = new FolderBrowserDialog
            {
                Description = "Download " + selection.Count + " item(s) to",
                UseDescriptionForTitle = true
            };

            if (browse.ShowDialog(this) != DialogResult.OK) return;

            string directory = browse.SelectedPath;

            await RunFilesOperation("Download", async (backend, ct) =>
            {
                List<TransferItem> plan = await PlanDownload(backend, selection, directory, ct);

                await RunDownloadPlan(backend, plan, ct);

                FilesStatus("Downloaded " + plan.Count + " file(s) to " + directory);
            });
        }

        private async Task RunDownloadPlan(IFilesBackend backend, List<TransferItem> plan, CancellationToken ct)
        {
            long total = 0;

            foreach (TransferItem item in plan) total += item.Size;

            long done = 0;
            int index = 0;

            foreach (TransferItem item in plan)
            {
                index++;

                FilesStatus("Downloading " + index + "/" + plan.Count + "  " + LastSegment(item.Blob)
                    + (item.Size > 0 ? " (" + FormatSize(item.Size) + ")" : string.Empty));

                long before = done;

                await backend.Download(item.Blob, item.Local, chunk =>
                {
                    done += chunk;
                    FilesProgress(done, total);
                }, ct);

                done = before + item.Size;
                FilesProgress(done, total);
            }
        }

        /// <summary>
        /// Works out where each remote file lands on disk. A folder brings its whole subtree
        /// with the structure intact; every name is sanitised, because the store may carry names
        /// Windows will not have as a file.
        /// </summary>
        private static async Task<List<TransferItem>> PlanDownload(IFilesBackend backend, IReadOnlyList<BlobFile> entries, string targetDirectory, CancellationToken ct)
        {
            var plan = new List<TransferItem>();

            foreach (BlobFile entry in entries)
            {
                if (!entry.IsFolder)
                {
                    plan.Add(new TransferItem
                    {
                        Blob = entry.BlobPath,
                        Local = Path.Combine(targetDirectory, SafeLocalName(entry.Name)),
                        Size = Math.Max(entry.Length, 0)
                    });

                    continue;
                }

                string root = Path.Combine(targetDirectory, SafeLocalName(entry.Name));

                List<BlobFile> tree = await backend.BrowseTree(entry.BlobPath, ct);

                foreach (BlobFile file in tree)
                {
                    string relative = file.BlobPath.Substring(entry.BlobPath.Length);

                    plan.Add(new TransferItem
                    {
                        Blob = file.BlobPath,
                        Local = Path.Combine(root, SafeLocalPath(relative)),
                        Size = Math.Max(file.Length, 0)
                    });
                }

                //Nothing to download, but the folder was asked for - create it empty.
                if (tree.Count == 0) Directory.CreateDirectory(root);
            }

            return plan;
        }

        private static string SafeLocalPath(string relativeBlobPath)
        {
            string[] segments = relativeBlobPath.Split('/');

            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = SafeLocalName(segments[i]);
            }

            return string.Join(Path.DirectorySeparatorChar.ToString(), segments);
        }

        //DOS device names stay reserved in any directory, with or without an extension
        //("CON.txt" opens the console, not a file) - a blob carrying one of these would
        //abort a download batch mid-way when its turn came.
        private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// A blob name as a file name. Blob names are far freer than the file system is, and
        /// one of them is "..", which as a path segment would climb out of the chosen folder.
        /// </summary>
        private static string SafeLocalName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "unnamed";

            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(name.Length);

            foreach (char c in name)
            {
                builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }

            //A trailing dot or space is legal in a name but not on disk.
            string cleaned = builder.ToString().TrimEnd('.', ' ');

            if (cleaned.Length == 0 || cleaned == "." || cleaned == "..") return "unnamed";

            string stem = cleaned.Split('.', 2)[0];

            return ReservedDeviceNames.Contains(stem) ? "_" + cleaned : cleaned;
        }

        /// <summary>
        /// The folder a prefix sits in. The root is its own parent.
        /// </summary>
        private static string ParentPrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return string.Empty;

            string trimmed = prefix.TrimEnd('/');
            int slash = trimmed.LastIndexOf('/');

            return slash < 0 ? string.Empty : trimmed.Substring(0, slash + 1);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes.ToString(CultureInfo.CurrentCulture) + " B";

            string[] units = { "kB", "MB", "GB", "TB" };
            double value = bytes / 1024d;
            int unit = 0;

            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString(value >= 100 ? "F0" : "F1", CultureInfo.CurrentCulture) + " " + units[unit];
        }

        // ---------------------------------------------------------------- moving

        private Task MoveEntries(List<BlobFile> entries, string targetPrefix)
        {
            return RunFilesOperation("Move", async (backend, ct) =>
            {
                int moved = 0;
                int skipped = 0;

                foreach (BlobFile entry in entries)
                {
                    string destination = targetPrefix + entry.Name + (entry.IsFolder ? "/" : string.Empty);

                    if (string.Equals(destination, entry.BlobPath, StringComparison.Ordinal)) continue;

                    //A folder dropped into itself is skipped rather than fatal: the rest of a
                    //mixed selection has nothing wrong with it.
                    if (entry.IsFolder && targetPrefix.StartsWith(entry.BlobPath, StringComparison.Ordinal))
                    {
                        skipped++;
                        continue;
                    }

                    FilesStatus("Moving " + entry.Name + " to " + (targetPrefix.Length == 0 ? "the " + FilesStoreNoun + " root" : targetPrefix) + "...");

                    await MoveOne(backend, entry, destination, ct);

                    moved++;
                    FilesProgress(moved, entries.Count);
                }

                await ReloadCurrent(backend, ct);

                string where = targetPrefix.Length == 0 ? "the " + FilesStoreNoun + " root" : targetPrefix;
                string ignored = skipped == 0 ? string.Empty : "  " + skipped + " skipped - a folder cannot go inside itself.";

                FilesStatus(moved == 0
                    ? "Nothing moved." + ignored
                    : "Moved " + moved + " item(s) to " + where + "." + ignored);
            });
        }

        /// <summary>
        /// Moves one entry. On the blob backend the service does the copying, so even a large
        /// file moves in the time it takes to ask; on DevOps the content is fetched and
        /// committed under the new name - correct, but the bytes do cross this machine.
        /// </summary>
        private static async Task MoveOne(IFilesBackend backend, BlobFile entry, string destination, CancellationToken ct)
        {
            if (string.Equals(destination, entry.BlobPath, StringComparison.Ordinal)) return;

            if (!entry.IsFolder)
            {
                await backend.Copy(entry.BlobPath, destination, ct);
                await backend.Delete(entry.BlobPath, ct);

                return;
            }

            if (destination.StartsWith(entry.BlobPath, StringComparison.Ordinal))
            {
                throw new BlobFileException("A folder cannot be moved into itself.");
            }

            //A folder is only the files named after it, so moving one means moving each of them.
            List<BlobFile> tree = await backend.BrowseTree(entry.BlobPath, ct);

            foreach (BlobFile file in tree)
            {
                await backend.Copy(file.BlobPath, string.Concat(destination, file.BlobPath.AsSpan(entry.BlobPath.Length)), ct);
                await backend.Delete(file.BlobPath, ct);
            }

            if (tree.Count == 0) await backend.CreateFolder(destination, ct);

            //Whatever marker held the old folder open goes with it.
            await backend.Delete(entry.BlobPath, ct);
        }

        // ---------------------------------------------------------------- drag and drop

        private void ListView_files_ItemDrag(object sender, ItemDragEventArgs e)
        {
            if (filesBusy) return;
            if (e.Button != MouseButtons.Left) return;

            List<BlobFile> selection = SelectedEntries();
            if (selection.Count == 0) return;

            filesDragSource = selection;
            filesDragError = null;

            try
            {
                //Nothing is downloaded here. The data object fetches the blobs only if the drop
                //target actually asks for the files, so a drag that ends on a folder in this
                //list - or nowhere at all - never touches the network.
                listView_files.DoDragDrop(new DataObject(new BlobDragData(this, selection)),
                                          DragDropEffects.Copy | DragDropEffects.Move);
            }
            finally
            {
                filesDragSource = null;
                ClearDropHighlight();
            }

            if (filesDragError is not null) FilesStatus("Drag out failed: " + filesDragError);
        }

        private void ListView_files_DragEnter(object sender, DragEventArgs e) => UpdateFilesDropEffect(e);

        private void ListView_files_DragOver(object sender, DragEventArgs e) => UpdateFilesDropEffect(e);

        private void ListView_files_DragLeave(object sender, EventArgs e) => ClearDropHighlight();

        private void UpdateFilesDropEffect(DragEventArgs e)
        {
            e.Effect = DragDropEffects.None;

            if (filesBusy || !BlobConfigured(complain: false))
            {
                ClearDropHighlight();
                return;
            }

            ListViewItem target = FilesItemAt(e);

            if (filesDragSource is not null)
            {
                //Our own items. Asking the data object what it holds would make it fetch the
                //files, which is exactly what a move must not do.
                e.Effect = CanMoveTo(filesDragSource, DropTargetPrefix(target)) ? DragDropEffects.Move : DragDropEffects.None;
            }
            else if (e.Data is not null && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }

            HighlightDropTarget(e.Effect == DragDropEffects.None ? null : target);
        }

        private async void ListView_files_DragDrop(object sender, DragEventArgs e)
        {
            try
            {
                ListViewItem target = FilesItemAt(e);
                string prefix = DropTargetPrefix(target);

                ClearDropHighlight();

                if (filesDragSource is not null)
                {
                    //Taken now: the drag is over the moment this hands control back, and the
                    //move runs on after that.
                    List<BlobFile> moving = filesDragSource;

                    if (!CanMoveTo(moving, prefix)) return;

                    await MoveEntries(moving, prefix);
                    return;
                }

                if (e.Data is null || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
                if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;

                await UploadPaths(paths, prefix);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private ListViewItem FilesItemAt(DragEventArgs e)
        {
            Point point = listView_files.PointToClient(new Point(e.X, e.Y));

            return listView_files.GetItemAt(point.X, point.Y);
        }

        /// <summary>
        /// Where a drop lands: the folder it was released on, or the folder on show.
        /// </summary>
        private string DropTargetPrefix(ListViewItem item)
        {
            if (item is null) return filesPrefix;

            if (item.Tag is BlobFile entry) return entry.IsFolder ? entry.BlobPath : filesPrefix;

            //The ".." row.
            return ParentPrefix(filesPrefix);
        }

        private static bool CanMoveTo(List<BlobFile> entries, string targetPrefix)
        {
            foreach (BlobFile entry in entries)
            {
                //Already in that folder.
                if (string.Equals(ParentPrefix(entry.BlobPath), targetPrefix, StringComparison.Ordinal)) continue;

                //Into itself, or into something it contains.
                if (entry.IsFolder && targetPrefix.StartsWith(entry.BlobPath, StringComparison.Ordinal)) continue;

                return true;
            }

            return false;
        }

        private void HighlightDropTarget(ListViewItem item)
        {
            ListViewItem folder = item is not null && IsFolderItem(item) ? item : null;

            if (ReferenceEquals(filesDropTarget, folder)) return;

            ClearDropHighlight();

            if (folder is null) return;

            filesDropTarget = folder;
            folder.BackColor = SystemColors.Highlight;
            folder.ForeColor = SystemColors.HighlightText;
        }

        private void ClearDropHighlight()
        {
            if (filesDropTarget is null) return;

            filesDropTarget.BackColor = listView_files.BackColor;
            filesDropTarget.ForeColor = listView_files.ForeColor;

            filesDropTarget = null;
        }

        private static bool IsFolderItem(ListViewItem item)
            => item.Tag is null || (item.Tag is BlobFile entry && entry.IsFolder);

        /// <summary>
        /// What a drag out of the list offers the world. It carries no files until something
        /// asks for them - Explorer does that when the mouse is released, and by then the drag
        /// has already started, so a slow download cannot stop one from beginning.
        /// </summary>
        private sealed class BlobDragData : IDataObject, ITypedDataObject
        {
            private readonly Form1 owner;
            private readonly List<BlobFile> entries;

            //The paths handed out. Kept because a drop target is free to ask more than once,
            //and downloading the same blobs twice would be worse than rude.
            private string[] staged;

            public BlobDragData(Form1 owner, List<BlobFile> entries)
            {
                this.owner = owner;
                this.entries = entries;
            }

            public object GetData(string format, bool autoConvert)
            {
                if (!IsFileDrop(format)) return null;

                return staged ??= owner.StageForDrag(entries);
            }

            public object GetData(string format) => GetData(format, true);

            public object GetData(Type format) => format is null ? null : GetData(format.FullName, true);

            public bool GetDataPresent(string format, bool autoConvert) => IsFileDrop(format);

            public bool GetDataPresent(string format) => IsFileDrop(format);

            public bool GetDataPresent(Type format) => format is not null && IsFileDrop(format.FullName);

            public string[] GetFormats(bool autoConvert) => new[] { DataFormats.FileDrop };

            public string[] GetFormats() => GetFormats(true);

            public void SetData(string format, bool autoConvert, object data) => throw new NotSupportedException();

            public void SetData(string format, object data) => throw new NotSupportedException();

            public void SetData(Type format, object data) => throw new NotSupportedException();

            public void SetData(object data) => throw new NotSupportedException();

            //A drop target can also ask by type rather than by format. There is one thing on
            //offer here either way, so every one of these ends up in the same place.
            public bool TryGetData<T>(out T data) => TryGetData(DataFormats.FileDrop, autoConvert: true, out data);

            public bool TryGetData<T>(string format, out T data) => TryGetData(format, autoConvert: true, out data);

            public bool TryGetData<T>(string format, Func<TypeName, Type> resolver, bool autoConvert, out T data)
                => TryGetData(format, autoConvert, out data);

            public bool TryGetData<T>(string format, bool autoConvert, out T data)
            {
                data = default;

                if (GetData(format, autoConvert) is not T value) return false;

                data = value;
                return true;
            }

            private static bool IsFileDrop(string format) => string.Equals(format, DataFormats.FileDrop, StringComparison.Ordinal);
        }

        /// <summary>
        /// Downloads what is being dragged into a temporary folder and returns the paths.
        /// Called from inside the drag loop, with the drop target waiting on the answer.
        /// </summary>
        private string[] StageForDrag(List<BlobFile> entries)
        {
            Cursor previous = Cursor.Current;
            Cursor.Current = Cursors.WaitCursor;

            try
            {
                //Deliberately blocking - there is nothing to hand over until this finishes.
                //Task.Run keeps the continuations off the message loop we are standing in.
                return Task.Run(() => StageForDragAsync(entries)).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is BlobFileException or HttpRequestException or IOException
                                          or UnauthorizedAccessException or XmlException)
            {
                //Reported once the drag is over; a dialog cannot be shown from in here.
                filesDragError = ex.Message;
                return null;
            }
            finally
            {
                Cursor.Current = previous;
            }
        }

        private async Task<string[]> StageForDragAsync(List<BlobFile> entries)
        {
            //Its own backend, like RunFilesOperation resolves one: the drag runs outside any
            //operation, but the same "one backend per transfer" rule applies.
            IFilesBackend backend = FilesBackendForThisRun;

            //Its own folder per drag: the same file dragged out twice must not land on itself
            //while the first copy is still being read by whoever it was dropped on.
            string root = Path.Combine(FilesStagingRoot(), Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(root);

            List<TransferItem> plan = await PlanDownload(backend, entries, root, CancellationToken.None);

            foreach (TransferItem item in plan)
            {
                await backend.Download(item.Blob, item.Local, null, CancellationToken.None);
            }

            var paths = new List<string>();

            foreach (BlobFile entry in entries)
            {
                paths.Add(Path.Combine(root, SafeLocalName(entry.Name)));
            }

            return paths.ToArray();
        }

        private string FilesStagingRoot()
        {
            filesStagingRoot ??= Path.Combine(Path.GetTempPath(), "ApiTester-blob-drag", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(filesStagingRoot);

            return filesStagingRoot;
        }

        /// <summary>
        /// Throws away what was staged for dragging. Whatever it was dropped on has long since
        /// copied it; these are our temporary files, not the user's.
        /// </summary>
        private void CleanFilesStaging()
        {
            if (filesStagingRoot is null) return;

            try
            {
                if (Directory.Exists(filesStagingRoot)) Directory.Delete(filesStagingRoot, true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                //A file still open somewhere. Windows clears the temp folder eventually.
            }

            filesStagingRoot = null;
        }

        // ---------------------------------------------------------------- small dialog

        /// <summary>
        /// Asks for one line of text. WinForms has no input box and the two places that need
        /// one do not justify a form of their own.
        /// </summary>
        private string PromptForText(string title, string prompt, string initial)
        {
            using var dialog = new Form
            {
                Text = title,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                ClientSize = new Size(430, 120)
            };

            var label = new Label { Text = prompt, AutoSize = true, Location = new Point(14, 16), MaximumSize = new Size(400, 0) };
            var input = new TextBox { Text = initial ?? string.Empty, Location = new Point(16, 44), Size = new Size(398, 27) };
            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(238, 80), Size = new Size(85, 30) };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(329, 80), Size = new Size(85, 30) };

            dialog.Controls.Add(label);
            dialog.Controls.Add(input);
            dialog.Controls.Add(ok);
            dialog.Controls.Add(cancel);

            dialog.AcceptButton = ok;
            dialog.CancelButton = cancel;

            input.SelectAll();

            return dialog.ShowDialog(this) == DialogResult.OK ? input.Text.Trim() : null;
        }
    }
}
