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

        //The note a debounced save belongs to. The name and text are read from the editor when
        //the save fires, not per keystroke - turning the visual editor's content into Markdown
        //is not free. Every path that puts another note into the editor flushes first, so the
        //editor still holds this note then; the id check in the flush makes sure.
        private int pendingNoteSaveId;

        //The editor holds edits that have not been written - what "Unsaved changes" says.
        private bool noteUnsaved;

        private System.Windows.Forms.Timer noteSaveTimer;

        //A pause in typing, not a pause between words, before the edit is written.
        private const int NoteSaveDebounceMs = 3000;

        //A saved note waits longer for its sync than a session does: notes are typed in bursts
        //with pauses, and every round is a request against the store.
        private const int NoteSyncDebounceMs = 30000;

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
            SetSplitterDistance(splitContainer_notes, splitContainer_notes.LogicalToDeviceUnits(_settings.SplitterNotesDistance));

            if (_settings.NoteEditorZoom > 0) fastColoredTextBox_note.Zoom = _settings.NoteEditorZoom;

            ShowNoteEditorMode();
        }

        /// <summary>
        /// The list width as it is stored: in 96-DPI units, so the same value means the same
        /// physical width on a machine with different display scaling. Only meaningful once the
        /// tab has been opened - before that the splitter still sits where the designer put it.
        /// </summary>
        private int NotesSplitterLogicalDistance()
        {
            return (int)Math.Round(splitContainer_notes.SplitterDistance * 96.0 / splitContainer_notes.DeviceDpi);
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
            timer.Tick += async (sender, e) =>
            {
                //An exception here would end up in the global handler, which only logs it -
                //the label already says the save failed, and Save retries it with the message.
                try
                {
                    await FlushPendingNoteSave();
                }
                catch (Exception)
                {
                }
            };
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

        //Row lookup by note id. Rows are rebuilt with the grid, and the per-keystroke save
        //debounce and the new-note selection look one up by id - a linear scan per save was
        //the alternative. Entries are valid only while the grid fill that produced them is
        //current: notesGridGeneration bumps on every rebuild, and a stale entry fails the
        //generation check rather than writing into a row the grid has already dropped.
        private readonly Dictionary<int, (DataGridViewRow Row, int Generation)> noteRowById = new();
        private int notesGridGeneration;

        private void FillNotesGrid(int? reselectId)
        {
            dataGridView_notes.Rows.Clear();
            noteRowById.Clear();
            notesGridGeneration++;

            foreach (NoteRow note in allNotes)
            {
                int index = dataGridView_notes.Rows.Add(
                    note.Name,
                    NoteUpdatedDisplay(note.UpdatedUtc));

                dataGridView_notes.Rows[index].Tag = note.Id;
                noteRowById[note.Id] = (dataGridView_notes.Rows[index], notesGridGeneration);

                if (reselectId.HasValue && note.Id == reselectId.Value)
                {
                    dataGridView_notes.Rows[index].Selected = true;
                }
            }
        }

        private bool TryGetCurrentNoteRow(int noteId, out DataGridViewRow row)
        {
            row = null;

            if (!noteRowById.TryGetValue(noteId, out (DataGridViewRow Row, int Generation) entry)) return false;
            if (entry.Generation != notesGridGeneration) return false;

            row = entry.Row;
            return true;
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

            //Typing while the note was being read went into the note still on screen - write
            //it before the editor is handed over.
            await FlushPendingNoteSave();

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
                LoadNoteEditor(note?.Text ?? string.Empty);
            }
            finally
            {
                suppressNoteDirty = false;
            }

            bool enabled = note is not null;
            textBox_note_name.Enabled = enabled;
            fastColoredTextBox_note.Enabled = enabled;
            richTextBox_note.Enabled = enabled;

            foreach (ToolStripItem item in toolStrip_note_format.Items)
            {
                if (item != toolStripButton_note_markdown) item.Enabled = enabled;
            }

            noteUnsaved = false;

            //The label always speaks, not only once typing started: a freshly opened note was
            //loaded from the database, so it reads as saved. Not from note.Dirty - that flag
            //means "not synced yet", which the sync label reports; read as "unsaved" it showed
            //a Save button with nothing to save.
            if (note is null)
            {
                label_notes_save_status.Text = "No note";
                label_notes_save_status.ForeColor = System.Drawing.SystemColors.GrayText;
            }
            else
            {
                UpdateNotesStatus(saved: true);
            }
        }

        private void TextBox_note_name_TextChanged(object sender, EventArgs e)
        {
            if (suppressNoteDirty || currentNote is null) return;

            ScheduleNoteSave();
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

            ScheduleNoteSave();
        }

        /// <summary>
        /// An edit is cheaper to wait out than to save per keystroke: the timer restarts on
        /// every change and the write lands a beat after typing pauses.
        /// </summary>
        private void ScheduleNoteSave()
        {
            if (currentNote is null) return;

            pendingNoteSaveId = currentNote.Id;
            noteUnsaved = true;

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

            pendingNoteSaveId = 0;

            //The editor holds the content to save; should it ever hold another note by now,
            //writing its text into this one would be the worst outcome there is.
            Note note = currentNote;
            if (note is null || note.Id != id || note.Deleted) return;

            try
            {
                note.Name = textBox_note_name.Text;
                note.Text = NoteEditorMarkdown();

                await MarkNoteDirty(note);
            }
            catch
            {
                //The edit is still owed: re-arm it so Save - or the next keystroke - tries
                //again, instead of leaving "Unsaved changes" with nothing behind it.
                if (pendingNoteSaveId == 0) pendingNoteSaveId = id;

                label_notes_save_status.Text = "Save failed";
                label_notes_save_status.ForeColor = System.Drawing.Color.IndianRed;
                throw;
            }

            //Typing while the write was in flight has armed the next save already - this one
            //did not catch those keystrokes, so it does not get to say "Saved".
            if (pendingNoteSaveId != 0) return;

            noteUnsaved = false;
            UpdateNotesStatus(saved: true, savedAt: DateTime.Now);

            if (wysiwygSavedAsPlainText)
            {
                wysiwygSavedAsPlainText = false;

                label_notes_save_status.Text = "Saved without formatting";
                label_notes_save_status.ForeColor = System.Drawing.Color.IndianRed;
            }
        }

        private async Task MarkNoteDirty(Note note)
        {
            note.UpdatedUtc = SyncRow.NowUtc();
            note.Dirty = true;

            await sessionsConn.UpdateAsync(note);

            //Keep the grid's Updated cell honest without a full reload.
            //Keep the grid's Updated cell honest without a full reload; if the grid was
            //rebuilt since the note started editing, a reload scheduled elsewhere owns it.
            if (TryGetCurrentNoteRow(note.Id, out DataGridViewRow dirtyRow))
            {
                dirtyRow.Cells[1].Value = NoteUpdatedDisplay(note.UpdatedUtc);
            }

            RequestSync(NoteSyncDebounceMs);
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
                //Save means save: should the editor show unsaved changes that nothing is
                //waiting to write any more, re-arm them rather than doing nothing.
                if (noteUnsaved && pendingNoteSaveId == 0 && currentNote is not null) pendingNoteSaveId = currentNote.Id;

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
            string text = carryEditorContent ? NoteEditorMarkdown() : string.Empty;

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

            if (TryGetCurrentNoteRow(note.Id, out DataGridViewRow newRow))
            {
                newRow.Selected = true;
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
