using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    /// <summary>
    /// The Notes tab: a pared-down mirror of the Sessions tab. The grid lists the notes in the
    /// local database; the editor on the right edits the selected one. Edits are written back
    /// on a short debounce and ride the ordinary sync round - see docs/blob-sync.md.
    /// </summary>
    public partial class Form1 : Form
    {
        /// <summary>One grid row's worth of a note - the list projection, without the body.</summary>
        private sealed class NoteRow
        {
            public int Id { get; init; }
            public string Name { get; set; }
            public string UpdatedUtc { get; set; }
        }

        //Text stays out of the row model - a note body is read when the note is opened, not
        //for every row on display.
        //ISO-8601 UTC sorts as a string; ascending puts the most recently edited at the
        //bottom, where the eye lands when scrolling the latest work.
        private const string NoteListProjection =
            "select Id, Name, UpdatedUtc from Note where Deleted = 0 order by UpdatedUtc, Id";

        private List<NoteRow> allNotes = new();

        //The note currently in the editor, with its Text loaded.
        private Note currentNote;

        //Set while the editor is being filled from a note - otherwise TextChanged of that very
        //assignment would mark the just-pulled note dirty again.
        private bool suppressNoteDirty;

        //The notes are listed on first sight of the tab, not on every start - mirroring how
        //the Files tab waits for filesLoaded.
        private bool notesLoaded;

        //The grid can be repopulated while the editor holds unsaved text (a sync pull lands
        //behind the scenes): the debounced save keeps the id it belongs to rather than
        //trusting currentNote, which the reload may have replaced.
        private int pendingNoteSaveId;
        private string pendingNoteName;
        private string pendingNoteText;

        private System.Windows.Forms.Timer noteSaveTimer;

        private const int NoteSaveDebounceMs = 750;

        /// <summary>
        /// Profile switch analogue of ResetFilesTab: the loaded listing and any half-debounced
        /// edit belong to the previous profile's database and must not be saved into the new
        /// one. Saves are discarded rather than flushed - the flush would already run against
        /// the new connection.
        /// </summary>
        private void ResetNotesTab()
        {
            if (!notesLoaded) return;

            noteSaveTimer?.Stop();
            pendingNoteSaveId = 0;

            notesLoaded = false;
            allNotes = new List<NoteRow>();
            currentNote = null;

            dataGridView_notes.Rows.Clear();
            SetNoteEditor(null);
        }

        /// <summary>
        /// First open of the Notes tab: create the table when missing and populate the grid.
        /// Called again after a profile switch, when notesLoaded has been reset.
        /// </summary>
        private async Task LoadNotes()
        {
            if (sessionsConn is null) return;

            //Cheap after the first run - existing databases predate the table.
            await sessionsConn.EnsureTableAsync<Note>();

            noteSaveTimer ??= NewNoteSaveTimer();

            ApplyNotesSettings();

            await ReloadNotesGridAsync();

            notesLoaded = true;
        }

        /// <summary>
        /// Per-profile view state: the saved splitter position and editor zoom.
        /// </summary>
        private void ApplyNotesSettings()
        {
            SetSplitterDistance(splitContainer_notes, _settings.SplitterNotesDistance);

            if (_settings.NoteEditorZoom > 0) fastColoredTextBox_note.Zoom = _settings.NoteEditorZoom;
        }

        private void FastColoredTextBox_note_ZoomChanged(object sender, EventArgs e)
        {
            //Zoom is plain view state - persisted silently, saved with the rest of the profile
            //on close.
            _settings.NoteEditorZoom = fastColoredTextBox_note.Zoom;
        }

        private System.Windows.Forms.Timer NewNoteSaveTimer()
        {
            var timer = new System.Windows.Forms.Timer { Interval = NoteSaveDebounceMs };
            timer.Tick += async (sender, e) => await FlushPendingNoteSave();
            return timer;
        }

        /// <summary>
        /// Sync-pull hook. Runs on the UI thread like every other grid repaint; the caller in
        /// BlobSync checks the edit-mode guard before reaching here.
        /// </summary>
        private void ReloadNotesGrid()
        {
            if (!notesLoaded || sessionsConn is null) return;

            _ = ReloadNotesGridAsync();
        }

        private async Task ReloadNotesGridAsync()
        {
            int? reselect = currentNote?.Id;

            await FlushPendingNoteSave();

            var rows = await sessionsConn.RawRowsAsync(NoteListProjection);

            allNotes = new List<NoteRow>(rows.Count);

            foreach (object[] values in rows)
            {
                allNotes.Add(new NoteRow
                {
                    Id = SqliteStore.AsInt(values[0]),
                    Name = SqliteStore.AsString(values[1]),
                    UpdatedUtc = SqliteStore.AsString(values[2])
                });
            }

            FillNotesGrid(reselect);
        }

        private void FillNotesGrid(int? reselectId)
        {
            dataGridView_notes.Rows.Clear();

            foreach (NoteRow note in allNotes)
            {
                int index = dataGridView_notes.Rows.Add(
                    note.Name,
                    NoteUpdatedDisplay(note.UpdatedUtc));

                dataGridView_notes.Rows[index].Tag = note.Id;

                if (reselectId.HasValue && note.Id == reselectId.Value)
                {
                    dataGridView_notes.Rows[index].Selected = true;
                }
            }
        }

        private static string NoteUpdatedDisplay(string updatedUtc)
        {
            DateTime parsed = SyncRow.ParseUtc(updatedUtc);

            return parsed == DateTime.MinValue
                ? string.Empty
                : parsed.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        }

        private async void DataGridView_notes_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                await DisplaySelectedNote();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Brings the grid's selected note into the editor. Whatever the editor held is saved
        /// first - a selection change is exactly where a debounced save must not lag behind.
        /// </summary>
        private async Task DisplaySelectedNote()
        {
            if (sessionsConn is null) return;

            await FlushPendingNoteSave();

            if (dataGridView_notes.SelectedRows.Count == 0
                || dataGridView_notes.SelectedRows[0].Tag is not int id)
            {
                SetNoteEditor(null);
                return;
            }

            //Already showing it - selection events also fire on repaint.
            if (currentNote is not null && currentNote.Id == id) return;

            Note note = await sessionsConn.FindAsync<Note>(id);
            if (note is null || note.Deleted)
            {
                SetNoteEditor(null);
                return;
            }

            SetNoteEditor(note);
        }

        private void SetNoteEditor(Note note)
        {
            currentNote = note;

            suppressNoteDirty = true;

            try
            {
                textBox_note_name.Text = note?.Name ?? string.Empty;
                fastColoredTextBox_note.Text = note?.Text ?? string.Empty;

                //Text assignment drops the caret at the end with everything between 0 and it
                //selected; land the caret at the top instead, with nothing marked.
                fastColoredTextBox_note.SelectionStart = 0;
                fastColoredTextBox_note.SelectionLength = 0;
                fastColoredTextBox_note.DoCaretVisible();
            }
            finally
            {
                suppressNoteDirty = false;
            }

            bool enabled = note is not null;
            textBox_note_name.Enabled = enabled;
            fastColoredTextBox_note.Enabled = enabled;

            //The label always speaks, not only once typing started: a freshly opened note was
            //loaded from the database, so it reads as saved.
            if (note is null)
            {
                label_notes_save_status.Text = "No note";
                label_notes_save_status.ForeColor = System.Drawing.SystemColors.GrayText;
            }
            else
            {
                UpdateNotesStatus(saved: !note.Dirty);
            }
        }

        private void TextBox_note_name_TextChanged(object sender, EventArgs e)
        {
            if (suppressNoteDirty || currentNote is null) return;

            ScheduleNoteSave(textBox_note_name.Text, fastColoredTextBox_note.Text);
        }

        /// <summary>
        /// Plain statement of where the editor stands: unsaved holds orange, saved green. The
        /// timestamp is only shown for a save this window made - a loaded row's UpdatedUtc is
        /// the last edit, whose save already happened elsewhere.
        /// </summary>
        private void UpdateNotesStatus(bool saved, DateTime? savedAt = null)
        {
            if (saved)
            {
                label_notes_save_status.Text = savedAt.HasValue
                    ? "Saved " + savedAt.Value.ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                    : "Saved";
                label_notes_save_status.ForeColor = System.Drawing.Color.ForestGreen;
            }
            else
            {
                label_notes_save_status.Text = "Unsaved changes";
                label_notes_save_status.ForeColor = System.Drawing.Color.DarkOrange;
            }
        }

        /// <summary>
        /// Called from the sync's SetSyncStatus. Kept separate from the save state: sync reports
        /// only that it ran (or the count it still owes), and a failure turns the label red.
        /// </summary>
        private void UpdateNotesSyncStatus(string text, string tooltip)
        {
            if (label_notes_sync_status is null || IsDisposed || Disposing) return;

            label_notes_sync_status.Text = text ?? string.Empty;
            label_notes_sync_status.ForeColor = string.IsNullOrEmpty(tooltip)
                ? System.Drawing.Color.SteelBlue
                : System.Drawing.Color.IndianRed;
        }

        private void FastColoredTextBox_note_TextChanged(object sender, FastColoredTextBoxNS.TextChangedEventArgs e)
        {
            if (suppressNoteDirty || currentNote is null) return;

            ScheduleNoteSave(textBox_note_name.Text, fastColoredTextBox_note.Text);
        }

        /// <summary>
        /// An edit is cheaper to wait out than to save per keystroke: the timer restarts on
        /// every change and the write lands a beat after typing pauses.
        /// </summary>
        private void ScheduleNoteSave(string name, string text)
        {
            if (currentNote is null) return;

            pendingNoteSaveId = currentNote.Id;
            pendingNoteName = name;
            pendingNoteText = text;

            UpdateNotesStatus(saved: false);

            noteSaveTimer?.Stop();
            noteSaveTimer?.Start();
        }

        /// <summary>
        /// Writes whatever the editor last reported, if anything is waiting. Called by the
        /// debounce timer and synchronously wherever the edited note is about to be replaced.
        /// </summary>
        private async Task FlushPendingNoteSave()
        {
            noteSaveTimer?.Stop();

            if (pendingNoteSaveId == 0 || sessionsConn is null) return;

            int id = pendingNoteSaveId;
            string name = pendingNoteName;
            string text = pendingNoteText;

            pendingNoteSaveId = 0;

            Note note = currentNote is not null && currentNote.Id == id
                ? currentNote
                : await sessionsConn.FindAsync<Note>(id);

            if (note is null || note.Deleted) return;

            note.Name = name;
            note.Text = text;

            await MarkNoteDirty(note);

            UpdateNotesStatus(saved: true, savedAt: DateTime.Now);
        }

        private async Task MarkNoteDirty(Note note)
        {
            note.UpdatedUtc = SyncRow.NowUtc();
            note.Dirty = true;

            await sessionsConn.UpdateAsync(note);

            //Keep the grid's Updated cell honest without a full reload.
            foreach (DataGridViewRow row in dataGridView_notes.Rows)
            {
                if (row.Tag is int rowId && rowId == note.Id)
                {
                    row.Cells[1].Value = NoteUpdatedDisplay(note.UpdatedUtc);
                    break;
                }
            }

            RequestSync();
        }

        private async void MenuItem_notes_new_Click(object sender, EventArgs e)
        {
            try
            {
                await NewNote(carryEditorContent: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void Button_notes_save_Click(object sender, EventArgs e)
        {
            try
            {
                await FlushPendingNoteSave();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Creates a note. When invoked from the context menu or the New button, the editor's
        /// current name/text are carried over - "New" reads as "save what I have as a note"
        /// rather than as throwing it away.
        /// </summary>
        private async Task NewNote(bool carryEditorContent)
        {
            if (sessionsConn is null) return;

            //Do not flush first: whatever the debounce holds is exactly what the new note
            //should contain.
            noteSaveTimer?.Stop();
            pendingNoteSaveId = 0;

            string name = carryEditorContent ? textBox_note_name.Text : "New note";
            string text = carryEditorContent ? fastColoredTextBox_note.Text : string.Empty;

            if (string.IsNullOrWhiteSpace(name)) name = "New note";

            //No content-derived uid as with sessions - a note has no pre-sync history, and two
            //empty notes must not collide. Fresh GUID it is.
            var note = new Note
            {
                Uid = NewUid(),
                Name = name,
                Text = text,
                CreatedUtc = SyncRow.NowUtc(),
                UpdatedUtc = SyncRow.NowUtc(),
                Dirty = true,
                Uploaded = false,
                Deleted = false
            };

            await sessionsConn.InsertAsync(note);

            await ReloadNotesGridAsync();

            foreach (DataGridViewRow row in dataGridView_notes.Rows)
            {
                if (row.Tag is int id && id == note.Id)
                {
                    row.Selected = true;
                    break;
                }
            }

            textBox_note_name.Focus();
            textBox_note_name.SelectAll();

            RequestSync();
        }

        private async void MenuItem_notes_delete_Click(object sender, EventArgs e)
        {
            try
            {
                await DeleteSelectedNote();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private async void DataGridView_notes_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Delete) return;

            e.Handled = true;

            try
            {
                await DeleteSelectedNote();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Soft-delete: the row stays until the tombstone is published and the push removes it.
        /// A note never published has no blob to hunt down - it can simply go.
        /// </summary>
        private async Task DeleteSelectedNote()
        {
            if (sessionsConn is null) return;

            await FlushPendingNoteSave();

            if (dataGridView_notes.SelectedRows.Count == 0
                || dataGridView_notes.SelectedRows[0].Tag is not int id) return;

            Note note = await sessionsConn.FindAsync<Note>(id);
            if (note is null) return;

            if (note.Uploaded)
            {
                note.Deleted = true;
                note.Dirty = true;
                note.UpdatedUtc = SyncRow.NowUtc();

                await sessionsConn.UpdateAsync(note);
            }
            else
            {
                await sessionsConn.DeleteAsync<Note>(note.Id);
            }

            SetNoteEditor(null);
            await ReloadNotesGridAsync();

            RequestSync();
        }

        /// <summary>
        /// Tab switch and form-close path: whatever the debounce still holds is written out
        /// before the editor's context goes away.
        /// </summary>
        private async Task FlushNotesOnLeave()
        {
            if (!notesLoaded) return;

            await FlushPendingNoteSave();
        }
    }
}
