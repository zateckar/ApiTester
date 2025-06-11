using System.Drawing;

namespace ApiTester
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            DataGridViewCellStyle dataGridViewCellStyle1 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle2 = new DataGridViewCellStyle();
            button_request_send = new Button();
            textBox_request_headers = new FastColoredTextBoxNS.FastColoredTextBox();
            textBox_request_body = new FastColoredTextBoxNS.FastColoredTextBox();
            splitContainer6_main_right = new SplitContainer();
            label_group = new Label();
            button_saveGroup = new Button();
            label_displayed_Id = new Label();
            comboBox_group = new ComboBox();
            textBox_request_url = new FastColoredTextBoxNS.FastColoredTextBox();
            comboBox_certificates = new ComboBox();
            comboBox_http_method = new ComboBox();
            splitContainer5_reqres = new SplitContainer();
            tabControl1 = new TabControl();
            tabPage_request_body = new TabPage();
            tabPage_request_header = new TabPage();
            tabControl_response = new TabControl();
            tabPage_response_body = new TabPage();
            textBox_response_body = new FastColoredTextBoxNS.FastColoredTextBox();
            tabPage_response_headers = new TabPage();
            textBox_response_headers = new FastColoredTextBoxNS.FastColoredTextBox();
            tabPage_statistics_information = new TabPage();
            listView1 = new ListView();
            statusStrip_response_stats = new StatusStrip();
            toolStripDropDownButton1 = new ToolStripDropDownButton();
            copyToToolStripMenuItem = new ToolStripMenuItem();
            toolStripDropDownButton_text_utils = new ToolStripDropDownButton();
            toolStripDropDownButton_request = new ToolStripDropDownButton();
            httpVersionToolStripMenuItem = new ToolStripMenuItem();
            toolStripComboBox_http_version = new ToolStripComboBox();
            repeatToolStripMenuItem = new ToolStripMenuItem();
            toolStripTextBox_repeat = new ToolStripTextBox();
            toolStripDropDownButton_clearAll = new ToolStripDropDownButton();
            toolStripDropDownButton_response_stats_certificate = new ToolStripDropDownButton();
            toolStripStatusLabel_response_stats_http_version = new ToolStripStatusLabel();
            toolStripStatusLabel_response_stats_datetime = new ToolStripStatusLabel();
            toolStripStatusLabel_response_stats_response_time = new ToolStripStatusLabel();
            toolStripStatusLabel_response_stats_certificate = new ToolStripStatusLabel();
            dataGridView1 = new DataGridView();
            contextMenuStrip1 = new ContextMenuStrip(components);
            copyToToolStripMenuItem1 = new ToolStripMenuItem();
            splitContainer1_main_form = new SplitContainer();
            tabControl2 = new TabControl();
            tabPage3 = new TabPage();
            splitContainer4 = new SplitContainer();
            textBox_filter = new TextBox();
            comboBox_filter_group = new ComboBox();
            tabPage4 = new TabPage();
            label7 = new Label();
            comboBox_settings_profiles = new ComboBox();
            groupBox1 = new GroupBox();
            groupBox2 = new GroupBox();
            button_blob_list = new Button();
            listBox_blob = new ListBox();
            textBox_blob_container = new TextBox();
            button_blob = new Button();
            label5 = new Label();
            textBox_blob_storage_account = new TextBox();
            textBox_blob_sas_token = new TextBox();
            label1 = new Label();
            label4 = new Label();
            label9 = new Label();
            textBox_profileName = new TextBox();
            button_settings_import = new Button();
            button_settings_export = new Button();
            button_settings_insert = new Button();
            button_settings_delete = new Button();
            button_settings_save = new Button();
            label3 = new Label();
            textBox_cosmos_Endpoint = new TextBox();
            label2 = new Label();
            autocompleteMenu1 = new AutocompleteMenuNS.AutocompleteMenu();
            ((System.ComponentModel.ISupportInitialize)textBox_request_headers).BeginInit();
            ((System.ComponentModel.ISupportInitialize)textBox_request_body).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitContainer6_main_right).BeginInit();
            splitContainer6_main_right.Panel1.SuspendLayout();
            splitContainer6_main_right.Panel2.SuspendLayout();
            splitContainer6_main_right.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)textBox_request_url).BeginInit();
            ((System.ComponentModel.ISupportInitialize)splitContainer5_reqres).BeginInit();
            splitContainer5_reqres.Panel1.SuspendLayout();
            splitContainer5_reqres.Panel2.SuspendLayout();
            splitContainer5_reqres.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage_request_body.SuspendLayout();
            tabPage_request_header.SuspendLayout();
            tabControl_response.SuspendLayout();
            tabPage_response_body.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)textBox_response_body).BeginInit();
            tabPage_response_headers.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)textBox_response_headers).BeginInit();
            tabPage_statistics_information.SuspendLayout();
            statusStrip_response_stats.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            contextMenuStrip1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer1_main_form).BeginInit();
            splitContainer1_main_form.Panel1.SuspendLayout();
            splitContainer1_main_form.Panel2.SuspendLayout();
            splitContainer1_main_form.SuspendLayout();
            tabControl2.SuspendLayout();
            tabPage3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer4).BeginInit();
            splitContainer4.Panel1.SuspendLayout();
            splitContainer4.Panel2.SuspendLayout();
            splitContainer4.SuspendLayout();
            tabPage4.SuspendLayout();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // button_request_send
            // 
            button_request_send.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_request_send.BackColor = Color.DarkOrange;
            button_request_send.FlatAppearance.BorderColor = Color.Black;
            button_request_send.FlatAppearance.BorderSize = 0;
            button_request_send.FlatAppearance.MouseDownBackColor = Color.Peru;
            button_request_send.FlatStyle = FlatStyle.Flat;
            button_request_send.Image = (Image)resources.GetObject("button_request_send.Image");
            button_request_send.Location = new Point(1258, 31);
            button_request_send.Margin = new Padding(4);
            button_request_send.Name = "button_request_send";
            button_request_send.Size = new Size(65, 47);
            button_request_send.TabIndex = 0;
            button_request_send.UseVisualStyleBackColor = false;
            button_request_send.Click += Button_request_send_Click;
            // 
            // textBox_request_headers
            // 
            textBox_request_headers.AutoCompleteBracketsList = new char[]
    {
    '(',
    ')',
    '{',
    '}',
    '[',
    ']',
    '"',
    '"',
    '\'',
    '\''
    };
            autocompleteMenu1.SetAutocompleteMenu(textBox_request_headers, null);
            textBox_request_headers.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>.+)\r\n";
            textBox_request_headers.AutoScrollMinSize = new Size(0, 22);
            textBox_request_headers.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            textBox_request_headers.BackBrush = null;
            textBox_request_headers.CharHeight = 17;
            textBox_request_headers.CharWidth = 8;
            textBox_request_headers.CommentPrefix = "--";
            textBox_request_headers.CurrentLineColor = Color.Gainsboro;
            textBox_request_headers.Cursor = Cursors.IBeam;
            textBox_request_headers.DefaultMarkerSize = 8;
            textBox_request_headers.DisabledColor = Color.FromArgb(100, 180, 180, 180);
            textBox_request_headers.Dock = DockStyle.Fill;
            textBox_request_headers.Font = new Font("Consolas", 9F);
            textBox_request_headers.HighlightFoldingIndicator = false;
            textBox_request_headers.Hotkeys = resources.GetString("textBox_request_headers.Hotkeys");
            textBox_request_headers.IsReplaceMode = false;
            textBox_request_headers.LeftBracket = '(';
            textBox_request_headers.LeftBracket2 = '{';
            textBox_request_headers.LineNumberColor = SystemColors.ButtonShadow;
            textBox_request_headers.Location = new Point(3, 3);
            textBox_request_headers.Margin = new Padding(4);
            textBox_request_headers.Name = "textBox_request_headers";
            textBox_request_headers.Paddings = new Padding(0, 5, 0, 0);
            textBox_request_headers.RightBracket = ')';
            textBox_request_headers.RightBracket2 = '}';
            textBox_request_headers.SelectionColor = Color.FromArgb(60, 255, 140, 0);
            textBox_request_headers.ServiceColors = (FastColoredTextBoxNS.ServiceColors)resources.GetObject("textBox_request_headers.ServiceColors");
            textBox_request_headers.ServiceLinesColor = SystemColors.ButtonFace;
            textBox_request_headers.Size = new Size(1311, 465);
            textBox_request_headers.TabIndex = 0;
            textBox_request_headers.WordWrap = true;
            textBox_request_headers.WordWrapIndent = 1;
            textBox_request_headers.Zoom = 100;
            textBox_request_headers.TextChanged += TextBox_request_headers_TextChanged;
            // 
            // textBox_request_body
            // 
            textBox_request_body.AutoCompleteBracketsList = new char[]
    {
    '(',
    ')',
    '{',
    '}',
    '[',
    ']',
    '"',
    '"',
    '\'',
    '\''
    };
            autocompleteMenu1.SetAutocompleteMenu(textBox_request_body, null);
            textBox_request_body.AutoIndentCharsPatterns = "^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;=]+);\r\n^\\s*(case|default)\\s*[^:]*(?<range>:)\\s*(?<range>[^;]+);";
            textBox_request_body.AutoScrollMinSize = new Size(0, 22);
            textBox_request_body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            textBox_request_body.BackBrush = null;
            textBox_request_body.CharHeight = 17;
            textBox_request_body.CharWidth = 8;
            textBox_request_body.Cursor = Cursors.IBeam;
            textBox_request_body.DefaultMarkerSize = 8;
            textBox_request_body.DisabledColor = Color.FromArgb(100, 180, 180, 180);
            textBox_request_body.Dock = DockStyle.Fill;
            textBox_request_body.Font = new Font("Consolas", 9F);
            textBox_request_body.HighlightFoldingIndicator = false;
            textBox_request_body.Hotkeys = resources.GetString("textBox_request_body.Hotkeys");
            textBox_request_body.IsReplaceMode = false;
            textBox_request_body.LeftBracket = '(';
            textBox_request_body.LeftBracket2 = '{';
            textBox_request_body.LineNumberColor = SystemColors.ButtonShadow;
            textBox_request_body.Location = new Point(3, 3);
            textBox_request_body.Margin = new Padding(4);
            textBox_request_body.Name = "textBox_request_body";
            textBox_request_body.Paddings = new Padding(0, 5, 0, 0);
            textBox_request_body.RightBracket = ')';
            textBox_request_body.RightBracket2 = '}';
            textBox_request_body.SelectionColor = Color.FromArgb(60, 255, 140, 0);
            textBox_request_body.ServiceColors = (FastColoredTextBoxNS.ServiceColors)resources.GetObject("textBox_request_body.ServiceColors");
            textBox_request_body.ServiceLinesColor = SystemColors.ButtonFace;
            textBox_request_body.Size = new Size(1311, 465);
            textBox_request_body.TabIndex = 3;
            textBox_request_body.WordWrap = true;
            textBox_request_body.WordWrapIndent = 1;
            textBox_request_body.Zoom = 100;
            // 
            // splitContainer6_main_right
            // 
            splitContainer6_main_right.Dock = DockStyle.Fill;
            splitContainer6_main_right.FixedPanel = FixedPanel.Panel1;
            splitContainer6_main_right.IsSplitterFixed = true;
            splitContainer6_main_right.Location = new Point(0, 0);
            splitContainer6_main_right.Margin = new Padding(0);
            splitContainer6_main_right.Name = "splitContainer6_main_right";
            splitContainer6_main_right.Orientation = Orientation.Horizontal;
            // 
            // splitContainer6_main_right.Panel1
            // 
            splitContainer6_main_right.Panel1.Controls.Add(label_group);
            splitContainer6_main_right.Panel1.Controls.Add(button_saveGroup);
            splitContainer6_main_right.Panel1.Controls.Add(label_displayed_Id);
            splitContainer6_main_right.Panel1.Controls.Add(comboBox_group);
            splitContainer6_main_right.Panel1.Controls.Add(button_request_send);
            splitContainer6_main_right.Panel1.Controls.Add(textBox_request_url);
            splitContainer6_main_right.Panel1.Controls.Add(comboBox_certificates);
            splitContainer6_main_right.Panel1.Controls.Add(comboBox_http_method);
            splitContainer6_main_right.Panel1MinSize = 78;
            // 
            // splitContainer6_main_right.Panel2
            // 
            splitContainer6_main_right.Panel2.Controls.Add(splitContainer5_reqres);
            splitContainer6_main_right.Size = new Size(1325, 1393);
            splitContainer6_main_right.SplitterDistance = 78;
            splitContainer6_main_right.SplitterWidth = 1;
            splitContainer6_main_right.TabIndex = 8;
            // 
            // label_group
            // 
            label_group.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            label_group.AutoSize = true;
            label_group.Font = new Font("Segoe UI", 7.8F);
            label_group.Location = new Point(97, 44);
            label_group.Margin = new Padding(4, 0, 4, 0);
            label_group.Name = "label_group";
            label_group.Size = new Size(48, 17);
            label_group.TabIndex = 12;
            label_group.Text = "Group:";
            // 
            // button_saveGroup
            // 
            button_saveGroup.AutoSize = true;
            button_saveGroup.Image = (Image)resources.GetObject("button_saveGroup.Image");
            button_saveGroup.Location = new Point(283, 38);
            button_saveGroup.Name = "button_saveGroup";
            button_saveGroup.Size = new Size(30, 28);
            button_saveGroup.TabIndex = 11;
            button_saveGroup.Click += Button_saveGroup_Click;
            // 
            // label_displayed_Id
            // 
            label_displayed_Id.Location = new Point(929, 50);
            label_displayed_Id.Margin = new Padding(2, 0, 2, 0);
            label_displayed_Id.Name = "label_displayed_Id";
            label_displayed_Id.Size = new Size(19, 21);
            label_displayed_Id.TabIndex = 10;
            label_displayed_Id.Visible = false;
            // 
            // comboBox_group
            // 
            comboBox_group.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            comboBox_group.FlatStyle = FlatStyle.Flat;
            comboBox_group.Font = new Font("Segoe UI", 8F);
            comboBox_group.FormattingEnabled = true;
            comboBox_group.Location = new Point(148, 40);
            comboBox_group.Margin = new Padding(4);
            comboBox_group.Name = "comboBox_group";
            comboBox_group.Size = new Size(130, 25);
            comboBox_group.TabIndex = 9;
            // 
            // textBox_request_url
            // 
            textBox_request_url.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBox_request_url.AutoCompleteBracketsList = new char[]
    {
    '(',
    ')',
    '{',
    '}',
    '[',
    ']',
    '"',
    '"',
    '\'',
    '\''
    };
            autocompleteMenu1.SetAutocompleteMenu(textBox_request_url, autocompleteMenu1);
            textBox_request_url.AutoIndentCharsPatterns = "";
            textBox_request_url.AutoScrollMinSize = new Size(6, 21);
            textBox_request_url.BackBrush = null;
            textBox_request_url.CharHeight = 17;
            textBox_request_url.CharWidth = 8;
            textBox_request_url.CommentPrefix = null;
            textBox_request_url.Cursor = Cursors.IBeam;
            textBox_request_url.DefaultMarkerSize = 8;
            textBox_request_url.DisabledColor = Color.FromArgb(100, 180, 180, 180);
            textBox_request_url.Font = new Font("Consolas", 9F);
            textBox_request_url.HighlightFoldingIndicator = false;
            textBox_request_url.HighlightingRangeType = FastColoredTextBoxNS.HighlightingRangeType.AllTextRange;
            textBox_request_url.Hotkeys = resources.GetString("textBox_request_url.Hotkeys");
            textBox_request_url.IsReplaceMode = false;
            textBox_request_url.LeftBracket = '<';
            textBox_request_url.LeftBracket2 = '(';
            textBox_request_url.Location = new Point(4, 5);
            textBox_request_url.Margin = new Padding(4);
            textBox_request_url.Multiline = false;
            textBox_request_url.Name = "textBox_request_url";
            textBox_request_url.Paddings = new Padding(4, 4, 0, 0);
            textBox_request_url.RightBracket = '>';
            textBox_request_url.RightBracket2 = ')';
            textBox_request_url.SelectionColor = Color.FromArgb(60, 255, 140, 0);
            textBox_request_url.ServiceColors = (FastColoredTextBoxNS.ServiceColors)resources.GetObject("textBox_request_url.ServiceColors");
            textBox_request_url.ShowLineNumbers = false;
            textBox_request_url.ShowScrollBars = false;
            textBox_request_url.Size = new Size(1317, 26);
            textBox_request_url.TabIndex = 4;
            textBox_request_url.Zoom = 100;
            textBox_request_url.TextChanged += TextBox_request_url_TextChanged;
            // 
            // comboBox_certificates
            // 
            comboBox_certificates.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            comboBox_certificates.FlatStyle = FlatStyle.Flat;
            comboBox_certificates.Font = new Font("Segoe UI", 8F);
            comboBox_certificates.FormattingEnabled = true;
            comboBox_certificates.Location = new Point(331, 40);
            comboBox_certificates.Margin = new Padding(4);
            comboBox_certificates.Name = "comboBox_certificates";
            comboBox_certificates.Size = new Size(267, 25);
            comboBox_certificates.TabIndex = 2;
            // 
            // comboBox_http_method
            // 
            comboBox_http_method.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            comboBox_http_method.BackColor = SystemColors.ButtonHighlight;
            comboBox_http_method.FlatStyle = FlatStyle.Flat;
            comboBox_http_method.Font = new Font("Segoe UI", 7.8F);
            comboBox_http_method.FormattingEnabled = true;
            comboBox_http_method.Items.AddRange(new object[] { "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD", "TRACE" });
            comboBox_http_method.Location = new Point(4, 40);
            comboBox_http_method.Margin = new Padding(4);
            comboBox_http_method.Name = "comboBox_http_method";
            comboBox_http_method.Size = new Size(78, 25);
            comboBox_http_method.TabIndex = 1;
            comboBox_http_method.Text = "GET";
            // 
            // splitContainer5_reqres
            // 
            splitContainer5_reqres.Dock = DockStyle.Fill;
            splitContainer5_reqres.FixedPanel = FixedPanel.Panel1;
            splitContainer5_reqres.Location = new Point(0, 0);
            splitContainer5_reqres.Margin = new Padding(2);
            splitContainer5_reqres.Name = "splitContainer5_reqres";
            splitContainer5_reqres.Orientation = Orientation.Horizontal;
            // 
            // splitContainer5_reqres.Panel1
            // 
            splitContainer5_reqres.Panel1.Controls.Add(tabControl1);
            // 
            // splitContainer5_reqres.Panel2
            // 
            splitContainer5_reqres.Panel2.Controls.Add(tabControl_response);
            splitContainer5_reqres.Panel2.Controls.Add(statusStrip_response_stats);
            splitContainer5_reqres.Size = new Size(1325, 1314);
            splitContainer5_reqres.SplitterDistance = 504;
            splitContainer5_reqres.TabIndex = 7;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage_request_body);
            tabControl1.Controls.Add(tabPage_request_header);
            tabControl1.Dock = DockStyle.Fill;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Margin = new Padding(1);
            tabControl1.Name = "tabControl1";
            tabControl1.Padding = new Point(30, 3);
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(1325, 504);
            tabControl1.TabIndex = 5;
            // 
            // tabPage_request_body
            // 
            tabPage_request_body.Controls.Add(textBox_request_body);
            tabPage_request_body.Location = new Point(4, 29);
            tabPage_request_body.Name = "tabPage_request_body";
            tabPage_request_body.Padding = new Padding(3);
            tabPage_request_body.Size = new Size(1317, 471);
            tabPage_request_body.TabIndex = 0;
            tabPage_request_body.Text = "Request body";
            tabPage_request_body.UseVisualStyleBackColor = true;
            // 
            // tabPage_request_header
            // 
            tabPage_request_header.Controls.Add(textBox_request_headers);
            tabPage_request_header.Location = new Point(4, 29);
            tabPage_request_header.Name = "tabPage_request_header";
            tabPage_request_header.Padding = new Padding(3);
            tabPage_request_header.Size = new Size(1317, 471);
            tabPage_request_header.TabIndex = 1;
            tabPage_request_header.Text = "Request header";
            tabPage_request_header.UseVisualStyleBackColor = true;
            // 
            // tabControl_response
            // 
            tabControl_response.Controls.Add(tabPage_response_body);
            tabControl_response.Controls.Add(tabPage_response_headers);
            tabControl_response.Controls.Add(tabPage_statistics_information);
            tabControl_response.Dock = DockStyle.Fill;
            tabControl_response.Location = new Point(0, 0);
            tabControl_response.Margin = new Padding(1);
            tabControl_response.Multiline = true;
            tabControl_response.Name = "tabControl_response";
            tabControl_response.Padding = new Point(30, 3);
            tabControl_response.SelectedIndex = 0;
            tabControl_response.Size = new Size(1325, 776);
            tabControl_response.TabIndex = 4;
            // 
            // tabPage_response_body
            // 
            tabPage_response_body.BackColor = SystemColors.Control;
            tabPage_response_body.BackgroundImageLayout = ImageLayout.None;
            tabPage_response_body.Controls.Add(textBox_response_body);
            tabPage_response_body.Location = new Point(4, 29);
            tabPage_response_body.Margin = new Padding(0);
            tabPage_response_body.Name = "tabPage_response_body";
            tabPage_response_body.Padding = new Padding(3);
            tabPage_response_body.Size = new Size(1317, 743);
            tabPage_response_body.TabIndex = 0;
            tabPage_response_body.Text = "Response body";
            // 
            // textBox_response_body
            // 
            textBox_response_body.AutoCompleteBracketsList = new char[]
    {
    '(',
    ')',
    '{',
    '}',
    '[',
    ']',
    '"',
    '"',
    '\'',
    '\''
    };
            autocompleteMenu1.SetAutocompleteMenu(textBox_response_body, null);
            textBox_response_body.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;]+);\r\n";
            textBox_response_body.AutoScrollMinSize = new Size(0, 23);
            textBox_response_body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            textBox_response_body.BackBrush = null;
            textBox_response_body.BackColor = Color.WhiteSmoke;
            textBox_response_body.CharHeight = 18;
            textBox_response_body.CharWidth = 10;
            textBox_response_body.Cursor = Cursors.IBeam;
            textBox_response_body.DefaultMarkerSize = 8;
            textBox_response_body.DisabledColor = Color.FromArgb(100, 180, 180, 180);
            textBox_response_body.Dock = DockStyle.Fill;
            textBox_response_body.HighlightFoldingIndicator = false;
            textBox_response_body.Hotkeys = resources.GetString("textBox_response_body.Hotkeys");
            textBox_response_body.IsReplaceMode = false;
            textBox_response_body.LeftBracket = '(';
            textBox_response_body.LeftBracket2 = '{';
            textBox_response_body.LineNumberColor = SystemColors.ButtonShadow;
            textBox_response_body.Location = new Point(3, 3);
            textBox_response_body.Margin = new Padding(4);
            textBox_response_body.Name = "textBox_response_body";
            textBox_response_body.Paddings = new Padding(0, 5, 0, 0);
            textBox_response_body.ReadOnly = true;
            textBox_response_body.RightBracket = ')';
            textBox_response_body.RightBracket2 = ')';
            textBox_response_body.SelectionColor = Color.FromArgb(60, 255, 140, 0);
            textBox_response_body.ServiceColors = (FastColoredTextBoxNS.ServiceColors)resources.GetObject("textBox_response_body.ServiceColors");
            textBox_response_body.ServiceLinesColor = SystemColors.ButtonFace;
            textBox_response_body.Size = new Size(1311, 737);
            textBox_response_body.TabIndex = 2;
            textBox_response_body.WordWrap = true;
            textBox_response_body.WordWrapIndent = 1;
            textBox_response_body.Zoom = 100;
            // 
            // tabPage_response_headers
            // 
            tabPage_response_headers.BackgroundImageLayout = ImageLayout.None;
            tabPage_response_headers.Controls.Add(textBox_response_headers);
            tabPage_response_headers.Location = new Point(4, 29);
            tabPage_response_headers.Margin = new Padding(0);
            tabPage_response_headers.Name = "tabPage_response_headers";
            tabPage_response_headers.Padding = new Padding(3);
            tabPage_response_headers.Size = new Size(1317, 743);
            tabPage_response_headers.TabIndex = 1;
            tabPage_response_headers.Text = "Response headers";
            // 
            // textBox_response_headers
            // 
            textBox_response_headers.AutoCompleteBracketsList = new char[]
    {
    '(',
    ')',
    '{',
    '}',
    '[',
    ']',
    '"',
    '"',
    '\'',
    '\''
    };
            autocompleteMenu1.SetAutocompleteMenu(textBox_response_headers, null);
            textBox_response_headers.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;]+);\r\n";
            textBox_response_headers.AutoScrollMinSize = new Size(0, 22);
            textBox_response_headers.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            textBox_response_headers.BackBrush = null;
            textBox_response_headers.BackColor = Color.WhiteSmoke;
            textBox_response_headers.CharHeight = 17;
            textBox_response_headers.CharWidth = 8;
            textBox_response_headers.CurrentLineColor = Color.Gainsboro;
            textBox_response_headers.Cursor = Cursors.IBeam;
            textBox_response_headers.DefaultMarkerSize = 8;
            textBox_response_headers.DisabledColor = Color.FromArgb(100, 180, 180, 180);
            textBox_response_headers.Dock = DockStyle.Fill;
            textBox_response_headers.Font = new Font("Consolas", 9F);
            textBox_response_headers.HighlightFoldingIndicator = false;
            textBox_response_headers.Hotkeys = resources.GetString("textBox_response_headers.Hotkeys");
            textBox_response_headers.IsReplaceMode = false;
            textBox_response_headers.LeftBracket = '(';
            textBox_response_headers.LeftBracket2 = '{';
            textBox_response_headers.LineNumberColor = SystemColors.ButtonShadow;
            textBox_response_headers.Location = new Point(3, 3);
            textBox_response_headers.Margin = new Padding(4);
            textBox_response_headers.Name = "textBox_response_headers";
            textBox_response_headers.Paddings = new Padding(0, 5, 0, 0);
            textBox_response_headers.ReadOnly = true;
            textBox_response_headers.RightBracket = ')';
            textBox_response_headers.RightBracket2 = '}';
            textBox_response_headers.SelectionColor = Color.FromArgb(60, 255, 140, 0);
            textBox_response_headers.ServiceColors = (FastColoredTextBoxNS.ServiceColors)resources.GetObject("textBox_response_headers.ServiceColors");
            textBox_response_headers.ServiceLinesColor = SystemColors.ButtonFace;
            textBox_response_headers.Size = new Size(1311, 737);
            textBox_response_headers.TabIndex = 3;
            textBox_response_headers.WordWrap = true;
            textBox_response_headers.WordWrapIndent = 1;
            textBox_response_headers.Zoom = 100;
            textBox_response_headers.TextChanged += TextBox_response_headers_TextChanged;
            textBox_response_headers.MouseDown += TextBox_response_headers_MouseDown;
            textBox_response_headers.MouseMove += TextBox_response_headers_MouseMove;
            // 
            // tabPage_statistics_information
            // 
            tabPage_statistics_information.Controls.Add(listView1);
            tabPage_statistics_information.Location = new Point(4, 29);
            tabPage_statistics_information.Name = "tabPage_statistics_information";
            tabPage_statistics_information.Size = new Size(1317, 743);
            tabPage_statistics_information.TabIndex = 2;
            tabPage_statistics_information.Text = "Statistics";
            tabPage_statistics_information.UseVisualStyleBackColor = true;
            // 
            // listView1
            // 
            listView1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            listView1.Location = new Point(6, 18);
            listView1.Name = "listView1";
            listView1.Size = new Size(1302, 499);
            listView1.TabIndex = 0;
            listView1.UseCompatibleStateImageBehavior = false;
            // 
            // statusStrip_response_stats
            // 
            statusStrip_response_stats.ImageScalingSize = new Size(20, 20);
            statusStrip_response_stats.Items.AddRange(new ToolStripItem[] { toolStripDropDownButton1, toolStripDropDownButton_text_utils, toolStripDropDownButton_request, toolStripDropDownButton_clearAll, toolStripDropDownButton_response_stats_certificate, toolStripStatusLabel_response_stats_http_version, toolStripStatusLabel_response_stats_datetime, toolStripStatusLabel_response_stats_response_time, toolStripStatusLabel_response_stats_certificate });
            statusStrip_response_stats.Location = new Point(0, 776);
            statusStrip_response_stats.Name = "statusStrip_response_stats";
            statusStrip_response_stats.ShowItemToolTips = true;
            statusStrip_response_stats.Size = new Size(1325, 30);
            statusStrip_response_stats.TabIndex = 5;
            statusStrip_response_stats.Text = "statusStrip1";
            // 
            // toolStripDropDownButton1
            // 
            toolStripDropDownButton1.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripDropDownButton1.DropDownItems.AddRange(new ToolStripItem[] { copyToToolStripMenuItem });
            toolStripDropDownButton1.ImageTransparentColor = Color.Magenta;
            toolStripDropDownButton1.Name = "toolStripDropDownButton1";
            toolStripDropDownButton1.Size = new Size(66, 28);
            toolStripDropDownButton1.Text = "Profile";
            // 
            // copyToToolStripMenuItem
            // 
            copyToToolStripMenuItem.Name = "copyToToolStripMenuItem";
            copyToToolStripMenuItem.Size = new Size(144, 26);
            copyToToolStripMenuItem.Text = "Copy to";
            copyToToolStripMenuItem.DropDownItemClicked += CopyToToolStripMenuItem_DropDownItemClicked;
            // 
            // toolStripDropDownButton_text_utils
            // 
            toolStripDropDownButton_text_utils.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripDropDownButton_text_utils.Image = (Image)resources.GetObject("toolStripDropDownButton_text_utils.Image");
            toolStripDropDownButton_text_utils.ImageScaling = ToolStripItemImageScaling.None;
            toolStripDropDownButton_text_utils.ImageTransparentColor = Color.Magenta;
            toolStripDropDownButton_text_utils.Name = "toolStripDropDownButton_text_utils";
            toolStripDropDownButton_text_utils.Padding = new Padding(5, 0, 0, 0);
            toolStripDropDownButton_text_utils.ShowDropDownArrow = false;
            toolStripDropDownButton_text_utils.Size = new Size(25, 28);
            toolStripDropDownButton_text_utils.Text = "toolStripDropDownButton2";
            toolStripDropDownButton_text_utils.ToolTipText = "Text utilities";
            toolStripDropDownButton_text_utils.Click += Button_text_utils_Click;
            // 
            // toolStripDropDownButton_request
            // 
            toolStripDropDownButton_request.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripDropDownButton_request.DropDownItems.AddRange(new ToolStripItem[] { httpVersionToolStripMenuItem, repeatToolStripMenuItem });
            toolStripDropDownButton_request.Image = (Image)resources.GetObject("toolStripDropDownButton_request.Image");
            toolStripDropDownButton_request.ImageTransparentColor = Color.Magenta;
            toolStripDropDownButton_request.Name = "toolStripDropDownButton_request";
            toolStripDropDownButton_request.Padding = new Padding(5, 0, 0, 0);
            toolStripDropDownButton_request.Size = new Size(81, 28);
            toolStripDropDownButton_request.Text = "Request";
            // 
            // httpVersionToolStripMenuItem
            // 
            httpVersionToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripComboBox_http_version });
            httpVersionToolStripMenuItem.Name = "httpVersionToolStripMenuItem";
            httpVersionToolStripMenuItem.Size = new Size(179, 26);
            httpVersionToolStripMenuItem.Text = "HTTP Version";
            // 
            // toolStripComboBox_http_version
            // 
            toolStripComboBox_http_version.Items.AddRange(new object[] { "HTTP 1.0", "HTTP 1.1", "HTTP 2.0" });
            toolStripComboBox_http_version.Name = "toolStripComboBox_http_version";
            toolStripComboBox_http_version.Size = new Size(121, 28);
            toolStripComboBox_http_version.Text = "HTTP 1.1";
            // 
            // repeatToolStripMenuItem
            // 
            repeatToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripTextBox_repeat });
            repeatToolStripMenuItem.Name = "repeatToolStripMenuItem";
            repeatToolStripMenuItem.Size = new Size(179, 26);
            repeatToolStripMenuItem.Text = "Repeat";
            // 
            // toolStripTextBox_repeat
            // 
            toolStripTextBox_repeat.Name = "toolStripTextBox_repeat";
            toolStripTextBox_repeat.Size = new Size(100, 27);
            toolStripTextBox_repeat.Text = "1";
            // 
            // toolStripDropDownButton_clearAll
            // 
            toolStripDropDownButton_clearAll.AccessibleDescription = "Clear request and response forms";
            toolStripDropDownButton_clearAll.DisplayStyle = ToolStripItemDisplayStyle.Image;
            toolStripDropDownButton_clearAll.Image = (Image)resources.GetObject("toolStripDropDownButton_clearAll.Image");
            toolStripDropDownButton_clearAll.ImageScaling = ToolStripItemImageScaling.None;
            toolStripDropDownButton_clearAll.ImageTransparentColor = Color.Magenta;
            toolStripDropDownButton_clearAll.Name = "toolStripDropDownButton_clearAll";
            toolStripDropDownButton_clearAll.Padding = new Padding(5, 0, 0, 0);
            toolStripDropDownButton_clearAll.ShowDropDownArrow = false;
            toolStripDropDownButton_clearAll.Size = new Size(33, 28);
            toolStripDropDownButton_clearAll.Text = "toolStripDropDownButton2";
            toolStripDropDownButton_clearAll.ToolTipText = "Clear request and response forms";
            toolStripDropDownButton_clearAll.Click += Button_clearAll_Click;
            // 
            // toolStripDropDownButton_response_stats_certificate
            // 
            toolStripDropDownButton_response_stats_certificate.DisplayStyle = ToolStripItemDisplayStyle.Text;
            toolStripDropDownButton_response_stats_certificate.Image = (Image)resources.GetObject("toolStripDropDownButton_response_stats_certificate.Image");
            toolStripDropDownButton_response_stats_certificate.ImageTransparentColor = Color.Magenta;
            toolStripDropDownButton_response_stats_certificate.Name = "toolStripDropDownButton_response_stats_certificate";
            toolStripDropDownButton_response_stats_certificate.Size = new Size(134, 28);
            toolStripDropDownButton_response_stats_certificate.Text = "Server certificate";
            // 
            // toolStripStatusLabel_response_stats_http_version
            // 
            toolStripStatusLabel_response_stats_http_version.Name = "toolStripStatusLabel_response_stats_http_version";
            toolStripStatusLabel_response_stats_http_version.Size = new Size(0, 24);
            // 
            // toolStripStatusLabel_response_stats_datetime
            // 
            toolStripStatusLabel_response_stats_datetime.Name = "toolStripStatusLabel_response_stats_datetime";
            toolStripStatusLabel_response_stats_datetime.Size = new Size(0, 24);
            // 
            // toolStripStatusLabel_response_stats_response_time
            // 
            toolStripStatusLabel_response_stats_response_time.Name = "toolStripStatusLabel_response_stats_response_time";
            toolStripStatusLabel_response_stats_response_time.Size = new Size(0, 24);
            // 
            // toolStripStatusLabel_response_stats_certificate
            // 
            toolStripStatusLabel_response_stats_certificate.BackColor = Color.IndianRed;
            toolStripStatusLabel_response_stats_certificate.Name = "toolStripStatusLabel_response_stats_certificate";
            toolStripStatusLabel_response_stats_certificate.Size = new Size(0, 24);
            // 
            // dataGridView1
            // 
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridViewCellStyle1.BackColor = Color.PaleGoldenrod;
            dataGridView1.AlternatingRowsDefaultCellStyle = dataGridViewCellStyle1;
            dataGridView1.BackgroundColor = SystemColors.Control;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.CellBorderStyle = DataGridViewCellBorderStyle.None;
            dataGridView1.ColumnHeadersHeight = 29;
            dataGridView1.ColumnHeadersVisible = false;
            dataGridView1.ContextMenuStrip = contextMenuStrip1;
            dataGridViewCellStyle2.Alignment = DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = SystemColors.Window;
            dataGridViewCellStyle2.Font = new Font("Segoe UI Semibold", 7.8F, FontStyle.Bold, GraphicsUnit.Point, 0);
            dataGridViewCellStyle2.ForeColor = SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = DataGridViewTriState.False;
            dataGridView1.DefaultCellStyle = dataGridViewCellStyle2;
            dataGridView1.Dock = DockStyle.Fill;
            dataGridView1.Location = new Point(0, 0);
            dataGridView1.Margin = new Padding(1);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.RowTemplate.Height = 25;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.Size = new Size(644, 1323);
            dataGridView1.TabIndex = 3;
            dataGridView1.VirtualMode = true;
            dataGridView1.CellPainting += DataGridView1_CellPainting;
            dataGridView1.CellValueChanged += DataGridView1_CellValueChanged;
            dataGridView1.RowContextMenuStripNeeded += dataGridView1_RowContextMenuStripNeeded;
            dataGridView1.RowEnter += DataGridView1_RowEnter;
            dataGridView1.UserDeletingRow += DataGridView1_UserDeletingRow;
            dataGridView1.KeyDown += DataGridView1_KeyDown;
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.ImageScalingSize = new Size(20, 20);
            contextMenuStrip1.Items.AddRange(new ToolStripItem[] { copyToToolStripMenuItem1 });
            contextMenuStrip1.MinimumSize = new Size(50, 50);
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.RenderMode = ToolStripRenderMode.Professional;
            contextMenuStrip1.Size = new Size(131, 50);
            contextMenuStrip1.Text = "Copy to";
            // 
            // copyToToolStripMenuItem1
            // 
            copyToToolStripMenuItem1.Name = "copyToToolStripMenuItem1";
            copyToToolStripMenuItem1.Size = new Size(130, 24);
            copyToToolStripMenuItem1.Text = "Copy to";
            copyToToolStripMenuItem1.DropDownItemClicked += copyToToolStripMenuItem1_DropDownItemClicked;
            // 
            // splitContainer1_main_form
            // 
            splitContainer1_main_form.Dock = DockStyle.Fill;
            splitContainer1_main_form.FixedPanel = FixedPanel.Panel1;
            splitContainer1_main_form.Location = new Point(0, 0);
            splitContainer1_main_form.Margin = new Padding(4);
            splitContainer1_main_form.Name = "splitContainer1_main_form";
            // 
            // splitContainer1_main_form.Panel1
            // 
            splitContainer1_main_form.Panel1.Controls.Add(tabControl2);
            // 
            // splitContainer1_main_form.Panel2
            // 
            splitContainer1_main_form.Panel2.Controls.Add(splitContainer6_main_right);
            splitContainer1_main_form.Size = new Size(1990, 1393);
            splitContainer1_main_form.SplitterDistance = 660;
            splitContainer1_main_form.SplitterWidth = 5;
            splitContainer1_main_form.TabIndex = 4;
            // 
            // tabControl2
            // 
            tabControl2.Alignment = TabAlignment.Bottom;
            tabControl2.Controls.Add(tabPage3);
            tabControl2.Controls.Add(tabPage4);
            tabControl2.Dock = DockStyle.Fill;
            tabControl2.Location = new Point(0, 0);
            tabControl2.Margin = new Padding(2);
            tabControl2.Name = "tabControl2";
            tabControl2.SelectedIndex = 0;
            tabControl2.Size = new Size(660, 1393);
            tabControl2.SizeMode = TabSizeMode.Fixed;
            tabControl2.TabIndex = 5;
            tabControl2.SelectedIndexChanged += TabControl2_SelectedIndexChanged;
            // 
            // tabPage3
            // 
            tabPage3.Controls.Add(splitContainer4);
            tabPage3.Location = new Point(4, 4);
            tabPage3.Margin = new Padding(4);
            tabPage3.Name = "tabPage3";
            tabPage3.Padding = new Padding(4);
            tabPage3.Size = new Size(652, 1360);
            tabPage3.TabIndex = 0;
            tabPage3.Text = "Sessions";
            tabPage3.UseVisualStyleBackColor = true;
            // 
            // splitContainer4
            // 
            splitContainer4.Dock = DockStyle.Fill;
            splitContainer4.Location = new Point(4, 4);
            splitContainer4.Name = "splitContainer4";
            splitContainer4.Orientation = Orientation.Horizontal;
            // 
            // splitContainer4.Panel1
            // 
            splitContainer4.Panel1.Controls.Add(textBox_filter);
            splitContainer4.Panel1.Controls.Add(comboBox_filter_group);
            // 
            // splitContainer4.Panel2
            // 
            splitContainer4.Panel2.Controls.Add(dataGridView1);
            splitContainer4.Size = new Size(644, 1352);
            splitContainer4.SplitterDistance = 25;
            splitContainer4.TabIndex = 11;
            // 
            // textBox_filter
            // 
            autocompleteMenu1.SetAutocompleteMenu(textBox_filter, null);
            textBox_filter.BorderStyle = BorderStyle.FixedSingle;
            textBox_filter.Dock = DockStyle.Fill;
            textBox_filter.Font = new Font("Segoe UI", 8F);
            textBox_filter.Location = new Point(0, 0);
            textBox_filter.Margin = new Padding(4);
            textBox_filter.Name = "textBox_filter";
            textBox_filter.PlaceholderText = "Filter";
            textBox_filter.Size = new Size(456, 25);
            textBox_filter.TabIndex = 4;
            textBox_filter.TextChanged += TextBox_filter_TextChanged;
            // 
            // comboBox_filter_group
            // 
            comboBox_filter_group.Dock = DockStyle.Right;
            comboBox_filter_group.Font = new Font("Segoe UI", 8F);
            comboBox_filter_group.FormattingEnabled = true;
            comboBox_filter_group.ItemHeight = 17;
            comboBox_filter_group.Location = new Point(456, 0);
            comboBox_filter_group.Margin = new Padding(5);
            comboBox_filter_group.Name = "comboBox_filter_group";
            comboBox_filter_group.Size = new Size(188, 25);
            comboBox_filter_group.TabIndex = 10;
            comboBox_filter_group.SelectedIndexChanged += ComboBox_filter_group_SelectedIndexChanged;
            // 
            // tabPage4
            // 
            tabPage4.Controls.Add(label7);
            tabPage4.Controls.Add(comboBox_settings_profiles);
            tabPage4.Controls.Add(groupBox1);
            tabPage4.Location = new Point(4, 4);
            tabPage4.Margin = new Padding(4);
            tabPage4.Name = "tabPage4";
            tabPage4.Padding = new Padding(4);
            tabPage4.Size = new Size(652, 1360);
            tabPage4.TabIndex = 1;
            tabPage4.Text = "Settings";
            tabPage4.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(18, 24);
            label7.Margin = new Padding(4, 0, 4, 0);
            label7.Name = "label7";
            label7.Size = new Size(108, 20);
            label7.TabIndex = 5;
            label7.Text = "Current profile:";
            // 
            // comboBox_settings_profiles
            // 
            comboBox_settings_profiles.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            comboBox_settings_profiles.FormattingEnabled = true;
            comboBox_settings_profiles.Location = new Point(138, 21);
            comboBox_settings_profiles.Margin = new Padding(4);
            comboBox_settings_profiles.Name = "comboBox_settings_profiles";
            comboBox_settings_profiles.Size = new Size(469, 28);
            comboBox_settings_profiles.TabIndex = 4;
            comboBox_settings_profiles.SelectedIndexChanged += ComboBox_settings_profiles_SelectedIndexChanged;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupBox1.Controls.Add(groupBox2);
            groupBox1.Controls.Add(label9);
            groupBox1.Controls.Add(textBox_profileName);
            groupBox1.Controls.Add(button_settings_import);
            groupBox1.Controls.Add(button_settings_export);
            groupBox1.Controls.Add(button_settings_insert);
            groupBox1.Controls.Add(button_settings_delete);
            groupBox1.Controls.Add(button_settings_save);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(textBox_cosmos_Endpoint);
            groupBox1.Controls.Add(label2);
            groupBox1.Location = new Point(18, 90);
            groupBox1.Margin = new Padding(4);
            groupBox1.Name = "groupBox1";
            groupBox1.Padding = new Padding(4);
            groupBox1.Size = new Size(616, 744);
            groupBox1.TabIndex = 2;
            groupBox1.TabStop = false;
            groupBox1.Text = "Database profile settings";
            // 
            // groupBox2
            // 
            groupBox2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupBox2.Controls.Add(button_blob_list);
            groupBox2.Controls.Add(listBox_blob);
            groupBox2.Controls.Add(textBox_blob_container);
            groupBox2.Controls.Add(button_blob);
            groupBox2.Controls.Add(label5);
            groupBox2.Controls.Add(textBox_blob_storage_account);
            groupBox2.Controls.Add(textBox_blob_sas_token);
            groupBox2.Controls.Add(label1);
            groupBox2.Controls.Add(label4);
            groupBox2.Location = new Point(20, 188);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(569, 429);
            groupBox2.TabIndex = 17;
            groupBox2.TabStop = false;
            groupBox2.Text = "Azure Blob Synchronization";
            // 
            // button_blob_list
            // 
            button_blob_list.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button_blob_list.Location = new Point(426, 372);
            button_blob_list.Margin = new Padding(4);
            button_blob_list.Name = "button_blob_list";
            button_blob_list.Size = new Size(124, 32);
            button_blob_list.TabIndex = 17;
            button_blob_list.Text = "List blobs";
            button_blob_list.UseVisualStyleBackColor = true;
            button_blob_list.Click += button_blob_list_Click;
            // 
            // listBox_blob
            // 
            listBox_blob.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            listBox_blob.BorderStyle = BorderStyle.FixedSingle;
            listBox_blob.FormattingEnabled = true;
            listBox_blob.Location = new Point(28, 167);
            listBox_blob.Name = "listBox_blob";
            listBox_blob.Size = new Size(522, 202);
            listBox_blob.TabIndex = 8;
            // 
            // textBox_blob_container
            // 
            textBox_blob_container.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            autocompleteMenu1.SetAutocompleteMenu(textBox_blob_container, null);
            textBox_blob_container.Location = new Point(166, 116);
            textBox_blob_container.Margin = new Padding(4);
            textBox_blob_container.Name = "textBox_blob_container";
            textBox_blob_container.Size = new Size(384, 27);
            textBox_blob_container.TabIndex = 13;
            // 
            // button_blob
            // 
            button_blob.Location = new Point(28, 372);
            button_blob.Margin = new Padding(4);
            button_blob.MinimumSize = new Size(230, 0);
            button_blob.Name = "button_blob";
            button_blob.Size = new Size(230, 32);
            button_blob.TabIndex = 7;
            button_blob.Text = "Sync local db with Azure blob";
            button_blob.UseVisualStyleBackColor = true;
            button_blob.Click += button_blob_Click;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(28, 49);
            label5.Margin = new Padding(4, 0, 4, 0);
            label5.Name = "label5";
            label5.Size = new Size(81, 20);
            label5.TabIndex = 16;
            label5.Text = "SAS Token:";
            // 
            // textBox_blob_storage_account
            // 
            textBox_blob_storage_account.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            autocompleteMenu1.SetAutocompleteMenu(textBox_blob_storage_account, null);
            textBox_blob_storage_account.Location = new Point(166, 81);
            textBox_blob_storage_account.Margin = new Padding(4);
            textBox_blob_storage_account.Name = "textBox_blob_storage_account";
            textBox_blob_storage_account.Size = new Size(384, 27);
            textBox_blob_storage_account.TabIndex = 11;
            // 
            // textBox_blob_sas_token
            // 
            textBox_blob_sas_token.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            autocompleteMenu1.SetAutocompleteMenu(textBox_blob_sas_token, null);
            textBox_blob_sas_token.Location = new Point(166, 46);
            textBox_blob_sas_token.Margin = new Padding(4);
            textBox_blob_sas_token.Name = "textBox_blob_sas_token";
            textBox_blob_sas_token.Size = new Size(384, 27);
            textBox_blob_sas_token.TabIndex = 15;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(28, 84);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(122, 20);
            label1.TabIndex = 12;
            label1.Text = "Storage Account:";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(28, 119);
            label4.Margin = new Padding(4, 0, 4, 0);
            label4.Name = "label4";
            label4.Size = new Size(76, 20);
            label4.TabIndex = 14;
            label4.Text = "Container:";
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(20, 88);
            label9.Margin = new Padding(4, 0, 4, 0);
            label9.Name = "label9";
            label9.Size = new Size(99, 20);
            label9.TabIndex = 10;
            label9.Text = "Profile Name:";
            // 
            // textBox_profileName
            // 
            textBox_profileName.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            autocompleteMenu1.SetAutocompleteMenu(textBox_profileName, null);
            textBox_profileName.Location = new Point(158, 84);
            textBox_profileName.Margin = new Padding(4);
            textBox_profileName.Name = "textBox_profileName";
            textBox_profileName.Size = new Size(431, 27);
            textBox_profileName.TabIndex = 9;
            // 
            // button_settings_import
            // 
            button_settings_import.Location = new Point(20, 653);
            button_settings_import.Margin = new Padding(4);
            button_settings_import.Name = "button_settings_import";
            button_settings_import.Size = new Size(93, 29);
            button_settings_import.TabIndex = 7;
            button_settings_import.Text = "Import settingsConn";
            button_settings_import.UseVisualStyleBackColor = true;
            button_settings_import.Click += button_settings_import_Click;
            // 
            // button_settings_export
            // 
            button_settings_export.Location = new Point(20, 696);
            button_settings_export.Margin = new Padding(4);
            button_settings_export.Name = "button_settings_export";
            button_settings_export.Size = new Size(93, 29);
            button_settings_export.TabIndex = 6;
            button_settings_export.Text = "Export settingsConn";
            button_settings_export.UseVisualStyleBackColor = true;
            button_settings_export.Click += button_settings_export_Click;
            // 
            // button_settings_insert
            // 
            button_settings_insert.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button_settings_insert.Location = new Point(368, 696);
            button_settings_insert.Margin = new Padding(4);
            button_settings_insert.Name = "button_settings_insert";
            button_settings_insert.Size = new Size(106, 29);
            button_settings_insert.TabIndex = 5;
            button_settings_insert.Text = "Add as new";
            button_settings_insert.UseVisualStyleBackColor = true;
            button_settings_insert.Click += button_settings_insert_Click;
            // 
            // button_settings_delete
            // 
            button_settings_delete.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button_settings_delete.Location = new Point(481, 696);
            button_settings_delete.Margin = new Padding(4);
            button_settings_delete.Name = "button_settings_delete";
            button_settings_delete.Size = new Size(106, 29);
            button_settings_delete.TabIndex = 4;
            button_settings_delete.Text = "Delete";
            button_settings_delete.UseVisualStyleBackColor = true;
            button_settings_delete.Click += button_settings_delete_Click;
            // 
            // button_settings_save
            // 
            button_settings_save.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button_settings_save.Location = new Point(368, 653);
            button_settings_save.Margin = new Padding(4);
            button_settings_save.Name = "button_settings_save";
            button_settings_save.Size = new Size(220, 35);
            button_settings_save.TabIndex = 3;
            button_settings_save.Text = "Update";
            button_settings_save.UseVisualStyleBackColor = true;
            button_settings_save.Click += button_settings_save_Click;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(20, 122);
            label3.Margin = new Padding(4, 0, 4, 0);
            label3.Name = "label3";
            label3.Size = new Size(91, 20);
            label3.TabIndex = 2;
            label3.Text = "Db file path:";
            // 
            // textBox_cosmos_Endpoint
            // 
            textBox_cosmos_Endpoint.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            autocompleteMenu1.SetAutocompleteMenu(textBox_cosmos_Endpoint, null);
            textBox_cosmos_Endpoint.Location = new Point(158, 119);
            textBox_cosmos_Endpoint.Margin = new Padding(4);
            textBox_cosmos_Endpoint.Name = "textBox_cosmos_Endpoint";
            textBox_cosmos_Endpoint.Size = new Size(430, 27);
            textBox_cosmos_Endpoint.TabIndex = 0;
            // 
            // label2
            // 
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label2.AutoEllipsis = true;
            label2.Font = new Font("Segoe UI", 9F, FontStyle.Italic);
            label2.Location = new Point(10, 32);
            label2.Margin = new Padding(6, 0, 6, 0);
            label2.Name = "label2";
            label2.Padding = new Padding(6, 0, 6, 0);
            label2.Size = new Size(596, 28);
            label2.TabIndex = 1;
            label2.Text = "Create multiple profiles so you can have multiple databases.";
            label2.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // autocompleteMenu1
            // 
            autocompleteMenu1.Colors = (AutocompleteMenuNS.Colors)resources.GetObject("autocompleteMenu1.Colors");
            autocompleteMenu1.Font = new Font("Microsoft Sans Serif", 9F);
            autocompleteMenu1.ImageList = null;
            autocompleteMenu1.TargetControlWrapper = null;
            // 
            // Form1
            // 
            ClientSize = new Size(1990, 1393);
            Controls.Add(splitContainer1_main_form);
            DoubleBuffered = true;
            Icon = (Icon)resources.GetObject("$this.Icon");
            Margin = new Padding(4);
            MinimumSize = new Size(1280, 768);
            Name = "Form1";
            SizeGripStyle = SizeGripStyle.Show;
            StartPosition = FormStartPosition.Manual;
            FormClosing += Form1_FormClosing;
            ((System.ComponentModel.ISupportInitialize)textBox_request_headers).EndInit();
            ((System.ComponentModel.ISupportInitialize)textBox_request_body).EndInit();
            splitContainer6_main_right.Panel1.ResumeLayout(false);
            splitContainer6_main_right.Panel1.PerformLayout();
            splitContainer6_main_right.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer6_main_right).EndInit();
            splitContainer6_main_right.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)textBox_request_url).EndInit();
            splitContainer5_reqres.Panel1.ResumeLayout(false);
            splitContainer5_reqres.Panel2.ResumeLayout(false);
            splitContainer5_reqres.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)splitContainer5_reqres).EndInit();
            splitContainer5_reqres.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabPage_request_body.ResumeLayout(false);
            tabPage_request_header.ResumeLayout(false);
            tabControl_response.ResumeLayout(false);
            tabPage_response_body.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)textBox_response_body).EndInit();
            tabPage_response_headers.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)textBox_response_headers).EndInit();
            tabPage_statistics_information.ResumeLayout(false);
            statusStrip_response_stats.ResumeLayout(false);
            statusStrip_response_stats.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            contextMenuStrip1.ResumeLayout(false);
            splitContainer1_main_form.Panel1.ResumeLayout(false);
            splitContainer1_main_form.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1_main_form).EndInit();
            splitContainer1_main_form.ResumeLayout(false);
            tabControl2.ResumeLayout(false);
            tabPage3.ResumeLayout(false);
            splitContainer4.Panel1.ResumeLayout(false);
            splitContainer4.Panel1.PerformLayout();
            splitContainer4.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer4).EndInit();
            splitContainer4.ResumeLayout(false);
            tabPage4.ResumeLayout(false);
            tabPage4.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button button_request_send;
        private System.Windows.Forms.ComboBox comboBox_http_method;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.SplitContainer splitContainer1_main_form;
        private System.Windows.Forms.ComboBox comboBox_certificates;
        private FastColoredTextBoxNS.FastColoredTextBox textBox_request_url;
        private System.Windows.Forms.TextBox textBox_filter;
        private System.Windows.Forms.TabControl tabControl2;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox_cosmos_Endpoint;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button button_settings_save;
        private System.Windows.Forms.Button button_settings_delete;
        private System.Windows.Forms.ComboBox comboBox_settings_profiles;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button button_settings_insert;
        private System.Windows.Forms.Button button_settings_export;
        private System.Windows.Forms.Button button_settings_import;
        private FastColoredTextBoxNS.FastColoredTextBox textBox_response_headers;
        private FastColoredTextBoxNS.FastColoredTextBox textBox_response_body;
        private System.Windows.Forms.SplitContainer splitContainer6_main_right;
        private System.Windows.Forms.SplitContainer splitContainer5_reqres;
        private AutocompleteMenuNS.AutocompleteMenu autocompleteMenu1;
        private System.Windows.Forms.StatusStrip statusStrip_response_stats;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel_response_stats_http_version;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel_response_stats_datetime;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel_response_stats_response_time;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel_response_stats_certificate;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton_response_stats_certificate;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox textBox_profileName;
        private System.Windows.Forms.ToolStripDropDownButton toolStripDropDownButton1;
        private System.Windows.Forms.ToolStripMenuItem copyToToolStripMenuItem;
        private System.Windows.Forms.ComboBox comboBox_group;
        private System.Windows.Forms.Label label_displayed_Id;
        private System.Windows.Forms.Button button_saveGroup;
        private System.Windows.Forms.Label label_group;
        private FastColoredTextBoxNS.FastColoredTextBox textBox_request_body;
        private FastColoredTextBoxNS.FastColoredTextBox textBox_request_headers;
        private System.Windows.Forms.ComboBox comboBox_filter_group;
        private System.Windows.Forms.SplitContainer splitContainer4;
        private System.Windows.Forms.TabControl tabControl_response;
        private System.Windows.Forms.TabPage tabPage_response_body;
        private System.Windows.Forms.TabPage tabPage_response_headers;
        private System.Windows.Forms.Button button_blob;
        private System.Windows.Forms.ListBox listBox_blob;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TextBox textBox_blob_container;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox textBox_blob_storage_account;
        private System.Windows.Forms.TextBox textBox_blob_sas_token;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TabPage tabPage_statistics_information;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.Button button_blob_list;
        private TabControl tabControl1;
        private TabPage tabPage_request_body;
        private TabPage tabPage_request_header;
        private ToolStripDropDownButton toolStripDropDownButton_clearAll;
        private ToolStripDropDownButton toolStripDropDownButton_text_utils;
        private ToolStripDropDownButton toolStripDropDownButton_request;
        private ToolStripMenuItem httpVersionToolStripMenuItem;
        private ToolStripComboBox toolStripComboBox_http_version;
        private ToolStripMenuItem repeatToolStripMenuItem;
        private ToolStripTextBox toolStripTextBox_repeat;
        private ContextMenuStrip contextMenuStrip1;
        private ToolStripMenuItem copyToToolStripMenuItem1;
    }
}

