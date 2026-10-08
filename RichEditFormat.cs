using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ApiTester
{
    /// <summary>
    /// Direct EM_GET/SETCHARFORMAT and EM_GET/SETPARAFORMAT on a RichTextBox. The managed
    /// Selection* properties set a whole font at once, which over a mixed selection (bold next
    /// to code) flattens everything to one style; the messages change exactly the attributes in
    /// the mask, and say which attributes a mixed selection does not share.
    /// </summary>
    internal static class RichEditFormat
    {
        private const int WM_USER = 0x0400;
        private const int EM_GETCHARFORMAT = WM_USER + 58;
        private const int EM_GETPARAFORMAT = WM_USER + 61;
        private const int EM_SETCHARFORMAT = WM_USER + 68;
        private const int EM_SETPARAFORMAT = WM_USER + 71;
        private const int WM_SETREDRAW = 0x000B;

        private const int SCF_SELECTION = 0x0001;

        public const uint CFM_BOLD = 0x00000001;
        public const uint CFM_ITALIC = 0x00000002;
        public const uint CFM_UNDERLINE = 0x00000004;
        public const uint CFM_STRIKEOUT = 0x00000008;
        public const uint CFM_BACKCOLOR = 0x04000000;
        public const uint CFM_CHARSET = 0x08000000;
        public const uint CFM_FACE = 0x20000000;
        public const uint CFM_COLOR = 0x40000000;
        public const uint CFM_SIZE = 0x80000000;

        public const uint CFE_BOLD = 0x00000001;
        public const uint CFE_ITALIC = 0x00000002;
        public const uint CFE_UNDERLINE = 0x00000004;
        public const uint CFE_STRIKEOUT = 0x00000008;
        public const uint CFE_AUTOBACKCOLOR = 0x04000000;
        public const uint CFE_AUTOCOLOR = 0x40000000;

        public const uint PFM_STARTINDENT = 0x00000001;
        public const uint PFM_OFFSET = 0x00000004;
        public const uint PFM_NUMBERING = 0x00000020;
        public const uint PFM_SPACEBEFORE = 0x00000040;
        public const uint PFM_SPACEAFTER = 0x00000080;

        public const ushort PFN_BULLET = 1;

        [InlineArray(32)]
        public struct FaceName
        {
            private ushort element;
        }

        [InlineArray(32)]
        public struct TabStops
        {
            private int element;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct CharFormat
        {
            public uint cbSize;
            public uint dwMask;
            public uint dwEffects;
            public int yHeight;
            public int yOffset;
            public int crTextColor;
            public byte bCharSet;
            public byte bPitchAndFamily;
            public FaceName szFaceName;
            public ushort wWeight;
            public short sSpacing;
            public int crBackColor;
            public int lcid;
            public uint dwReserved;
            public short sStyle;
            public ushort wKerning;
            public byte bUnderlineType;
            public byte bAnimation;
            public byte bRevAuthor;
            public byte bUnderlineColor;

            public string Face
            {
                readonly get
                {
                    Span<char> chars = stackalloc char[32];
                    int length = 0;
                    for (int i = 0; i < 32 && szFaceName[i] != 0; i++) chars[length++] = (char)szFaceName[i];
                    return new string(chars[..length]);
                }
                set
                {
                    for (int i = 0; i < 32; i++) szFaceName[i] = i < value.Length && i < 31 ? value[i] : (ushort)0;
                }
            }

            /// <summary>Half-points, as RTF counts them; the message speaks twips.</summary>
            public readonly int HalfPoints => yHeight / 10;

            public readonly bool Has(uint mask) => (dwMask & mask) == mask;

            public readonly bool Is(uint mask, uint effect) => Has(mask) && (dwEffects & effect) != 0;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct ParaFormat
        {
            public uint cbSize;
            public uint dwMask;
            public ushort wNumbering;
            public ushort wEffects;
            public int dxStartIndent;
            public int dxRightIndent;
            public int dxOffset;
            public ushort wAlignment;
            public short cTabCount;
            public TabStops rgxTabs;
            public int dySpaceBefore;
            public int dySpaceAfter;
            public int dyLineSpacing;
            public short sStyle;
            public byte bLineSpacingRule;
            public byte bOutlineLevel;
            public ushort wShadingWeight;
            public ushort wShadingStyle;
            public ushort wNumberingStart;
            public ushort wNumberingStyle;
            public ushort wNumberingTab;
            public ushort wBorderSpace;
            public ushort wBorderWidth;
            public ushort wBorders;

            /// <summary>Where the text of the paragraph's wrapped lines starts - RTF's \li.</summary>
            public readonly int LeftIndent => dxStartIndent + dxOffset;
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendCharFormat(IntPtr hWnd, int msg, IntPtr wParam, ref CharFormat format);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendParaFormat(IntPtr hWnd, int msg, IntPtr wParam, ref ParaFormat format);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int EM_GETZOOM = WM_USER + 224;
        private const int EM_SETZOOM = WM_USER + 225;

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendGetZoom(IntPtr hWnd, int msg, ref int numerator, ref int denominator);

        /// <summary>
        /// Zoom in percent, straight from the control. RichTextBox.ZoomFactor caches the last
        /// value it set and skips setting it "again" - but loading new content resets the
        /// control's zoom behind that cache, and Ctrl+wheel changes it without telling it.
        /// </summary>
        public static int GetZoom(RichTextBox box)
        {
            int numerator = 0;
            int denominator = 0;
            SendGetZoom(box.Handle, EM_GETZOOM, ref numerator, ref denominator);

            return numerator > 0 && denominator > 0 ? (int)Math.Round(numerator * 100.0 / denominator) : 100;
        }

        public static void SetZoom(RichTextBox box, int percent)
        {
            percent = Math.Clamp(percent, 10, 500);

            //0/0 is RichEdit's "no zoom".
            SendMessage(box.Handle, EM_SETZOOM, percent == 100 ? 0 : percent, percent == 100 ? 0 : 100);
        }

        private const int EM_GETTEXTEX = WM_USER + 94;
        private const int EM_GETTEXTLENGTHEX = WM_USER + 95;
        private const uint GTL_PRECISE = 2;
        private const uint GTL_NUMCHARS = 8;
        private const uint GT_NOHIDDENTEXT = 8;
        private const uint CP_UNICODE = 1200;

        [StructLayout(LayoutKind.Sequential)]
        private struct GetTextLengthEx
        {
            public uint flags;
            public uint codepage;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GetTextEx
        {
            public uint cb;
            public uint flags;
            public uint codepage;
            public IntPtr lpDefaultChar;
            public IntPtr lpUsedDefChar;
        }

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendTextLength(IntPtr hWnd, int msg, ref GetTextLengthEx wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendGetText(IntPtr hWnd, int msg, ref GetTextEx wParam, IntPtr lParam);

        /// <summary>
        /// The text as it is seen, "\n" between paragraphs. RichTextBox.Text also holds hidden
        /// text - the target of every link, for one.
        /// </summary>
        public static string VisibleText(RichTextBox box)
        {
            var length = new GetTextLengthEx { flags = GTL_PRECISE | GTL_NUMCHARS, codepage = CP_UNICODE };
            int chars = (int)SendTextLength(box.Handle, EM_GETTEXTLENGTHEX, ref length, IntPtr.Zero);

            int bytes = (chars + 1) * 2;
            IntPtr buffer = Marshal.AllocHGlobal(bytes);

            try
            {
                var request = new GetTextEx { cb = (uint)bytes, flags = GT_NOHIDDENTEXT, codepage = CP_UNICODE };
                int copied = (int)SendGetText(box.Handle, EM_GETTEXTEX, ref request, buffer);

                return (Marshal.PtrToStringUni(buffer, Math.Max(0, copied)) ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static CharFormat GetChar(RichTextBox box)
        {
            var format = new CharFormat { cbSize = (uint)Marshal.SizeOf<CharFormat>() };
            SendCharFormat(box.Handle, EM_GETCHARFORMAT, SCF_SELECTION, ref format);
            return format;
        }

        public static void SetChar(RichTextBox box, CharFormat format)
        {
            format.cbSize = (uint)Marshal.SizeOf<CharFormat>();
            SendCharFormat(box.Handle, EM_SETCHARFORMAT, SCF_SELECTION, ref format);
        }

        public static ParaFormat GetPara(RichTextBox box)
        {
            var format = new ParaFormat { cbSize = (uint)Marshal.SizeOf<ParaFormat>() };
            SendParaFormat(box.Handle, EM_GETPARAFORMAT, IntPtr.Zero, ref format);
            return format;
        }

        public static void SetPara(RichTextBox box, ParaFormat format)
        {
            format.cbSize = (uint)Marshal.SizeOf<ParaFormat>();
            SendParaFormat(box.Handle, EM_SETPARAFORMAT, IntPtr.Zero, ref format);
        }

        /// <summary>Sets or clears effect bits (bold, italic, ...) over the selection.</summary>
        public static void SetEffect(RichTextBox box, uint mask, bool on)
        {
            SetChar(box, new CharFormat { dwMask = mask, dwEffects = on ? mask : 0 });
        }

        public static int ColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

        /// <summary>Stops the box repainting while a multi-step change is applied.</summary>
        public static void Redraw(RichTextBox box, bool on)
        {
            SendMessage(box.Handle, WM_SETREDRAW, on ? 1 : 0, IntPtr.Zero);
            if (on) box.Invalidate();
        }
    }
}
