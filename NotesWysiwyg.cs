using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace ApiTester
{
    /// <summary>
    /// The Notes tab's visual editor: a RichTextBox showing the note's Markdown rendered, with
    /// the formatting the toolbar and shortcuts apply. The note itself stays Markdown -
    /// NoteMarkdown translates in both directions. The Markdown button swaps in the plain
    /// source editor.
    /// </summary>
    public partial class Form1 : Form
    {
        private static bool NoteMarkdownMode => _settings?.NoteEditorMarkdown ?? false;

        //The Markdown the visual editor was last filled from. Until something is edited there
        //this is what a save writes back - running it through the editor would respell it
        //(__bold__ as **bold**) for no reason.
        private string wysiwygSource = string.Empty;
        private bool wysiwygEdited;

        //Set when the editor's formatting could not be read back and the note was saved as
        //its bare text, so the status label can say so.
        private bool wysiwygSavedAsPlainText;

        /// <summary>
        /// A RichTextBox comes without a context menu. Paste goes through the same plain-text
        /// path as Ctrl+V.
        /// </summary>
        private void SetupNotesEditor()
        {
            var menu = new ContextMenuStrip(components);

            var cut = new ToolStripMenuItem("Cut", null, (sender, e) => richTextBox_note.Cut());
            var copy = new ToolStripMenuItem("Copy", null, (sender, e) => richTextBox_note.Copy());
            var paste = new ToolStripMenuItem("Paste", null, (sender, e) => PasteNotePlainText());
            var selectAll = new ToolStripMenuItem("Select all", null, (sender, e) => richTextBox_note.SelectAll());

            menu.Items.AddRange(new ToolStripItem[] { cut, copy, paste, new ToolStripSeparator(), selectAll });

            menu.Opening += (sender, e) =>
            {
                bool selected = richTextBox_note.SelectionLength > 0;
                cut.Enabled = selected && currentNote is not null;
                copy.Enabled = selected;
                paste.Enabled = currentNote is not null && Clipboard.ContainsText();
            };

            richTextBox_note.ContextMenuStrip = menu;

            //Ctrl+wheel zooms inside RichEdit, which says nothing about it. Read the result once
            //the wheel message has been handled; like the Markdown editor's zoom it is plain view
            //state, saved with the profile on close.
            richTextBox_note.MouseWheel += (sender, e) =>
            {
                if ((ModifierKeys & Keys.Control) == 0) return;

                BeginInvoke(new Action(() =>
                {
                    if (_settings is not null && !richTextBox_note.IsDisposed) _settings.NoteVisualZoom = RichEditFormat.GetZoom(richTextBox_note);
                }));
            };

            //F5 stamps the date in either editor, as it does in Notepad.
            fastColoredTextBox_note.KeyDown += (sender, e) =>
            {
                if (e.KeyCode != Keys.F5 || e.Modifiers != Keys.None) return;

                InsertNoteDateTime();
                e.Handled = true;
                e.SuppressKeyPress = true;
            };
        }

        private void ToolStripButton_note_date_Click(object sender, EventArgs e) => InsertNoteDateTime();

        /// <summary>
        /// Puts the current local date and time at the caret, replacing any selection.
        /// Year first: it reads the same to anyone and sorts as text.
        /// </summary>
        private void InsertNoteDateTime()
        {
            if (currentNote is null) return;

            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

            if (NoteMarkdownMode)
            {
                fastColoredTextBox_note.InsertText(stamp);
                fastColoredTextBox_note.Focus();
            }
            else
            {
                richTextBox_note.SelectedText = stamp;
                richTextBox_note.Focus();
            }
        }

        private static void NoteFormatButton(ToolStripButton button, string text, string tooltip, FontStyle style)
        {
            button.DisplayStyle = ToolStripItemDisplayStyle.Text;
            button.Font = new Font("Segoe UI", 9F, style);
            button.Text = text;
            button.ToolTipText = tooltip;
        }

        // ---------------------------------------------------------------- content

        /// <summary>Fills whichever editor is active. The caller suppresses dirty-marking.</summary>
        private void LoadNoteEditor(string markdown)
        {
            if (NoteMarkdownMode)
            {
                fastColoredTextBox_note.Text = markdown;

                //Text assignment drops the caret at the end with everything between 0 and it
                //selected; land the caret at the top instead, with nothing marked.
                fastColoredTextBox_note.SelectionStart = 0;
                fastColoredTextBox_note.SelectionLength = 0;
                fastColoredTextBox_note.DoCaretVisible();
            }
            else
            {
                LoadWysiwyg(markdown);
            }
        }

        private void LoadWysiwyg(string markdown)
        {
            wysiwygSource = markdown ?? string.Empty;
            wysiwygEdited = false;

            richTextBox_note.Rtf = NoteMarkdown.ToRtf(NoteMarkdown.Parse(wysiwygSource));

            //New content resets RichEdit's zoom.
            RichEditFormat.SetZoom(richTextBox_note, _settings?.NoteVisualZoom ?? 100);

            //Undo must not reach back into the previous note.
            richTextBox_note.ClearUndo();
            richTextBox_note.Select(0, 0);
        }

        /// <summary>The note as Markdown, from whichever editor holds it.</summary>
        private string NoteEditorMarkdown()
        {
            if (NoteMarkdownMode) return fastColoredTextBox_note.Text;
            if (!wysiwygEdited) return wysiwygSource;

            var lines = NoteMarkdown.FromRtf(richTextBox_note.Rtf);
            string visible = RichEditFormat.VisibleText(richTextBox_note);

            //The reading of the RTF is checked against the text the box shows. Should the two
            //ever disagree, the text is what must survive: it is saved as is, without the
            //formatting, rather than saving a reading that dropped or garbled characters.
            if (NoteMarkdown.PlainText(lines) == visible) return NoteMarkdown.Serialize(lines);

            wysiwygSavedAsPlainText = true;
            return visible.Replace("\n", NoteMarkdown.Eol);
        }

        private void RichTextBox_note_TextChanged(object sender, EventArgs e)
        {
            if (suppressNoteDirty || currentNote is null) return;

            NoteWysiwygEdited();
        }

        private void NoteWysiwygEdited()
        {
            if (currentNote is null) return;

            wysiwygEdited = true;
            ScheduleNoteSave();
        }

        // ---------------------------------------------------------------- mode

        private void ToolStripButton_note_markdown_Click(object sender, EventArgs e)
        {
            try
            {
                SetNoteMarkdownMode(toolStripButton_note_markdown.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// Moves the note across to the other editor. A save still pending is not lost: it
        /// reads whichever editor is active when it fires, and that one now holds the text.
        /// </summary>
        private void SetNoteMarkdownMode(bool markdown)
        {
            if (markdown != NoteMarkdownMode)
            {
                string text = NoteEditorMarkdown();

                _settings.NoteEditorMarkdown = markdown;

                suppressNoteDirty = true;

                try
                {
                    LoadNoteEditor(text);
                }
                finally
                {
                    suppressNoteDirty = false;
                }
            }

            ShowNoteEditorMode();

            if (currentNote is not null)
            {
                if (markdown) fastColoredTextBox_note.Focus();
                else richTextBox_note.Focus();
            }
        }

        private void ShowNoteEditorMode()
        {
            bool markdown = NoteMarkdownMode;

            fastColoredTextBox_note.Visible = markdown;
            richTextBox_note.Visible = !markdown;
            toolStripButton_note_markdown.Checked = markdown;

            foreach (ToolStripItem item in toolStrip_note_format.Items)
            {
                if (item != toolStripButton_note_markdown && item != toolStripButton_note_date) item.Visible = !markdown;
            }
        }

        // ---------------------------------------------------------------- toolbar

        private void ToolStripButton_note_h1_Click(object sender, EventArgs e) => ApplyNoteParagraphKind(NoteLineKind.Heading, 1);
        private void ToolStripButton_note_h2_Click(object sender, EventArgs e) => ApplyNoteParagraphKind(NoteLineKind.Heading, 2);
        private void ToolStripButton_note_h3_Click(object sender, EventArgs e) => ApplyNoteParagraphKind(NoteLineKind.Heading, 3);
        private void ToolStripButton_note_text_Click(object sender, EventArgs e) => ApplyNoteParagraphKind(NoteLineKind.Paragraph, 0);
        private void ToolStripButton_note_bullet_Click(object sender, EventArgs e) => ApplyNoteParagraphKind(NoteLineKind.Bullet, 0);
        private void ToolStripButton_note_quote_Click(object sender, EventArgs e) => ApplyNoteParagraphKind(NoteLineKind.Quote, 0);
        private void ToolStripButton_note_codeblock_Click(object sender, EventArgs e) => ApplyNoteParagraphKind(NoteLineKind.Code, 0);
        private void ToolStripButton_note_bold_Click(object sender, EventArgs e) => ToggleNoteInline(NoteInline.Bold);
        private void ToolStripButton_note_italic_Click(object sender, EventArgs e) => ToggleNoteInline(NoteInline.Italic);
        private void ToolStripButton_note_strike_Click(object sender, EventArgs e) => ToggleNoteInline(NoteInline.Strike);
        private void ToolStripButton_note_code_Click(object sender, EventArgs e) => ToggleNoteInline(NoteInline.Code);

        /// <summary>
        /// Bold, italic, strikethrough or inline code over the selection - on unless the whole
        /// selection already has it. With nothing selected it applies to what is typed next.
        /// </summary>
        private void ToggleNoteInline(NoteInline style)
        {
            if (currentNote is null || NoteMarkdownMode) return;

            RichEditFormat.CharFormat current = RichEditFormat.GetChar(richTextBox_note);

            switch (style)
            {
                case NoteInline.Bold:
                    RichEditFormat.SetEffect(richTextBox_note, RichEditFormat.CFM_BOLD, !current.Is(RichEditFormat.CFM_BOLD, RichEditFormat.CFE_BOLD));
                    break;
                case NoteInline.Italic:
                    RichEditFormat.SetEffect(richTextBox_note, RichEditFormat.CFM_ITALIC, !current.Is(RichEditFormat.CFM_ITALIC, RichEditFormat.CFE_ITALIC));
                    break;
                case NoteInline.Strike:
                    RichEditFormat.SetEffect(richTextBox_note, RichEditFormat.CFM_STRIKEOUT, !current.Is(RichEditFormat.CFM_STRIKEOUT, RichEditFormat.CFE_STRIKEOUT));
                    break;
                case NoteInline.Code:
                    bool code = !(current.Has(RichEditFormat.CFM_FACE) && string.Equals(current.Face, NoteMarkdown.CodeFont, StringComparison.OrdinalIgnoreCase));
                    RichEditFormat.SetChar(richTextBox_note, CodeFormat(code));
                    break;
            }

            richTextBox_note.Focus();
            NoteWysiwygEdited();
        }

        private static RichEditFormat.CharFormat CodeFormat(bool code)
        {
            var format = new RichEditFormat.CharFormat
            {
                dwMask = RichEditFormat.CFM_FACE | RichEditFormat.CFM_BACKCOLOR,
                dwEffects = code ? 0 : RichEditFormat.CFE_AUTOBACKCOLOR,
                crBackColor = code ? RichEditFormat.ColorRef(NoteMarkdown.CodeBack) : 0
            };

            format.Face = code ? NoteMarkdown.CodeFont : NoteMarkdown.BodyFont;
            return format;
        }

        /// <summary>A paragraph of the visual editor: where it is and what kind of line it is.</summary>
        private readonly struct NoteParagraph
        {
            public int Start { get; init; }
            public int End { get; init; }
            public NoteLineKind Kind { get; init; }
            public int Level { get; init; }

            public int Length => End - Start;
        }

        /// <summary>
        /// Reads the paragraph at a position. Selects inside it to ask, so the caller restores
        /// the selection.
        /// </summary>
        private NoteParagraph NoteParagraphAt(string text, int position)
        {
            int start = position > 0 ? text.LastIndexOf('\n', position - 1) + 1 : 0;
            int end = text.IndexOf('\n', position);
            if (end < 0) end = text.Length;

            //The formatting of the paragraph's first character - or of its mark, when empty.
            richTextBox_note.Select(start, end > start ? 1 : 0);

            RichEditFormat.ParaFormat para = RichEditFormat.GetPara(richTextBox_note);
            RichEditFormat.CharFormat chars = RichEditFormat.GetChar(richTextBox_note);

            int size = chars.Has(RichEditFormat.CFM_SIZE) ? chars.HalfPoints : NoteMarkdown.BodySize;
            NoteLineKind kind = NoteMarkdown.KindFromFormat(para.wNumbering != 0, para.LeftIndent, size, out int level);

            return new NoteParagraph { Start = start, End = end, Kind = kind, Level = level };
        }

        /// <summary>
        /// Turns every paragraph the selection touches into a heading, bullet, quote, code or
        /// normal text - or back to normal text, when they all already are that.
        /// </summary>
        private void ApplyNoteParagraphKind(NoteLineKind kind, int level)
        {
            if (currentNote is null || NoteMarkdownMode) return;

            int selectionStart = richTextBox_note.SelectionStart;
            int selectionLength = richTextBox_note.SelectionLength;
            string text = richTextBox_note.Text;

            var paragraphs = new System.Collections.Generic.List<NoteParagraph>();
            int last = selectionStart + Math.Max(0, selectionLength - 1);

            RichEditFormat.Redraw(richTextBox_note, false);

            try
            {
                int position = selectionStart;

                while (true)
                {
                    NoteParagraph paragraph = NoteParagraphAt(text, position);
                    paragraphs.Add(paragraph);

                    if (paragraph.End >= last || paragraph.End >= text.Length) break;
                    position = paragraph.End + 1;
                }

                bool already = paragraphs.TrueForAll(p => p.Kind == kind && (kind != NoteLineKind.Heading || p.Level == level));
                if (already && kind != NoteLineKind.Paragraph)
                {
                    kind = NoteLineKind.Paragraph;
                    level = 0;
                }

                foreach (NoteParagraph paragraph in paragraphs)
                {
                    SetNoteParagraphKind(paragraph, text.Length, kind, kind == NoteLineKind.Bullet && paragraph.Kind == NoteLineKind.Bullet ? paragraph.Level : level);
                }
            }
            finally
            {
                richTextBox_note.Select(selectionStart, selectionLength);
                RichEditFormat.Redraw(richTextBox_note, true);
            }

            richTextBox_note.Focus();
            NoteWysiwygEdited();
        }

        private void SetNoteParagraphKind(NoteParagraph paragraph, int textLength, NoteLineKind kind, int level)
        {
            richTextBox_note.Select(paragraph.Start, paragraph.Length);

            var para = new RichEditFormat.ParaFormat
            {
                dwMask = RichEditFormat.PFM_STARTINDENT | RichEditFormat.PFM_OFFSET | RichEditFormat.PFM_NUMBERING
                    | RichEditFormat.PFM_SPACEBEFORE | RichEditFormat.PFM_SPACEAFTER
            };

            switch (kind)
            {
                case NoteLineKind.Heading:
                    para.dySpaceBefore = 160;
                    para.dySpaceAfter = 60;
                    break;
                case NoteLineKind.Bullet:
                    para.wNumbering = RichEditFormat.PFN_BULLET;
                    para.dxStartIndent = NoteMarkdown.BulletIndent + level * NoteMarkdown.BulletStep - NoteMarkdown.BulletHang;
                    para.dxOffset = NoteMarkdown.BulletHang;
                    break;
                case NoteLineKind.Quote:
                    para.dxStartIndent = NoteMarkdown.QuoteIndent;
                    break;
                case NoteLineKind.Code:
                    para.dxStartIndent = NoteMarkdown.CodeIndent;
                    break;
            }

            RichEditFormat.SetPara(richTextBox_note, para);

            //Character formatting goes over the paragraph mark too: an empty paragraph has
            //nothing else to carry it, and a heading is recognised by its size.
            richTextBox_note.Select(paragraph.Start, paragraph.Length + (paragraph.End < textLength ? 1 : 0));

            var chars = new RichEditFormat.CharFormat { dwMask = RichEditFormat.CFM_SIZE };

            if (kind == NoteLineKind.Code)
            {
                chars = CodeFormat(code: true);
                chars.dwMask |= RichEditFormat.CFM_SIZE | RichEditFormat.CFM_BOLD | RichEditFormat.CFM_ITALIC | RichEditFormat.CFM_STRIKEOUT | RichEditFormat.CFM_COLOR;
                chars.dwEffects |= RichEditFormat.CFE_AUTOCOLOR;
                chars.yHeight = NoteMarkdown.CodeSize * 10;
            }
            else
            {
                //Leaving code: the whole paragraph was code, inline code included.
                if (paragraph.Kind == NoteLineKind.Code) chars = CodeFormat(code: false);

                chars.dwMask |= RichEditFormat.CFM_SIZE;
                chars.yHeight = (kind == NoteLineKind.Heading ? NoteMarkdown.HeadingSize(level) : NoteMarkdown.BodySize) * 10;

                if (kind == NoteLineKind.Heading)
                {
                    chars.dwMask |= RichEditFormat.CFM_BOLD | RichEditFormat.CFM_COLOR;
                    chars.dwEffects |= RichEditFormat.CFE_BOLD;
                    chars.crTextColor = RichEditFormat.ColorRef(NoteMarkdown.HeadingColor);
                }
                else if (kind == NoteLineKind.Quote)
                {
                    chars.dwMask |= RichEditFormat.CFM_COLOR;
                    chars.crTextColor = RichEditFormat.ColorRef(NoteMarkdown.QuoteColor);
                }
                else if (paragraph.Kind is NoteLineKind.Heading or NoteLineKind.Quote or NoteLineKind.Code)
                {
                    chars.dwMask |= RichEditFormat.CFM_COLOR;
                    chars.dwEffects |= RichEditFormat.CFE_AUTOCOLOR;
                }

                //A heading's bold is the heading's, not the text's.
                if (paragraph.Kind == NoteLineKind.Heading && kind != NoteLineKind.Heading) chars.dwMask |= RichEditFormat.CFM_BOLD;
            }

            RichEditFormat.SetChar(richTextBox_note, chars);

            //And for what is typed into an empty paragraph next.
            if (paragraph.Length == 0)
            {
                richTextBox_note.Select(paragraph.Start, 0);
                RichEditFormat.SetChar(richTextBox_note, chars);
            }
        }

        /// <summary>Tab and Shift+Tab nest and un-nest bullets.</summary>
        private bool IndentNoteBullets(bool outdent)
        {
            int selectionStart = richTextBox_note.SelectionStart;
            int selectionLength = richTextBox_note.SelectionLength;
            string text = richTextBox_note.Text;

            var paragraphs = new System.Collections.Generic.List<NoteParagraph>();
            int last = selectionStart + Math.Max(0, selectionLength - 1);

            RichEditFormat.Redraw(richTextBox_note, false);

            try
            {
                int position = selectionStart;

                while (true)
                {
                    NoteParagraph paragraph = NoteParagraphAt(text, position);
                    paragraphs.Add(paragraph);

                    if (paragraph.End >= last || paragraph.End >= text.Length) break;
                    position = paragraph.End + 1;
                }

                if (!paragraphs.TrueForAll(p => p.Kind == NoteLineKind.Bullet)) return false;

                foreach (NoteParagraph paragraph in paragraphs)
                {
                    int level = Math.Max(0, paragraph.Level + (outdent ? -2 : 2));
                    richTextBox_note.Select(paragraph.Start, paragraph.Length);

                    RichEditFormat.SetPara(richTextBox_note, new RichEditFormat.ParaFormat
                    {
                        dwMask = RichEditFormat.PFM_STARTINDENT | RichEditFormat.PFM_OFFSET,
                        dxStartIndent = NoteMarkdown.BulletIndent + level * NoteMarkdown.BulletStep - NoteMarkdown.BulletHang,
                        dxOffset = NoteMarkdown.BulletHang
                    });
                }
            }
            finally
            {
                richTextBox_note.Select(selectionStart, selectionLength);
                RichEditFormat.Redraw(richTextBox_note, true);
            }

            NoteWysiwygEdited();
            return true;
        }

        // ---------------------------------------------------------------- keyboard

        private void RichTextBox_note_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (HandleNoteKey(e))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>True when the key was dealt with here and RichEdit must not see it.</summary>
        private bool HandleNoteKey(KeyEventArgs e)
        {
            Keys key = e.KeyCode;

            if (e.Control && !e.Alt)
            {
                switch (key)
                {
                    case Keys.B when !e.Shift: ToggleNoteInline(NoteInline.Bold); return true;
                    case Keys.I when !e.Shift: ToggleNoteInline(NoteInline.Italic); return true;
                    case Keys.X when e.Shift: ToggleNoteInline(NoteInline.Strike); return true;
                    case Keys.E when !e.Shift: ToggleNoteInline(NoteInline.Code); return true;
                    case Keys.D1 or Keys.NumPad1 when !e.Shift: ApplyNoteParagraphKind(NoteLineKind.Heading, 1); return true;
                    case Keys.D2 or Keys.NumPad2 when !e.Shift: ApplyNoteParagraphKind(NoteLineKind.Heading, 2); return true;
                    case Keys.D3 or Keys.NumPad3 when !e.Shift: ApplyNoteParagraphKind(NoteLineKind.Heading, 3); return true;
                    case Keys.D0 or Keys.NumPad0 when !e.Shift: ApplyNoteParagraphKind(NoteLineKind.Paragraph, 0); return true;
                    case Keys.D8 when e.Shift: ApplyNoteParagraphKind(NoteLineKind.Bullet, 0); return true;
                    case Keys.D9 when e.Shift: ApplyNoteParagraphKind(NoteLineKind.Quote, 0); return true;
                    case Keys.C when e.Shift: ApplyNoteParagraphKind(NoteLineKind.Code, 0); return true;
                    case Keys.S when !e.Shift: Button_notes_save_Click(this, EventArgs.Empty); return true;
                    case Keys.V: PasteNotePlainText(); return true;
                    case Keys.Z or Keys.Y when !e.Shift:
                        //Undoing a formatting change raises no TextChanged.
                        if (key == Keys.Z ? richTextBox_note.CanUndo : richTextBox_note.CanRedo) NoteWysiwygEdited();
                        return false;
                }

                //Everything else RichEdit would do with Ctrl is formatting Markdown has no
                //spelling for - underline, alignment, line spacing, numbering, sub- and
                //superscript, all caps. Editing and moving around stay.
                return !NoteCtrlKeyAllowed(key, e.Shift);
            }

            if (key == Keys.F5 && e.Modifiers == Keys.None)
            {
                InsertNoteDateTime();
                return true;
            }

            if (e.Shift && !e.Control && !e.Alt && key == Keys.Insert)
            {
                PasteNotePlainText();
                return true;
            }

            if (key == Keys.Enter && !e.Control && !e.Alt) return HandleNoteEnter(e.Shift);

            if (key == Keys.Tab && !e.Control && !e.Alt) return IndentNoteBullets(outdent: e.Shift);

            return false;
        }

        private static bool NoteCtrlKeyAllowed(Keys key, bool shift)
        {
            switch (key)
            {
                case Keys.ControlKey:
                case Keys.ShiftKey:
                case Keys.Menu:
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Back:
                case Keys.Delete:
                case Keys.Insert:
                case Keys.Tab:
                    return true;
                case Keys.A:
                case Keys.C:
                case Keys.X:
                    return !shift;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Enter leaves a heading for normal text, and on an empty bullet or quote it ends the
        /// list or quote instead of starting another one. Shift+Enter is a plain Enter - a line
        /// break inside a paragraph has no place in the one-line-per-paragraph model.
        /// </summary>
        private bool HandleNoteEnter(bool shift)
        {
            if (currentNote is null || richTextBox_note.SelectionLength > 0) return shift && InsertNoteNewline();

            int caret = richTextBox_note.SelectionStart;
            string text = richTextBox_note.Text;
            NoteParagraph paragraph = NoteParagraphAt(text, caret);
            richTextBox_note.Select(caret, 0);

            if (paragraph.Length == 0 && paragraph.Kind is NoteLineKind.Bullet or NoteLineKind.Quote)
            {
                ApplyNoteParagraphKind(NoteLineKind.Paragraph, 0);
                return true;
            }

            if (paragraph.Kind == NoteLineKind.Heading && caret == paragraph.End)
            {
                InsertNoteNewline();

                string after = richTextBox_note.Text;
                NoteParagraph next = NoteParagraphAt(after, caret + 1);
                SetNoteParagraphKind(next, after.Length, NoteLineKind.Paragraph, 0);
                richTextBox_note.Select(caret + 1, 0);
                return true;
            }

            return shift && InsertNoteNewline();
        }

        private bool InsertNoteNewline()
        {
            richTextBox_note.SelectedText = "\n";
            return true;
        }

        /// <summary>
        /// Pastes text only: formatting from a browser or Word would bring along everything
        /// Markdown cannot hold, and none of it would survive the next save anyway.
        /// </summary>
        private void PasteNotePlainText()
        {
            if (!Clipboard.ContainsText()) return;

            string text = Clipboard.GetText().Replace("\r\n", "\n").Replace('\r', '\n');
            richTextBox_note.SelectedText = text;
        }

        private void RichTextBox_note_LinkClicked(object sender, LinkClickedEventArgs e)
        {
            //Ctrl+click follows a link; a plain click is for putting the caret into it.
            if ((ModifierKeys & Keys.Control) == 0) return;

            string url = NoteLinkTarget(e);
            if (url is null) return;

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// The target of a clicked link. RichEdit keeps it as hidden text in front of the
        /// shown text: HYPERLINK "target".
        /// </summary>
        private string NoteLinkTarget(LinkClickedEventArgs e)
        {
            string candidate = e.LinkText;

            int marker = candidate?.IndexOf("HYPERLINK \"", StringComparison.Ordinal) ?? -1;

            if (marker < 0)
            {
                string text = richTextBox_note.Text;
                int at = Math.Min(text.Length, e.LinkStart + 1);
                int found = at > 0 ? text.LastIndexOf("HYPERLINK \"", at - 1, StringComparison.Ordinal) : -1;

                if (found >= 0)
                {
                    candidate = text;
                    marker = found;
                }
            }

            if (marker >= 0)
            {
                int start = marker + "HYPERLINK \"".Length;
                int end = candidate.IndexOf('"', start);
                candidate = end > start ? candidate.Substring(start, end - start) : null;
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri uri)) return null;

            return uri.Scheme is "http" or "https" or "mailto" ? uri.AbsoluteUri : null;
        }
    }
}
