using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ApiTester
{
    [Flags]
    internal enum NoteInline
    {
        None = 0,
        Bold = 1,
        Italic = 2,
        Strike = 4,
        Code = 8
    }

    internal enum NoteLineKind
    {
        Paragraph,
        Heading,
        Bullet,
        Quote,
        Code
    }

    /// <summary>A stretch of text with one inline style - and one link target, if any.</summary>
    internal sealed class NoteRun
    {
        public string Text;
        public NoteInline Style;
        public string Url;

        public NoteRun(string text, NoteInline style, string url)
        {
            Text = text;
            Style = style;
            Url = url;
        }
    }

    /// <summary>
    /// One Markdown source line - and one paragraph in the visual editor. Keeping the two
    /// one-to-one is what lets a note go through the visual editor without its line structure
    /// being reflowed.
    /// </summary>
    internal sealed class NoteLine
    {
        public NoteLineKind Kind;

        //Heading: 1-6. Bullet: the columns of indentation in front of the marker.
        public int Level;

        //Code lines hold a single run with the verbatim text.
        public List<NoteRun> Runs = new();

        public string PlainText
        {
            get
            {
                var text = new StringBuilder();
                foreach (NoteRun run in Runs) text.Append(run.Text);
                return text.ToString();
            }
        }
    }

    /// <summary>
    /// The notes' visual editor speaks RTF, the notes themselves are stored as Markdown. This is
    /// the translation between the two, for the subset the visual editor can show: ATX headings,
    /// bullets, quotes, fenced code, bold, italic, strikethrough, inline code and links.
    /// Anything else is kept as its literal text - a table or a numbered list shows as typed
    /// and is written back as typed.
    ///
    /// Writing back is verified rather than trusted: each line's Markdown is parsed again and
    /// compared with what the editor holds, and the next way of spelling it is tried when the
    /// two disagree (escaping, other emphasis markers).
    /// </summary>
    internal static class NoteMarkdown
    {
        public const string Eol = "\r\n";

        // ---------------------------------------------------------------- Markdown -> model

        private static readonly Regex HeadingRegex = new(@"^(#{1,6})(?:[ \t]+(.*))?$", RegexOptions.Compiled);
        private static readonly Regex BulletRegex = new(@"^([ \t]*)[-*+][ \t]+(.*)$", RegexOptions.Compiled);
        private static readonly Regex QuoteRegex = new(@"^>[ ]?(.*)$", RegexOptions.Compiled);
        private static readonly Regex FenceRegex = new(@"^ {0,3}(`{3,}|~{3,})(.*)$", RegexOptions.Compiled);

        public static List<NoteLine> Parse(string markdown)
        {
            var lines = new List<NoteLine>();
            string fence = null;

            foreach (string line in SplitLines(markdown))
            {
                if (fence is not null)
                {
                    lines.Add(CodeLine(line));
                    if (ClosesFence(line, fence)) fence = null;
                    continue;
                }

                string opening = FenceOpening(line);
                if (opening is not null)
                {
                    lines.Add(CodeLine(line));
                    fence = opening;
                    continue;
                }

                lines.Add(ParseBlockLine(line));
            }

            return lines;
        }

        public static string[] SplitLines(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private static NoteLine CodeLine(string text)
        {
            var line = new NoteLine { Kind = NoteLineKind.Code };
            if (text.Length > 0) line.Runs.Add(new NoteRun(text, NoteInline.None, null));
            return line;
        }

        /// <summary>The fence a line opens with ("```", "~~~~"...), or null.</summary>
        public static string FenceOpening(string line)
        {
            Match m = FenceRegex.Match(line);
            if (!m.Success) return null;

            //CommonMark: a backtick fence's info string cannot hold a backtick - that line is
            //inline code, not a fence.
            if (m.Groups[1].Value[0] == '`' && m.Groups[2].Value.Contains('`')) return null;

            return m.Groups[1].Value;
        }

        private static bool ClosesFence(string line, string fence)
        {
            string trimmed = line.TrimEnd(' ', '\t');
            int indent = 0;
            while (indent < trimmed.Length && indent < 3 && trimmed[indent] == ' ') indent++;

            int run = 0;
            while (indent + run < trimmed.Length && trimmed[indent + run] == fence[0]) run++;

            return run >= fence.Length && indent + run == trimmed.Length;
        }

        /// <summary>A line outside fenced code.</summary>
        private static NoteLine ParseBlockLine(string text)
        {
            Match m = HeadingRegex.Match(text);
            if (m.Success)
            {
                var heading = new NoteLine { Kind = NoteLineKind.Heading, Level = m.Groups[1].Length };
                heading.Runs = ParseInline(m.Groups[2].Value);
                return heading;
            }

            m = BulletRegex.Match(text);
            if (m.Success)
            {
                var bullet = new NoteLine { Kind = NoteLineKind.Bullet, Level = Columns(m.Groups[1].Value) };
                bullet.Runs = ParseInline(m.Groups[2].Value);
                return bullet;
            }

            m = QuoteRegex.Match(text);
            if (m.Success)
            {
                var quote = new NoteLine { Kind = NoteLineKind.Quote };
                quote.Runs = ParseInline(m.Groups[1].Value);
                return quote;
            }

            return new NoteLine { Kind = NoteLineKind.Paragraph, Runs = ParseInline(text) };
        }

        private static int Columns(string whitespace)
        {
            int columns = 0;
            foreach (char c in whitespace) columns += c == '\t' ? 4 : 1;
            return columns;
        }

        public static List<NoteRun> ParseInline(string text)
        {
            var runs = new List<NoteRun>();
            ParseInline(text, 0, text.Length, NoteInline.None, null, runs);
            return Merge(runs);
        }

        private static void ParseInline(string s, int start, int end, NoteInline style, string url, List<NoteRun> output)
        {
            var text = new StringBuilder();
            int i = start;

            while (i < end)
            {
                char c = s[i];

                if (c == '\\' && i + 1 < end && IsEscapable(s[i + 1]))
                {
                    text.Append(s[i + 1]);
                    i += 2;
                    continue;
                }

                if (c == '`')
                {
                    int ticks = RunLength(s, i, end, '`');
                    int close = FindCodeClose(s, i + ticks, end, ticks);

                    if (close >= 0)
                    {
                        Flush(text, style, url, output);

                        string code = s.Substring(i + ticks, close - i - ticks);
                        if (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim(' ').Length > 0)
                        {
                            code = code.Substring(1, code.Length - 2);
                        }

                        output.Add(new NoteRun(code, style | NoteInline.Code, url));
                        i = close + ticks;
                        continue;
                    }

                    text.Append('`', ticks);
                    i += ticks;
                    continue;
                }

                if (c == '[' && url is null && TryLink(s, i, end, out int textEnd, out string target, out int after))
                {
                    Flush(text, style, url, output);
                    ParseInline(s, i + 1, textEnd, style, target, output);
                    i = after;
                    continue;
                }

                if (c == '*' || c == '_' || c == '~')
                {
                    int run = RunLength(s, i, end, c);

                    if (TryEmphasis(s, i, end, c, run, style, url, text, output, out int next))
                    {
                        i = next;
                        continue;
                    }

                    text.Append(c, run);
                    i += run;
                    continue;
                }

                text.Append(c);
                i++;
            }

            Flush(text, style, url, output);
        }

        private static bool TryEmphasis(string s, int i, int end, char c, int run, NoteInline style, string url,
            StringBuilder text, List<NoteRun> output, out int next)
        {
            next = i;

            Span<(int Length, NoteInline Flag)> candidates = stackalloc (int, NoteInline)[3];
            int count = 0;

            if (c == '~')
            {
                if (run >= 2) candidates[count++] = (2, NoteInline.Strike);
            }
            else
            {
                if (run >= 3) candidates[count++] = (3, NoteInline.Bold | NoteInline.Italic);
                if (run >= 2) candidates[count++] = (2, NoteInline.Bold);
                candidates[count++] = (1, NoteInline.Italic);
            }

            for (int k = 0; k < count; k++)
            {
                int length = candidates[k].Length;
                if (!CanOpen(s, i, end, c, length)) continue;

                int close = FindCloser(s, i + length, end, c, length);
                if (close < 0) continue;

                Flush(text, style, url, output);
                ParseInline(s, i + length, close, style | candidates[k].Flag, url, output);
                next = close + length;
                return true;
            }

            return false;
        }

        private static bool CanOpen(string s, int i, int end, char c, int length)
        {
            if (i + length >= end || char.IsWhiteSpace(s[i + length])) return false;

            //Intraword underscores are not emphasis - snake_case_names stay as they are.
            return c != '_' || i == 0 || !char.IsLetterOrDigit(s[i - 1]);
        }

        private static int FindCloser(string s, int from, int end, char c, int length)
        {
            for (int j = from; j < end; j++)
            {
                char ch = s[j];

                if (ch == '\\')
                {
                    j++;
                    continue;
                }

                if (ch == '`')
                {
                    int ticks = RunLength(s, j, end, '`');
                    int close = FindCodeClose(s, j + ticks, end, ticks);
                    j = (close >= 0 ? close + ticks : j + ticks) - 1;
                    continue;
                }

                if (ch != c) continue;

                int run = RunLength(s, j, end, c);

                if (j == from || run < length || char.IsWhiteSpace(s[j - 1]))
                {
                    j += run - 1;
                    continue;
                }

                //A longer run closes an inner emphasis first ("**a *b***") - the outer marker is
                //its last characters.
                int position = j + run - length;

                if (c == '_' && position + length < end && char.IsLetterOrDigit(s[position + length]))
                {
                    j += run - 1;
                    continue;
                }

                return position;
            }

            return -1;
        }

        private static int FindCodeClose(string s, int from, int end, int ticks)
        {
            for (int j = from; j < end; j++)
            {
                if (s[j] != '`') continue;

                int run = RunLength(s, j, end, '`');
                if (run == ticks) return j;

                j += run - 1;
            }

            return -1;
        }

        private static bool TryLink(string s, int i, int end, out int textEnd, out string url, out int after)
        {
            textEnd = -1;
            url = null;
            after = -1;

            int j = i + 1;
            while (j < end && s[j] != ']')
            {
                if (s[j] == '[') return false;
                if (s[j] == '\\') j++;
                j++;
            }

            if (j + 1 >= end || s[j + 1] != '(') return false;

            int close = s.IndexOf(')', j + 2, end - j - 2);
            if (close < 0) return false;

            string target = s.Substring(j + 2, close - j - 2);
            if (target.Length == 0 || target.Contains(' ') || target.Contains('\t')) return false;

            textEnd = j;
            url = target;
            after = close + 1;
            return true;
        }

        private static int RunLength(string s, int i, int end, char c)
        {
            int run = 0;
            while (i + run < end && s[i + run] == c) run++;
            return run;
        }

        private static bool IsEscapable(char c) => c < 128 && char.IsPunctuation(c) || c is '$' or '+' or '<' or '=' or '>' or '^' or '`' or '|' or '~';

        private static void Flush(StringBuilder text, NoteInline style, string url, List<NoteRun> output)
        {
            if (text.Length == 0) return;

            output.Add(new NoteRun(text.ToString(), style, url));
            text.Clear();
        }

        // ---------------------------------------------------------------- model -> Markdown

        public static string Serialize(IReadOnlyList<NoteLine> lines)
        {
            var output = new List<string>(lines.Count + 2);

            //The fence the code lines currently sit in, and whether this writer made it up -
            //code formatted in the visual editor has no fence lines of its own.
            string fence = null;
            bool synthetic = false;

            foreach (NoteLine line in lines)
            {
                if (line.Kind == NoteLineKind.Code)
                {
                    string text = line.PlainText;

                    if (fence is null)
                    {
                        string opening = FenceOpening(text);

                        if (opening is not null)
                        {
                            output.Add(text);
                            fence = opening;
                            synthetic = false;
                            continue;
                        }

                        output.Add("```");
                        fence = "```";
                        synthetic = true;
                        output.Add(text);
                        continue;
                    }

                    output.Add(text);
                    if (ClosesFence(text, fence)) fence = null;
                    continue;
                }

                if (fence is not null)
                {
                    output.Add(synthetic ? "```" : new string(fence[0], fence.Length));
                    fence = null;
                }

                output.Add(SerializeLine(line));
            }

            if (fence is not null && synthetic) output.Add("```");

            return string.Join(Eol, output);
        }

        private static string SerializeLine(NoteLine line)
        {
            NoteLine wanted = Normalized(line);
            string wantedText = wanted.PlainText;
            string sameText = null;

            foreach (string candidate in InlineCandidates(wanted.Runs))
            {
                string full = WithPrefix(wanted, candidate);
                NoteLine parsed = Normalized(ParseStandalone(full));

                if (Equivalent(parsed, wanted)) return full;

                if (sameText is null && SameShape(parsed, wanted) && parsed.PlainText == wantedText) sameText = full;
            }

            //Nothing reads back exactly - a combination this subset cannot spell, like bold
            //that starts inside a link and ends outside it. Keeping every character matters
            //more than keeping every style: a spelling that loses some formatting, or failing
            //that, the bare text with everything special escaped.
            return sameText ?? WithPrefix(wanted, Escape(wantedText));
        }

        private static string WithPrefix(NoteLine line, string content)
        {
            string full = Prefix(line, content.Length == 0) + content;
            return line.Kind == NoteLineKind.Paragraph ? EscapeLineStart(full) : full;
        }

        private static bool SameShape(NoteLine a, NoteLine b)
        {
            return a.Kind == b.Kind && (a.Kind is not (NoteLineKind.Heading or NoteLineKind.Bullet) || a.Level == b.Level);
        }

        private static string Prefix(NoteLine line, bool empty)
        {
            return line.Kind switch
            {
                NoteLineKind.Heading => new string('#', Math.Clamp(line.Level, 1, 6)) + (empty ? string.Empty : " "),
                NoteLineKind.Bullet => new string(' ', Math.Max(0, line.Level)) + "- ",
                NoteLineKind.Quote => empty ? ">" : "> ",
                _ => string.Empty
            };
        }

        /// <summary>
        /// A paragraph whose text would read as a heading, bullet, quote or fence gets its
        /// first character escaped.
        /// </summary>
        private static string EscapeLineStart(string line)
        {
            if (FenceOpening(line) is null && ParseBlockLine(line).Kind == NoteLineKind.Paragraph) return line;

            int first = 0;
            while (first < line.Length && (line[first] == ' ' || line[first] == '\t')) first++;

            return first < line.Length && IsEscapable(line[first])
                ? string.Concat(line.AsSpan(0, first), "\\", line.AsSpan(first))
                : line;
        }

        private static NoteLine ParseStandalone(string line)
        {
            return FenceOpening(line) is not null ? CodeLine(line) : ParseBlockLine(line);
        }

        private static IEnumerable<string> InlineCandidates(List<NoteRun> runs)
        {
            foreach (bool escape in new[] { false, true })
            {
                yield return Minimal(runs, escape);
                yield return SelfContained(runs, escape, underscore: false, alternate: false);
                yield return SelfContained(runs, escape, underscore: false, alternate: true);
                yield return SelfContained(runs, escape, underscore: true, alternate: false);
            }
        }

        /// <summary>
        /// Opens and closes markers only where the style changes: "**a *b***".
        /// </summary>
        private static string Minimal(List<NoteRun> runs, bool escape)
        {
            var text = new StringBuilder();
            var open = new List<NoteInline>();

            foreach (List<NoteRun> group in LinkGroups(runs))
            {
                if (group[0].Url is not null)
                {
                    CloseMarkers(text, open, 0);
                    text.Append('[').Append(Minimal(Unlinked(group), escape)).Append("](").Append(EncodeUrl(group[0].Url)).Append(')');
                    continue;
                }

                NoteRun run = group[0];
                NoteInline wanted = run.Style & ~NoteInline.Code;

                int keep = 0;
                while (keep < open.Count && wanted.HasFlag(open[keep])) keep++;
                CloseMarkers(text, open, keep);

                foreach (NoteInline flag in new[] { NoteInline.Strike, NoteInline.Bold, NoteInline.Italic })
                {
                    if (!wanted.HasFlag(flag) || open.Contains(flag)) continue;

                    text.Append(Marker(flag, underscore: false));
                    open.Add(flag);
                }

                text.Append(RunText(run, escape));
            }

            CloseMarkers(text, open, 0);
            return text.ToString();
        }

        private static void CloseMarkers(StringBuilder text, List<NoteInline> open, int keep)
        {
            for (int k = open.Count - 1; k >= keep; k--)
            {
                text.Append(Marker(open[k], underscore: false));
                open.RemoveAt(k);
            }
        }

        /// <summary>
        /// Every styled run wrapped on its own: "**a**_b_". Alternating the marker character
        /// keeps two adjacent runs from fusing into one long marker.
        /// </summary>
        private static string SelfContained(List<NoteRun> runs, bool escape, bool underscore, bool alternate)
        {
            var text = new StringBuilder();
            int styled = 0;

            foreach (List<NoteRun> group in LinkGroups(runs))
            {
                if (group[0].Url is not null)
                {
                    text.Append('[').Append(SelfContained(Unlinked(group), escape, underscore, alternate)).Append("](").Append(EncodeUrl(group[0].Url)).Append(')');
                    continue;
                }

                NoteRun run = group[0];
                NoteInline style = run.Style & ~NoteInline.Code;

                if (style == NoteInline.None)
                {
                    text.Append(RunText(run, escape));
                    continue;
                }

                bool us = underscore || (alternate && styled % 2 == 1);
                styled++;

                string opening = (style.HasFlag(NoteInline.Strike) ? "~~" : string.Empty)
                    + (style.HasFlag(NoteInline.Bold) ? Marker(NoteInline.Bold, us) : string.Empty)
                    + (style.HasFlag(NoteInline.Italic) ? Marker(NoteInline.Italic, us) : string.Empty);

                char[] closing = opening.ToCharArray();
                Array.Reverse(closing);

                text.Append(opening).Append(RunText(run, escape)).Append(closing);
            }

            return text.ToString();
        }

        private static string Marker(NoteInline flag, bool underscore)
        {
            return flag switch
            {
                NoteInline.Strike => "~~",
                NoteInline.Bold => underscore ? "__" : "**",
                _ => underscore ? "_" : "*"
            };
        }

        /// <summary>Consecutive runs sharing a link target together; every other run alone.</summary>
        private static IEnumerable<List<NoteRun>> LinkGroups(List<NoteRun> runs)
        {
            int i = 0;

            while (i < runs.Count)
            {
                var group = new List<NoteRun> { runs[i] };

                if (runs[i].Url is not null)
                {
                    while (i + group.Count < runs.Count && runs[i + group.Count].Url == runs[i].Url)
                    {
                        group.Add(runs[i + group.Count]);
                    }
                }

                i += group.Count;
                yield return group;
            }
        }

        private static List<NoteRun> Unlinked(List<NoteRun> group)
        {
            var runs = new List<NoteRun>(group.Count);
            foreach (NoteRun run in group) runs.Add(new NoteRun(run.Text, run.Style, null));
            return runs;
        }

        private static string RunText(NoteRun run, bool escape)
        {
            if (run.Style.HasFlag(NoteInline.Code)) return CodeSpan(run.Text);

            return escape ? Escape(run.Text) : run.Text;
        }

        private static string CodeSpan(string code)
        {
            int longest = 0;

            for (int i = 0; i < code.Length; i++)
            {
                if (code[i] != '`') continue;

                int run = RunLength(code, i, code.Length, '`');
                longest = Math.Max(longest, run);
                i += run - 1;
            }

            string ticks = new('`', longest + 1);

            bool pad = code[0] == '`' || code[^1] == '`'
                || (code.Length >= 2 && code[0] == ' ' && code[^1] == ' ' && code.Trim(' ').Length > 0);

            return pad ? ticks + " " + code + " " + ticks : ticks + code + ticks;
        }

        private static string Escape(string text)
        {
            var escaped = new StringBuilder(text.Length + 8);

            foreach (char c in text)
            {
                if (c is '\\' or '*' or '_' or '~' or '`' or '[' or ']') escaped.Append('\\');
                escaped.Append(c);
            }

            return escaped.ToString();
        }

        private static string EncodeUrl(string url)
        {
            return url.Replace(" ", "%20").Replace(")", "%29").Replace("\t", "%09");
        }

        // ---------------------------------------------------------------- comparison

        /// <summary>
        /// The form a line is compared and written in. Style on whitespace at the edge of a run
        /// cannot be written in Markdown ("** a**" is not bold), so it is dropped; headings are
        /// bold by definition; the indentation in front of heading and bullet text is eaten by
        /// the marker's own whitespace.
        /// </summary>
        public static NoteLine Normalized(NoteLine line)
        {
            var result = new NoteLine { Kind = line.Kind, Level = line.Level };

            if (line.Kind == NoteLineKind.Code)
            {
                string code = line.PlainText;
                if (code.Length > 0) result.Runs.Add(new NoteRun(code, NoteInline.None, null));
                return result;
            }

            //Character by character: the style of whitespace outside code is whatever the text
            //on both sides of it shares - a space between two bold words stays bold, a space
            //at the edge of a bold word does not.
            var chars = new List<(char C, NoteInline Style, string Url)>();

            foreach (NoteRun run in line.Runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;

                NoteInline style = line.Kind == NoteLineKind.Heading ? run.Style & ~NoteInline.Bold : run.Style;
                foreach (char c in run.Text) chars.Add((c, style, run.Url));
            }

            for (int i = 0; i < chars.Count; i++)
            {
                if (!char.IsWhiteSpace(chars[i].C) || chars[i].Style.HasFlag(NoteInline.Code)) continue;

                //A link edge is an edge too: "[**a** ](x)" - the bold cannot run on past "]".
                string url = chars[i].Url;

                int end = i;
                while (end < chars.Count && char.IsWhiteSpace(chars[end].C) && !chars[end].Style.HasFlag(NoteInline.Code) && chars[end].Url == url) end++;

                NoteInline before = i > 0 && chars[i - 1].Url == url ? chars[i - 1].Style : NoteInline.None;
                NoteInline after = end < chars.Count && chars[end].Url == url ? chars[end].Style : NoteInline.None;
                NoteInline shared = before & after & ~NoteInline.Code;

                for (int k = i; k < end; k++) chars[k] = (chars[k].C, shared, chars[k].Url);

                i = end - 1;
            }

            var runs = new List<NoteRun>(chars.Count);
            foreach (var c in chars) runs.Add(new NoteRun(c.C.ToString(), c.Style, c.Url));

            if (line.Kind is NoteLineKind.Heading or NoteLineKind.Bullet)
            {
                while (runs.Count > 0 && !runs[0].Style.HasFlag(NoteInline.Code))
                {
                    string trimmed = runs[0].Text.TrimStart(' ', '\t');
                    if (trimmed.Length > 0)
                    {
                        runs[0] = new NoteRun(trimmed, runs[0].Style, runs[0].Url);
                        break;
                    }

                    runs.RemoveAt(0);
                }
            }

            result.Runs = Merge(runs);
            return result;
        }

        private static List<NoteRun> Merge(List<NoteRun> runs)
        {
            var merged = new List<NoteRun>(runs.Count);

            foreach (NoteRun run in runs)
            {
                if (string.IsNullOrEmpty(run.Text)) continue;

                if (merged.Count > 0 && merged[^1].Style == run.Style && merged[^1].Url == run.Url)
                {
                    merged[^1] = new NoteRun(merged[^1].Text + run.Text, run.Style, run.Url);
                }
                else
                {
                    merged.Add(new NoteRun(run.Text, run.Style, run.Url));
                }
            }

            return merged;
        }

        private static bool Equivalent(NoteLine parsed, NoteLine wanted)
        {
            NoteLine a = Normalized(parsed);

            if (a.Kind != wanted.Kind) return false;
            if (a.Kind is NoteLineKind.Heading or NoteLineKind.Bullet && a.Level != wanted.Level) return false;
            if (a.Runs.Count != wanted.Runs.Count) return false;

            for (int i = 0; i < a.Runs.Count; i++)
            {
                if (a.Runs[i].Text != wanted.Runs[i].Text
                    || a.Runs[i].Style != wanted.Runs[i].Style
                    || a.Runs[i].Url != wanted.Runs[i].Url) return false;
            }

            return true;
        }

        // ---------------------------------------------------------------- model -> RTF

        //Sizes are in RTF half-points. A heading is recognised by its size when the RTF is read
        //back, so every level needs one of its own, and none may equal the body size.
        public const int BodySize = 22;
        public const int CodeSize = 20;
        private static readonly int[] HeadingSizes = { 40, 34, 30, 26, 24, 23 };

        //Paragraph indents in twips. The kind of a paragraph is read back from them.
        public const int CodeIndent = 240;
        public const int QuoteIndent = 480;
        public const int BulletIndent = 360;
        public const int BulletStep = 180;
        public const int BulletHang = 240;

        public const string BodyFont = "Segoe UI";
        public const string CodeFont = "Consolas";

        //Colour table indexes, in the order of the table in RtfHeader.
        public static readonly System.Drawing.Color CodeBack = System.Drawing.Color.FromArgb(236, 236, 236);
        public static readonly System.Drawing.Color LinkColor = System.Drawing.Color.FromArgb(0, 102, 204);
        public static readonly System.Drawing.Color QuoteColor = System.Drawing.Color.FromArgb(96, 96, 96);
        public static readonly System.Drawing.Color FenceColor = System.Drawing.Color.FromArgb(150, 150, 150);
        public static readonly System.Drawing.Color HeadingColor = System.Drawing.Color.FromArgb(31, 56, 100);

        public static int HeadingSize(int level) => HeadingSizes[Math.Clamp(level, 1, 6) - 1];

        public static int HeadingLevel(int halfPoints)
        {
            int index = Array.IndexOf(HeadingSizes, halfPoints);
            return index < 0 ? 0 : index + 1;
        }

        public static string ToRtf(IReadOnlyList<NoteLine> lines)
        {
            var rtf = new StringBuilder();

            rtf.Append(@"{\rtf1\ansi\ansicpg1252\deff0\uc1");
            rtf.Append(@"{\fonttbl{\f0\fswiss\fcharset0 ").Append(BodyFont).Append(";}");
            rtf.Append(@"{\f1\fmodern\fcharset0 ").Append(CodeFont).Append(";}");
            rtf.Append(@"{\f2\fnil\fcharset2 Symbol;}}");
            rtf.Append(@"{\colortbl ;");
            foreach (System.Drawing.Color color in new[] { CodeBack, LinkColor, QuoteColor, FenceColor, HeadingColor })
            {
                rtf.Append(CultureInfo.InvariantCulture, $@"\red{color.R}\green{color.G}\blue{color.B};");
            }
            rtf.Append('}');
            rtf.Append("\r\n");

            string fence = null;

            //Every line ends in \par, the last one included: RichEdit takes the final \par of
            //the stream as its end-of-document mark, and without it the last line's paragraph
            //format (bullet, quote...) is dropped.
            for (int index = 0; index < lines.Count; index++)
            {
                NoteLine line = lines[index];

                if (index > 0) rtf.Append(@"\par").Append("\r\n");

                rtf.Append(@"\pard");

                switch (line.Kind)
                {
                    case NoteLineKind.Heading:
                        rtf.Append(CultureInfo.InvariantCulture, $@"\sb160\sa60\plain\f0\b\fs{HeadingSize(line.Level)}\cf5 ");
                        break;
                    case NoteLineKind.Bullet:
                        rtf.Append(@"{\pntext\f2\'B7\tab}{\*\pn\pnlvlblt\pnf2\pnindent0{\pntxtb\'B7}}");
                        rtf.Append(CultureInfo.InvariantCulture, $@"\fi-{BulletHang}\li{BulletIndent + Math.Max(0, line.Level) * BulletStep}\plain\f0\fs{BodySize} ");
                        break;
                    case NoteLineKind.Quote:
                        rtf.Append(CultureInfo.InvariantCulture, $@"\li{QuoteIndent}\plain\f0\fs{BodySize}\cf3 ");
                        break;
                    case NoteLineKind.Code:
                        //Fence lines stay visible, dimmed: hiding them would leave nowhere to
                        //keep the info string, and two code blocks in a row would merge.
                        string text = line.PlainText;
                        bool isFence = fence is null ? FenceOpening(text) is not null : ClosesFence(text, fence);
                        if (fence is null) fence = FenceOpening(text);
                        else if (ClosesFence(text, fence)) fence = null;

                        rtf.Append(CultureInfo.InvariantCulture, $@"\li{CodeIndent}\plain\f1\fs{CodeSize}\highlight1{(isFence ? @"\cf4" : string.Empty)} ");
                        rtf.Append(EscapeRtf(text));
                        continue;
                    default:
                        rtf.Append(CultureInfo.InvariantCulture, $@"\plain\f0\fs{BodySize} ");
                        break;
                }

                if (line.Kind != NoteLineKind.Code) fence = null;

                foreach (List<NoteRun> group in LinkGroups(line.Runs))
                {
                    if (group[0].Url is not null)
                    {
                        rtf.Append(@"{\field{\*\fldinst{HYPERLINK """).Append(EscapeRtf(group[0].Url.Replace("\"", "%22"))).Append(@"""}}{\fldrslt{");
                        foreach (NoteRun run in group) AppendRtfRun(rtf, run, link: true);
                        rtf.Append("}}}");
                        continue;
                    }

                    AppendRtfRun(rtf, group[0], link: false);
                }
            }

            rtf.Append(@"\par").Append("\r\n").Append('}');
            return rtf.ToString();
        }

        private static void AppendRtfRun(StringBuilder rtf, NoteRun run, bool link)
        {
            var props = new StringBuilder();
            if (run.Style.HasFlag(NoteInline.Bold)) props.Append(@"\b");
            if (run.Style.HasFlag(NoteInline.Italic)) props.Append(@"\i");
            if (run.Style.HasFlag(NoteInline.Strike)) props.Append(@"\strike");
            if (run.Style.HasFlag(NoteInline.Code)) props.Append(@"\f1\highlight1");
            if (link) props.Append(@"\ul\cf2");

            //The space only delimits a control word - after a bare brace it would be text.
            rtf.Append('{').Append(props);
            if (props.Length > 0) rtf.Append(' ');
            rtf.Append(EscapeRtf(run.Text)).Append('}');
        }

        private static string EscapeRtf(string text)
        {
            var escaped = new StringBuilder(text.Length + 8);

            foreach (char c in text)
            {
                if (c == '\\' || c == '{' || c == '}') escaped.Append('\\').Append(c);
                else if (c == '\t') escaped.Append(@"\tab ");
                else if (c < 32) continue;
                else if (c > 126) escaped.Append(@"\u").Append(((short)c).ToString(CultureInfo.InvariantCulture)).Append('?');
                else escaped.Append(c);
            }

            return escaped.ToString();
        }

        // ---------------------------------------------------------------- RTF -> model

        private static readonly HashSet<string> MonospaceFonts = new(StringComparer.OrdinalIgnoreCase)
        {
            "Consolas", "Courier New", "Courier", "Cascadia Mono", "Cascadia Code", "Lucida Console", "Lucida Sans Typewriter"
        };

        private sealed class RtfState
        {
            public int Font;
            public int Size = 24;
            public bool Bold;
            public bool Italic;
            public bool Strike;
            public bool Hidden;
            public bool Skip;
            public bool FontTable;
            public bool FieldInstruction;
            public string FieldUrl;
            public string LinkUrl;
            public int UnicodeSkip = 1;

            public RtfState Clone() => (RtfState)MemberwiseClone();
        }

        /// <summary>
        /// Reads the RTF the visual editor produces back into lines. Only what this editor can
        /// produce is understood; everything else in the stream is skipped over.
        /// </summary>
        public static List<NoteLine> FromRtf(string rtf)
        {
            var lines = new List<NoteLine>();

            var fontNames = new Dictionary<int, StringBuilder>();
            var fontCharsets = new Dictionary<int, int>();
            int fontEntry = 0;
            int defaultCodepage = 1252;
            int defaultFont = 0;

            var state = new RtfState();
            var stack = new Stack<RtfState>();
            var fieldText = new StringBuilder();

            //Paragraph properties last until \pard, not until the group closes.
            int leftIndent = 0;
            bool bullet = false;

            var runs = new List<(string Text, NoteInline Style, string Url, int Size)>();
            var text = new StringBuilder();
            NoteInline textStyle = NoteInline.None;
            string textUrl = null;
            int textSize = 0;

            var bytes = new List<byte>();
            int bytesCodepage = 0;
            int skipChars = 0;

            int i = 0;

            void FlushBytes()
            {
                if (bytes.Count == 0) return;

                string decoded;
                try
                {
                    decoded = Encoding.GetEncoding(bytesCodepage).GetString(bytes.ToArray());
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
                {
                    decoded = Encoding.Latin1.GetString(bytes.ToArray());
                }

                bytes.Clear();
                foreach (char c in decoded) AppendChar(c);
            }

            void FlushText()
            {
                if (text.Length == 0) return;

                runs.Add((text.ToString(), textStyle, textUrl, textSize));
                text.Clear();
            }

            void AppendChar(char c)
            {
                if (state.FontTable)
                {
                    if (!fontNames.TryGetValue(fontEntry, out StringBuilder name)) fontNames[fontEntry] = name = new StringBuilder();
                    name.Append(c);
                    return;
                }

                if (state.FieldInstruction)
                {
                    fieldText.Append(c);
                    return;
                }

                if (state.Skip || state.Hidden) return;

                NoteInline style = NoteInline.None;
                if (state.Bold) style |= NoteInline.Bold;
                if (state.Italic) style |= NoteInline.Italic;
                if (state.Strike) style |= NoteInline.Strike;
                if (fontNames.TryGetValue(state.Font, out StringBuilder font) && MonospaceFonts.Contains(font.ToString().TrimEnd(';').Trim())) style |= NoteInline.Code;

                if (text.Length > 0 && (style != textStyle || state.LinkUrl != textUrl || state.Size != textSize)) FlushText();

                textStyle = style;
                textUrl = state.LinkUrl;
                textSize = state.Size;
                text.Append(c);
            }

            void EmitChar(char c)
            {
                if (skipChars > 0)
                {
                    skipChars--;
                    return;
                }

                FlushBytes();
                AppendChar(c);
            }

            void EndParagraph()
            {
                FlushBytes();
                FlushText();
                lines.Add(BuildLine(runs, leftIndent, bullet, state.Size));
                runs.Clear();
            }

            while (i < rtf.Length)
            {
                char c = rtf[i];

                if (c == '{')
                {
                    FlushBytes();
                    stack.Push(state.Clone());
                    i++;
                    continue;
                }

                if (c == '}')
                {
                    FlushBytes();
                    RtfState closing = state;
                    state = stack.Count > 0 ? stack.Pop() : new RtfState();

                    //The instruction group of a field closes before its result opens: hand the
                    //target to the field group, whose result group inherits it.
                    if (closing.FieldInstruction && !state.FieldInstruction)
                    {
                        state.FieldUrl = ParseHyperlink(fieldText.ToString());
                        fieldText.Clear();
                    }

                    i++;
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    i++;
                    continue;
                }

                if (c != '\\')
                {
                    EmitChar(c);
                    i++;
                    continue;
                }

                //Control symbol or control word.
                if (i + 1 >= rtf.Length) break;

                char next = rtf[i + 1];

                if (next == '\'')
                {
                    if (i + 3 < rtf.Length && byte.TryParse(rtf.AsSpan(i + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
                    {
                        if (skipChars > 0)
                        {
                            skipChars--;
                        }
                        else
                        {
                            int codepage = CodepageOf(fontCharsets, state.Font, defaultCodepage);
                            if (bytes.Count > 0 && codepage != bytesCodepage) FlushBytes();
                            bytesCodepage = codepage;
                            bytes.Add(value);
                        }
                    }

                    i += 4;
                    continue;
                }

                if (!char.IsAsciiLetter(next))
                {
                    i += 2;

                    switch (next)
                    {
                        case '\\':
                        case '{':
                        case '}':
                            EmitChar(next);
                            break;
                        case '~':
                            EmitChar(' ');
                            break;
                        case '_':
                            EmitChar('‑');
                            break;
                        case '*':
                            if (!state.FieldInstruction) state.Skip = true;
                            break;
                        case '\r':
                        case '\n':
                            if (!state.Skip && !state.FontTable) EndParagraph();
                            break;
                    }

                    continue;
                }

                int wordStart = i + 1;
                int j = wordStart;
                while (j < rtf.Length && char.IsAsciiLetter(rtf[j])) j++;
                string word = rtf.Substring(wordStart, j - wordStart);

                int? parameter = null;
                int numberStart = j;
                if (j < rtf.Length && (rtf[j] == '-' || char.IsAsciiDigit(rtf[j])))
                {
                    j++;
                    while (j < rtf.Length && char.IsAsciiDigit(rtf[j])) j++;
                    if (int.TryParse(rtf.AsSpan(numberStart, j - numberStart), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number)) parameter = number;
                }

                if (j < rtf.Length && rtf[j] == ' ') j++;
                i = j;

                if (word != "u") FlushBytes();

                switch (word)
                {
                    case "ansicpg":
                        defaultCodepage = parameter ?? 1252;
                        break;
                    case "deff":
                        defaultFont = parameter ?? 0;
                        state.Font = defaultFont;
                        break;
                    case "fonttbl":
                        state.FontTable = true;
                        break;
                    case "f":
                        if (state.FontTable) fontEntry = parameter ?? 0;
                        else state.Font = parameter ?? defaultFont;
                        break;
                    case "fcharset":
                        if (state.FontTable) fontCharsets[fontEntry] = parameter ?? 0;
                        break;
                    case "colortbl":
                    case "stylesheet":
                    case "info":
                    case "pict":
                    case "object":
                    case "header":
                    case "footer":
                    case "generator":
                        state.Skip = true;
                        break;
                    case "pntext":
                    case "listtext":
                        bullet = true;
                        state.Skip = true;
                        break;
                    case "pnlvlblt":
                    case "pnlvlbody":
                        bullet = true;
                        break;
                    case "fldinst":
                        state.FieldInstruction = true;
                        state.Skip = false;
                        fieldText.Clear();
                        break;
                    case "fldrslt":
                        state.LinkUrl = state.FieldUrl;
                        state.Skip = false;
                        break;
                    case "pard":
                        leftIndent = 0;
                        bullet = false;
                        break;
                    case "li":
                        leftIndent = parameter ?? 0;
                        break;
                    case "plain":
                        state.Font = defaultFont;
                        state.Size = 24;
                        state.Bold = state.Italic = state.Strike = state.Hidden = false;
                        break;
                    case "fs":
                        state.Size = parameter ?? 24;
                        break;
                    case "b":
                        state.Bold = parameter != 0;
                        break;
                    case "i":
                        state.Italic = parameter != 0;
                        break;
                    case "strike":
                        state.Strike = parameter != 0;
                        break;
                    case "v":
                        state.Hidden = parameter != 0;
                        break;
                    case "uc":
                        state.UnicodeSkip = parameter ?? 1;
                        break;
                    case "u":
                        if (parameter.HasValue)
                        {
                            FlushBytes();
                            int code = parameter.Value < 0 ? parameter.Value + 65536 : parameter.Value;
                            skipChars = 0;
                            AppendChar((char)code);
                            skipChars = state.UnicodeSkip;
                        }
                        break;
                    case "par":
                    case "line":
                    case "sect":
                    case "page":
                    case "row":
                        if (!state.Skip && !state.FontTable && !state.FieldInstruction) EndParagraph();
                        break;
                    case "tab":
                    case "cell":
                        EmitChar('\t');
                        break;
                    case "emdash":
                        EmitChar('—');
                        break;
                    case "endash":
                        EmitChar('–');
                        break;
                    case "bullet":
                        EmitChar('•');
                        break;
                    case "lquote":
                        EmitChar('‘');
                        break;
                    case "rquote":
                        EmitChar('’');
                        break;
                    case "ldblquote":
                        EmitChar('“');
                        break;
                    case "rdblquote":
                        EmitChar('”');
                        break;
                }
            }

            //Text after the last \par is the final paragraph; RichEdit ends every stream with a
            //\par of its own, which is not a line.
            FlushBytes();
            FlushText();
            if (runs.Count > 0 || lines.Count == 0) lines.Add(BuildLine(runs, leftIndent, bullet, state.Size));

            //The final paragraph mark keeps no character formatting of its own - an empty last
            //line just reports the size of whatever came before it, so Enter at the end of a
            //closing heading would read as a second, empty heading.
            NoteLine lastLine = lines[^1];
            if (lastLine.Kind == NoteLineKind.Heading && lastLine.Runs.Count == 0)
            {
                lastLine.Kind = NoteLineKind.Paragraph;
                lastLine.Level = 0;
            }

            return lines;
        }

        private static int CodepageOf(Dictionary<int, int> charsets, int font, int fallback)
        {
            if (!charsets.TryGetValue(font, out int charset)) return fallback;

            return charset switch
            {
                0 => 1252,
                128 => 932,
                129 => 949,
                134 => 936,
                136 => 950,
                161 => 1253,
                162 => 1254,
                163 => 1258,
                177 => 1255,
                178 => 1256,
                186 => 1257,
                204 => 1251,
                222 => 874,
                238 => 1250,
                _ => fallback
            };
        }

        private static string ParseHyperlink(string instruction)
        {
            Match m = Regex.Match(instruction, @"HYPERLINK\s+""([^""]*)""");
            return m.Success && m.Groups[1].Value.Length > 0 ? m.Groups[1].Value : null;
        }

        private static NoteLine BuildLine(List<(string Text, NoteInline Style, string Url, int Size)> runs, int leftIndent, bool bullet, int markSize)
        {
            int size = markSize;
            foreach (var run in runs)
            {
                if (run.Text.Length == 0) continue;
                size = run.Size;
                break;
            }

            var line = new NoteLine { Kind = KindFromFormat(bullet, leftIndent, size, out int level) };
            line.Level = level;

            if (line.Kind == NoteLineKind.Code)
            {
                var code = new StringBuilder();
                foreach (var run in runs) code.Append(run.Text);
                if (code.Length > 0) line.Runs.Add(new NoteRun(code.ToString(), NoteInline.None, null));
                return line;
            }

            foreach (var run in runs) line.Runs.Add(new NoteRun(run.Text, run.Style, run.Url));
            line.Runs = Merge(line.Runs);

            return line;
        }

        /// <summary>
        /// What kind of line a paragraph is, from its formatting: the bullet, the left indent
        /// (twips) and the font size (half-points) of its text.
        /// </summary>
        public static NoteLineKind KindFromFormat(bool bullet, int leftIndent, int halfPoints, out int level)
        {
            level = 0;

            if (bullet)
            {
                level = Math.Max(0, (int)Math.Round((leftIndent - BulletIndent) / (double)BulletStep));
                return NoteLineKind.Bullet;
            }

            if (leftIndent >= CodeIndent / 2 && leftIndent < (CodeIndent + QuoteIndent) / 2) return NoteLineKind.Code;
            if (leftIndent >= (CodeIndent + QuoteIndent) / 2) return NoteLineKind.Quote;

            level = HeadingLevel(halfPoints);
            return level > 0 ? NoteLineKind.Heading : NoteLineKind.Paragraph;
        }

        public static string PlainText(IReadOnlyList<NoteLine> lines)
        {
            var text = new StringBuilder();

            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append(lines[i].PlainText);
            }

            return text.ToString();
        }
    }
}
