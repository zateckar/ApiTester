using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        //An import loads the whole source table - bodies and all - before inserting a row of
        //it, so it needs an upper bound. Far above any realistic manual import.
        private const int MaxImportSessions = 100_000;

        private async Task LoadSettings()
        {
            List<Setting> result = new List<Setting>();
            try
            {
                await settingsConn.EnsureTableAsync<Setting>();
                result = await settingsConn.ToListAsync<Setting>();

                if (result.Count == 0)
                {
                    //First run - seed a default profile and use it.
                    _settings = new Setting
                    {
                        Endpoint = "default.sqlite",
                        ProfileName = "default",
                        Selected = true
                    };

                    await settingsConn.InsertAsync(_settings);
                    result = await settingsConn.ToListAsync<Setting>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            PopulateProfileControls(result);

            Setting selected = result.FirstOrDefault(s => s.Selected) ?? result[0];

            foreach (ProfileItem item in comboBox_settings_profiles.Items)
            {
                if (item.Id == selected.Id) { comboBox_settings_profiles.SelectedItem = item; break; }
            }

            await LoadSettingProfile(selected.Id);

            await LoadSessions();
        }

        /// <summary>
        /// DEBUG builds only: picks a PAT out of devops-pat.txt next to the executable and puts
        /// it into profiles that have none. The file is git-ignored - the token never ends up in
        /// the repository, and this convenience does not exist in release builds.
        /// </summary>
        private static void SeedDevOpsPat()
        {
            string patFile = Path.Combine(AppContext.BaseDirectory, "devops-pat.txt");

            if (!File.Exists(patFile)) return;

            string pat = File.ReadAllText(patFile).Trim();
            if (pat.Length == 0 || string.Equals(_settings.DevOpsPat, pat, StringComparison.Ordinal)) return;

            if (string.IsNullOrEmpty(_settings.DevOpsPat)) _settings.DevOpsPat = pat;
        }

        /// <summary>
        /// Rebuilds the profile combo and both "Copy to" menus from scratch.
        /// </summary>
        private void PopulateProfileControls(List<Setting> profiles)
        {
            comboBox_settings_profiles.Items.Clear();

            //These are rebuilt on every settings change - without clearing, entries accumulate.
            copyToToolStripMenuItem.DropDownItems.Clear();
            copyToToolStripMenuItem1.DropDownItems.Clear();

            foreach (Setting item in profiles)
            {
                //ProfileItem rather than the Setting itself: the combo renders items via
                //ToString(), which needs no data binding and so survives trimming.
                comboBox_settings_profiles.Items.Add(new ProfileItem(item));

                //A ToolStripItem has a single owner - adding one instance to both menus would
                //re-parent it and silently empty the first menu. Each menu needs its own item.
                copyToToolStripMenuItem.DropDownItems.Add(NewProfileMenuItem(item));
                copyToToolStripMenuItem1.DropDownItems.Add(NewProfileMenuItem(item));
            }
        }

        /// <summary>
        /// A profile as the combo box sees it. Rendered through ToString rather than
        /// DisplayMember, which would go through the unavailable binding stack when trimmed.
        /// </summary>
        private sealed class ProfileItem
        {
            public ProfileItem(Setting profile)
            {
                Id = profile.Id;
                Name = profile.ProfileName;
            }

            public int Id { get; }
            public string Name { get; }

            public override string ToString() => Name ?? string.Empty;
        }

        private static ToolStripMenuItem NewProfileMenuItem(Setting profile)
        {
            return new ToolStripMenuItem
            {
                Text = profile.ProfileName,
                Name = profile.Id.ToString(CultureInfo.InvariantCulture)
            };
        }

        private async Task LoadSettingProfile(int Id)
        {
            Setting profile;

            try
            {
                profile = await settingsConn.FindAsync<Setting>(Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return;
            }

            if (profile is null) return;

            _settings = profile;

#if DEBUG
            SeedDevOpsPat();
#endif

            textBox_cosmos_Endpoint.Text = _settings.Endpoint;
            textBox_profileName.Text = _settings.ProfileName;
            textBox_blob_sas_token.Text = _settings.BlobSASToken;
            textBox_blob_container.Text = _settings.BlobContainer;
            textBox_blob_storage_account.Text = _settings.BlobStorageAccount;
            textBox_blob_encryption_key.Text = _settings.BlobEncryptionKey;
            textBox_blob_account_key.Text = _settings.BlobAccountKey;
            comboBox_sync_target.SelectedIndex = _settings.SyncWithDevOps ? 1 : 0;
            ApplySyncTargetEnablement();
            textBox_devops_repo.Text = _settings.DevOpsRepo;
            textBox_devops_pat.Text = _settings.DevOpsPat;
            textBox_devops_branch.Text = _settings.DevOpsBranch;

            //Setting Checked fires CheckedChanged; RefreshGrid on an empty grid is harmless.
            checkBox_group_url.Checked = _settings.GroupByUrl;

            //The column widths belong to the profile - applied once here, not from RefreshGrid,
            //or every repaint would snap a manual resize back to the saved value. Note is the
            //fill column and derives its width from what is left, so it is neither saved nor
            //applied.
            dataGridView1.Columns["DateTime"].Width = _settings.DataGridViewDateTimeWidth;
            dataGridView1.Columns["UriHost"].Width = _settings.DataGridViewCol3Width;
            dataGridView1.Columns["UriAbsolutePath"].Width = _settings.DataGridViewPathWidth;
            dataGridView1.Columns["ResponseStatusCode"].Width = _settings.DataGridViewStatusCodeWidth;

            //Swap first, close after: a store holds its database file open until closed, so
            //keeping the reference without closing it would leak the old profile's connection
            //(and the lock on its file) on every switch.
            SqliteStore previousStore = sessionsConn;
            sessionsConn = new SqliteStore(_settings.Endpoint);

            //A sync still running against the previous profile's database must not finish
            //against this one, and none of what it learned carries over.
            syncGeneration++;
            syncInstanceId = null;
            syncPulledThisRun = false;
            DiscardKey();

            //The generation bump above turns away any round that was still working on the
            //previous store; closing it cannot cut one off mid-write.
            if (previousStore is not null) await previousStore.CloseAsync();

            //New credentials invalidate whatever the last round failed on - give the sync a
            //clean start against this profile's container.
            syncBlocked = false;

            //The remote store belongs to the profile, so what the Files tab is showing does
            //not survive a switch to another one.
            ResetFilesTab();

            //Same for the notes: the listing came out of the previous profile's database and
            //the editor's debounce may still hold its text. Drop both before the new profile's
            //grid loads.
            ResetNotesTab();

            ApplySavedSplitterData();
        }


        private async void TabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (_settings.Id == 0) return;

                await MarkProfileSelected();

                if (tabControl2.SelectedTab == tabPage3)
                {
                    await LoadSessions();
                }

                //Listed on first sight rather than on every start - a container the user never
                //opens should cost nothing.
                if (tabControl2.SelectedTab == tabPage_files && !filesLoaded)
                {
                    await NavigateFiles(filesPrefix);
                }

                if (tabControl2.SelectedTab == tabPage_notes && !notesLoaded)
                {
                    await LoadNotes();
                }

                //Leaving the Notes tab flushes whatever the editor's debounce still holds.
                if (tabControl2.SelectedTab != tabPage_notes)
                {
                    await FlushNotesOnLeave();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void ComboBox_settings_profiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (comboBox_settings_profiles.SelectedItem is ProfileItem selected)
                {
                    await LoadSettingProfile(selected.Id);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void ReadProfileInto(Setting profile)
        {
            profile.Endpoint = textBox_cosmos_Endpoint.Text;
            profile.ProfileName = textBox_profileName.Text;
            profile.BlobSASToken = textBox_blob_sas_token.Text;
            profile.BlobContainer = textBox_blob_container.Text;
            profile.BlobStorageAccount = textBox_blob_storage_account.Text;
            profile.BlobEncryptionKey = textBox_blob_encryption_key.Text;
            profile.BlobAccountKey = textBox_blob_account_key.Text;
            profile.SyncWithDevOps = comboBox_sync_target.SelectedIndex == 1;
            profile.DevOpsRepo = textBox_devops_repo.Text.Trim();
            profile.DevOpsPat = textBox_devops_pat.Text;
            profile.DevOpsBranch = string.IsNullOrWhiteSpace(textBox_devops_branch.Text) ? "main" : textBox_devops_branch.Text.Trim();
        }

        private void ComboBox_sync_target_SelectedIndexChanged(object sender, EventArgs e)
            => ApplySyncTargetEnablement();

        /// <summary>
        /// The combination of two backends and one encryption setting makes a free-for-all
        /// unclear about what applies where; greying out the section the selection does not
        /// use is what makes it self-describing.
        /// </summary>
        private void ApplySyncTargetEnablement()
        {
            bool devOps = comboBox_sync_target.SelectedIndex == 1;

            foreach (Control control in new Control[]
            {
                label5, textBox_blob_sas_token,
                label1, textBox_blob_storage_account,
                label4, textBox_blob_container,
                label_blob_account_key, textBox_blob_account_key
            })
            {
                control.Enabled = !devOps;
            }

            foreach (Control control in new Control[]
            {
                label_devops_repo, textBox_devops_repo,
                label_devops_pat, textBox_devops_pat,
                label_devops_branch, textBox_devops_branch
            })
            {
                control.Enabled = devOps;
            }
        }

        /// <summary>
        /// Records the current profile as the one to come back to. Selected is a single-row flag,
        /// so setting it means clearing it everywhere else first.
        /// </summary>
        private async Task MarkProfileSelected()
        {
            if (_settings.Id == 0) return;

            await settingsConn.ExecuteAsync("update Setting set Selected = 0");

            _settings.Selected = true;
            await settingsConn.UpdateAsync(_settings);
        }

        private async void button_settings_save_Click(object sender, EventArgs e)
        {
            try
            {
                //Without a row to write to, UpdateAsync would match nothing and the edits would
                //disappear on the reload below without a word.
                if (_settings.Id == 0)
                {
                    MessageBox.Show("There is no profile to update. Use \"Add as new\" to create one.");
                    return;
                }

                ReadProfileInto(_settings);

                await settingsConn.UpdateAsync(_settings);
                await LoadSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void button_settings_insert_Click(object sender, EventArgs e)
        {
            try
            {
                //Built to one side rather than into _settings: reusing that would carry the
                //existing primary key and update the current profile instead, and a failed
                //insert would leave the form editing a profile that does not exist.
                var created = new Setting();
                ReadProfileInto(created);

                await settingsConn.InsertAsync(created);

                //The new profile becomes the current one. Without this, LoadSettings would go
                //back to whichever row still carries Selected - and the form would show that
                //profile's values while the user believes they are editing the new one.
                _settings = created;
                await MarkProfileSelected();

                await LoadSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void button_settings_delete_Click(object sender, EventArgs e)
        {
            try
            {
                if (_settings.Id == 0) return;

                if (MessageBox.Show("Delete profile \"" + _settings.ProfileName + "\"?", "Delete profile",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

                await settingsConn.DeleteAsync<Setting>(_settings.Id);

                //_settings still points at the deleted row - LoadSettings picks a surviving profile.
                _settings = new Setting();

                await LoadSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// How many sessions the active database holds.
        /// </summary>
        /// <returns>-1 when it cannot say. A database written before the sync has no Deleted
        /// column, and one that has never been used has no Session table at all - neither is a
        /// reason to refuse to copy the file, only to leave a number out of the message.</returns>
        private async Task<int> CountSessions()
        {
            string[] attempts =
            {
                "select count(*) from Session where Deleted = 0",
                "select count(*) from Session"
            };

            foreach (string sql in attempts)
            {
                try
                {
                    return await sessionsConn.ScalarIntAsync(sql);
                }
                catch (SqliteException)
                {
                }
            }

            return -1;
        }

        /// <summary>
        /// Writes a copy of the active session database to a location the user picks.
        /// </summary>
        private async void button_settings_export_Click(object sender, EventArgs e)
        {
            try
            {
                //Full path for the same reason as in SqliteStore: an earlier OpenFileDialog
                //may have moved the current directory since the profile stored its relative
                //endpoint.
                var source = new FileInfo(Path.GetFullPath(_settings.Endpoint));
                if (!source.Exists)
                {
                    MessageBox.Show("There is no session database at \"" + source.FullName + "\" yet.");
                    return;
                }

                using var saveFileDialog = new SaveFileDialog
                {
                    Filter = "SQLite database (*.sqlite)|*.sqlite|All files (*.*)|*.*",
                    FileName = "export-" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + ".sqlite"
                };

                if (saveFileDialog.ShowDialog() != DialogResult.OK) return;

                //A sync round writes sessions one at a time; a copy taken in the middle of one
                //catches the database between two of them.
                if (!await SuspendSync())
                {
                    MessageBox.Show("A sync is still running. Try the export again in a moment.");
                    return;
                }

                try
                {
                    //Counted from the database rather than from the grid, which shows the profile
                    //it was last loaded for and may not be this one.
                    int exported = await CountSessions();

                    //Flush and release the connection so the file on disk is complete.
                    await sessionsConn.CloseAsync();
                    File.Copy(source.FullName, saveFileDialog.FileName, true);
                    sessionsConn = new SqliteStore(_settings.Endpoint);

                    MessageBox.Show((exported < 0
                            ? "Exported the session database to:\n"
                            : "Exported " + exported.ToString(CultureInfo.CurrentCulture) + " sessions to:\n")
                        + saveFileDialog.FileName);
                }
                finally
                {
                    ResumeSync();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
            {
                MessageBox.Show("Export failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Appends the sessions from another database file into the active profile.
        /// </summary>
        private async void button_settings_import_Click(object sender, EventArgs e)
        {
            try
            {
                using var openFileDialog = new OpenFileDialog
                {
                    Filter = "SQLite database (*.sqlite)|*.sqlite|All files (*.*)|*.*"
                };

                if (openFileDialog.ShowDialog() != DialogResult.OK) return;

                //Inserting a few thousand sessions while a sync round is reading and writing the
                //same table is asking for trouble; it waits its turn.
                if (!await SuspendSync())
                {
                    MessageBox.Show("A sync is still running. Try the import again in a moment.");
                    return;
                }

                CursorWait(true);

                try
                {
                    var source = new SqliteStore(openFileDialog.FileName);
                    List<Session> imported;

                    try
                    {
                        //Refuse a database too large to hold in memory at once - the list
                        //below carries every row, response bodies included.
                        int available = await source.ScalarIntAsync("select count(*) from Session");

                        if (available > MaxImportSessions)
                        {
                            CursorWait(false);
                            MessageBox.Show("The selected database holds " + available.ToString(CultureInfo.CurrentCulture)
                                + " sessions; importing more than " + MaxImportSessions.ToString(CultureInfo.CurrentCulture)
                                + " at once is not supported. Export and import it in smaller parts.");
                            return;
                        }

                        imported = await source.ToListAsync<Session>();
                    }
                    finally
                    {
                        await source.CloseAsync();
                    }

                    await sessionsConn.EnsureTableAsync<Session>();

                    //Which sessions are already here. A uid identifies a session across
                    //databases, so importing the same file twice must not double every row.
                    //Case-insensitive to match the sync's own index (BlobSync.LocalUidIndex).
                    var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    foreach (object[] row in await sessionsConn.RawRowsAsync(
                                 "select Uid from Session where Uid is not null and Uid <> ''"))
                    {
                        known.Add(SqliteStore.AsString(row[0]));
                    }

                    int inserted = 0;
                    int skipped = 0;

                    foreach (Session s in imported)
                    {
                        //Add reports whether it was new, which also catches a file that holds the
                        //same session twice.
                        if (!string.IsNullOrEmpty(s.Uid) && !known.Add(s.Uid))
                        {
                            skipped++;
                            continue;
                        }

                        //Clear the primary key so each row is appended rather than colliding
                        //with an existing id in the target database.
                        s.Id = 0;

                        //An import of a database that was never synced brings no uids with it.
                        if (string.IsNullOrEmpty(s.Uid)) s.Uid = NewUid();

                        //The epoch, not "now" - the same rule the sync's own migration follows.
                        //Stamping a real time would make an untouched old session look newer
                        //than another instance's genuine edits of it.
                        if (string.IsNullOrEmpty(s.UpdatedUtc)) s.UpdatedUtc = EpochUtc;

                        s.Dirty = true;
                        s.Uploaded = false;
                        s.Deleted = false;

                        await sessionsConn.InsertAsync(s);
                        inserted++;
                    }

                    CursorWait(false);

                    MessageBox.Show("Imported " + inserted.ToString(CultureInfo.CurrentCulture) + " sessions."
                        + (skipped > 0 ? "\n\nSkipped " + skipped.ToString(CultureInfo.CurrentCulture) + " that are already in this database." : string.Empty));
                }
                finally
                {
                    ResumeSync();
                }

                await LoadSessions();
            }
            catch (Exception ex) when (ex is IOException or SqliteException)
            {
                CursorWait(false);
                MessageBox.Show("Import failed: " + ex.Message);
            }
        }
    }
}
