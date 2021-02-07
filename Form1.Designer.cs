
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.button_request_send = new System.Windows.Forms.Button();
            this.textBox_response_headers = new FastColoredTextBoxNS.FastColoredTextBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.splitContainer2 = new System.Windows.Forms.SplitContainer();
            this.label1 = new System.Windows.Forms.Label();
            this.numericUpDown_request = new System.Windows.Forms.NumericUpDown();
            this.textBox_request_url = new FastColoredTextBoxNS.FastColoredTextBox();
            this.comboBox_http_version = new System.Windows.Forms.ComboBox();
            this.comboBox_certificates = new System.Windows.Forms.ComboBox();
            this.textBox_request_headers = new FastColoredTextBoxNS.FastColoredTextBox();
            this.comboBox_http_method = new System.Windows.Forms.ComboBox();
            this.textBox_request_body = new FastColoredTextBoxNS.FastColoredTextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.splitContainer3 = new System.Windows.Forms.SplitContainer();
            this.textBox_response_stats = new FastColoredTextBoxNS.FastColoredTextBox();
            this.textBox_response_body = new FastColoredTextBoxNS.FastColoredTextBox();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            this.tabControl2 = new System.Windows.Forms.TabControl();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.textBox_filter = new System.Windows.Forms.TextBox();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.label7 = new System.Windows.Forms.Label();
            this.comboBox_settings_profiles = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.button_settings_import = new System.Windows.Forms.Button();
            this.button_settings_export = new System.Windows.Forms.Button();
            this.button_settings_insert = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.button_settings_delete = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.button_settings_save = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.textBox_cosmos_ContainerId = new System.Windows.Forms.TextBox();
            this.textBox_cosmos_DatabaseId = new System.Windows.Forms.TextBox();
            this.textBox_cosmos_AuthorizationKey = new System.Windows.Forms.TextBox();
            this.textBox_cosmos_EndpointUrl = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_response_headers)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).BeginInit();
            this.splitContainer2.Panel1.SuspendLayout();
            this.splitContainer2.Panel2.SuspendLayout();
            this.splitContainer2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_request)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_request_url)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_request_headers)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_request_body)).BeginInit();
            this.tabPage2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).BeginInit();
            this.splitContainer3.Panel1.SuspendLayout();
            this.splitContainer3.Panel2.SuspendLayout();
            this.splitContainer3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_response_stats)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_response_body)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.tabControl2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // button_request_send
            // 
            this.button_request_send.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.button_request_send.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.button_request_send.BackColor = System.Drawing.Color.Teal;
            this.button_request_send.FlatAppearance.BorderColor = System.Drawing.Color.Teal;
            this.button_request_send.FlatAppearance.BorderSize = 0;
            this.button_request_send.FlatStyle = System.Windows.Forms.FlatStyle.Popup;
            this.button_request_send.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.button_request_send.ForeColor = System.Drawing.SystemColors.ButtonHighlight;
            this.button_request_send.Location = new System.Drawing.Point(806, 30);
            this.button_request_send.Name = "button_request_send";
            this.button_request_send.Size = new System.Drawing.Size(60, 175);
            this.button_request_send.TabIndex = 0;
            this.button_request_send.Text = "Send";
            this.button_request_send.UseVisualStyleBackColor = false;
            this.button_request_send.Click += new System.EventHandler(this.button_request_send_Click);
            // 
            // textBox_response_headers
            // 
            this.textBox_response_headers.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_response_headers.AutoCompleteBracketsList = new char[] {
        '(',
        ')',
        '{',
        '}',
        '[',
        ']',
        '\"',
        '\"',
        '\'',
        '\''};
            this.textBox_response_headers.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;]+);\r\n";
            this.textBox_response_headers.AutoScrollMinSize = new System.Drawing.Size(2, 19);
            this.textBox_response_headers.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.textBox_response_headers.BackBrush = null;
            this.textBox_response_headers.BracketsHighlightStrategy = FastColoredTextBoxNS.BracketsHighlightStrategy.Strategy2;
            this.textBox_response_headers.CharHeight = 14;
            this.textBox_response_headers.CharWidth = 7;
            this.textBox_response_headers.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.textBox_response_headers.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.textBox_response_headers.Font = new System.Drawing.Font("Cascadia Code", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.textBox_response_headers.IsReplaceMode = false;
            this.textBox_response_headers.Language = FastColoredTextBoxNS.Language.JSON;
            this.textBox_response_headers.LeftBracket = '[';
            this.textBox_response_headers.LeftBracket2 = '{';
            this.textBox_response_headers.Location = new System.Drawing.Point(0, 0);
            this.textBox_response_headers.Name = "textBox_response_headers";
            this.textBox_response_headers.Paddings = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.textBox_response_headers.RightBracket = ']';
            this.textBox_response_headers.RightBracket2 = '}';
            this.textBox_response_headers.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.textBox_response_headers.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("textBox_response_headers.ServiceColors")));
            this.textBox_response_headers.Size = new System.Drawing.Size(591, 198);
            this.textBox_response_headers.TabIndex = 1;
            this.textBox_response_headers.Zoom = 100;
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(0, 0);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.Padding = new System.Drawing.Point(51, 4);
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(885, 822);
            this.tabControl1.TabIndex = 2;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.SystemColors.ControlLight;
            this.tabPage1.Controls.Add(this.splitContainer2);
            this.tabPage1.Location = new System.Drawing.Point(4, 26);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(877, 792);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Request";
            // 
            // splitContainer2
            // 
            this.splitContainer2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer2.Location = new System.Drawing.Point(3, 3);
            this.splitContainer2.Name = "splitContainer2";
            this.splitContainer2.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer2.Panel1
            // 
            this.splitContainer2.Panel1.Controls.Add(this.label1);
            this.splitContainer2.Panel1.Controls.Add(this.numericUpDown_request);
            this.splitContainer2.Panel1.Controls.Add(this.textBox_request_url);
            this.splitContainer2.Panel1.Controls.Add(this.comboBox_http_version);
            this.splitContainer2.Panel1.Controls.Add(this.comboBox_certificates);
            this.splitContainer2.Panel1.Controls.Add(this.textBox_request_headers);
            this.splitContainer2.Panel1.Controls.Add(this.button_request_send);
            this.splitContainer2.Panel1.Controls.Add(this.comboBox_http_method);
            this.splitContainer2.Panel1MinSize = 100;
            // 
            // splitContainer2.Panel2
            // 
            this.splitContainer2.Panel2.Controls.Add(this.textBox_request_body);
            this.splitContainer2.Panel2MinSize = 100;
            this.splitContainer2.Size = new System.Drawing.Size(871, 786);
            this.splitContainer2.SplitterDistance = 205;
            this.splitContainer2.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(300, 33);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(46, 15);
            this.label1.TabIndex = 6;
            this.label1.Text = "Repeat:";
            // 
            // numericUpDown_request
            // 
            this.numericUpDown_request.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.numericUpDown_request.Location = new System.Drawing.Point(352, 30);
            this.numericUpDown_request.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDown_request.Name = "numericUpDown_request";
            this.numericUpDown_request.Size = new System.Drawing.Size(38, 23);
            this.numericUpDown_request.TabIndex = 5;
            this.numericUpDown_request.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            // 
            // textBox_request_url
            // 
            this.textBox_request_url.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_request_url.AutoCompleteBracketsList = new char[] {
        '(',
        ')',
        '{',
        '}',
        '[',
        ']',
        '\"',
        '\"',
        '\'',
        '\''};
            this.textBox_request_url.AutoIndentCharsPatterns = "";
            this.textBox_request_url.AutoScrollMinSize = new System.Drawing.Size(6, 16);
            this.textBox_request_url.BackBrush = null;
            this.textBox_request_url.CharHeight = 14;
            this.textBox_request_url.CharWidth = 7;
            this.textBox_request_url.CommentPrefix = null;
            this.textBox_request_url.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.textBox_request_url.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.textBox_request_url.Font = new System.Drawing.Font("Cascadia Code", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.textBox_request_url.HighlightFoldingIndicator = false;
            this.textBox_request_url.HighlightingRangeType = FastColoredTextBoxNS.HighlightingRangeType.AllTextRange;
            this.textBox_request_url.IsReplaceMode = false;
            this.textBox_request_url.Language = FastColoredTextBoxNS.Language.HTML;
            this.textBox_request_url.LeftBracket = '<';
            this.textBox_request_url.LeftBracket2 = '(';
            this.textBox_request_url.Location = new System.Drawing.Point(0, 3);
            this.textBox_request_url.Multiline = false;
            this.textBox_request_url.Name = "textBox_request_url";
            this.textBox_request_url.Paddings = new System.Windows.Forms.Padding(4, 2, 0, 0);
            this.textBox_request_url.RightBracket = '>';
            this.textBox_request_url.RightBracket2 = ')';
            this.textBox_request_url.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.textBox_request_url.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("textBox_request_url.ServiceColors")));
            this.textBox_request_url.ShowLineNumbers = false;
            this.textBox_request_url.ShowScrollBars = false;
            this.textBox_request_url.Size = new System.Drawing.Size(866, 21);
            this.textBox_request_url.TabIndex = 4;
            this.textBox_request_url.Zoom = 100;
            // 
            // comboBox_http_version
            // 
            this.comboBox_http_version.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboBox_http_version.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.comboBox_http_version.FormattingEnabled = true;
            this.comboBox_http_version.Items.AddRange(new object[] {
            "HTTP 1.0",
            "HTTP 1.1",
            "HTTP 2.0",
            "HTTP 3.0"});
            this.comboBox_http_version.Location = new System.Drawing.Point(83, 30);
            this.comboBox_http_version.Name = "comboBox_http_version";
            this.comboBox_http_version.Size = new System.Drawing.Size(78, 23);
            this.comboBox_http_version.TabIndex = 3;
            this.comboBox_http_version.Text = "HTTP 2.0";
            // 
            // comboBox_certificates
            // 
            this.comboBox_certificates.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBox_certificates.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboBox_certificates.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.comboBox_certificates.FormattingEnabled = true;
            this.comboBox_certificates.Location = new System.Drawing.Point(411, 30);
            this.comboBox_certificates.Name = "comboBox_certificates";
            this.comboBox_certificates.Size = new System.Drawing.Size(389, 23);
            this.comboBox_certificates.TabIndex = 2;
            // 
            // textBox_request_headers
            // 
            this.textBox_request_headers.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_request_headers.AutoCompleteBracketsList = new char[] {
        '(',
        ')',
        '{',
        '}',
        '[',
        ']',
        '\"',
        '\"',
        '\'',
        '\''};
            this.textBox_request_headers.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;=]+);\r\n^\\s*(case|default)\\s*[^:" +
    "]*(?<range>:)\\s*(?<range>[^;]+);\r\n";
            this.textBox_request_headers.AutoScrollMinSize = new System.Drawing.Size(25, 19);
            this.textBox_request_headers.BackBrush = null;
            this.textBox_request_headers.CharHeight = 14;
            this.textBox_request_headers.CharWidth = 7;
            this.textBox_request_headers.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.textBox_request_headers.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.textBox_request_headers.Font = new System.Drawing.Font("Cascadia Code", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.textBox_request_headers.IsReplaceMode = false;
            this.textBox_request_headers.LeftBracket = '(';
            this.textBox_request_headers.LeftBracket2 = '{';
            this.textBox_request_headers.Location = new System.Drawing.Point(0, 57);
            this.textBox_request_headers.Name = "textBox_request_headers";
            this.textBox_request_headers.Paddings = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.textBox_request_headers.RightBracket = ')';
            this.textBox_request_headers.RightBracket2 = '}';
            this.textBox_request_headers.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.textBox_request_headers.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("textBox_request_headers.ServiceColors")));
            this.textBox_request_headers.ServiceLinesColor = System.Drawing.SystemColors.ButtonFace;
            this.textBox_request_headers.Size = new System.Drawing.Size(800, 146);
            this.textBox_request_headers.TabIndex = 0;
            this.textBox_request_headers.Zoom = 100;
            // 
            // comboBox_http_method
            // 
            this.comboBox_http_method.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.comboBox_http_method.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.comboBox_http_method.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboBox_http_method.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.comboBox_http_method.FormattingEnabled = true;
            this.comboBox_http_method.Items.AddRange(new object[] {
            "GET",
            "POST",
            "PUT",
            "PATCH",
            "DELETE",
            "OPTIONS",
            "HEAD",
            "TRACE"});
            this.comboBox_http_method.Location = new System.Drawing.Point(0, 30);
            this.comboBox_http_method.Name = "comboBox_http_method";
            this.comboBox_http_method.Size = new System.Drawing.Size(77, 23);
            this.comboBox_http_method.TabIndex = 1;
            this.comboBox_http_method.Text = "GET";
            // 
            // textBox_request_body
            // 
            this.textBox_request_body.AllowSeveralTextStyleDrawing = true;
            this.textBox_request_body.AutoCompleteBracketsList = new char[] {
        '(',
        ')',
        '{',
        '}',
        '[',
        ']',
        '\"',
        '\"',
        '\'',
        '\''};
            this.textBox_request_body.AutoIndentCharsPatterns = "^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;=]+);\r\n^\\s*(case|default)\\s*[^:]*" +
    "(?<range>:)\\s*(?<range>[^;]+);";
            this.textBox_request_body.AutoScrollMinSize = new System.Drawing.Size(25, 19);
            this.textBox_request_body.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.textBox_request_body.BackBrush = null;
            this.textBox_request_body.CharHeight = 14;
            this.textBox_request_body.CharWidth = 7;
            this.textBox_request_body.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.textBox_request_body.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.textBox_request_body.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox_request_body.Font = new System.Drawing.Font("Cascadia Code", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.textBox_request_body.IsReplaceMode = false;
            this.textBox_request_body.Location = new System.Drawing.Point(0, 0);
            this.textBox_request_body.Name = "textBox_request_body";
            this.textBox_request_body.Paddings = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.textBox_request_body.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.textBox_request_body.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("textBox_request_body.ServiceColors")));
            this.textBox_request_body.ServiceLinesColor = System.Drawing.SystemColors.ButtonFace;
            this.textBox_request_body.Size = new System.Drawing.Size(871, 577);
            this.textBox_request_body.TabIndex = 3;
            this.textBox_request_body.Zoom = 100;
            // 
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.tabPage2.Controls.Add(this.splitContainer3);
            this.tabPage2.Location = new System.Drawing.Point(4, 26);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(877, 792);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Response";
            // 
            // splitContainer3
            // 
            this.splitContainer3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer3.Location = new System.Drawing.Point(3, 3);
            this.splitContainer3.Name = "splitContainer3";
            this.splitContainer3.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer3.Panel1
            // 
            this.splitContainer3.Panel1.Controls.Add(this.textBox_response_stats);
            this.splitContainer3.Panel1.Controls.Add(this.textBox_response_headers);
            // 
            // splitContainer3.Panel2
            // 
            this.splitContainer3.Panel2.Controls.Add(this.textBox_response_body);
            this.splitContainer3.Size = new System.Drawing.Size(871, 786);
            this.splitContainer3.SplitterDistance = 200;
            this.splitContainer3.TabIndex = 3;
            // 
            // textBox_response_stats
            // 
            this.textBox_response_stats.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_response_stats.AutoCompleteBracketsList = new char[] {
        '(',
        ')',
        '{',
        '}',
        '[',
        ']',
        '\"',
        '\"',
        '\'',
        '\''};
            this.textBox_response_stats.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;]+);\r\n";
            this.textBox_response_stats.AutoScrollMinSize = new System.Drawing.Size(2, 19);
            this.textBox_response_stats.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.textBox_response_stats.BackBrush = null;
            this.textBox_response_stats.BracketsHighlightStrategy = FastColoredTextBoxNS.BracketsHighlightStrategy.Strategy2;
            this.textBox_response_stats.CharHeight = 14;
            this.textBox_response_stats.CharWidth = 7;
            this.textBox_response_stats.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.textBox_response_stats.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.textBox_response_stats.Font = new System.Drawing.Font("Cascadia Code", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.textBox_response_stats.IsReplaceMode = false;
            this.textBox_response_stats.Language = FastColoredTextBoxNS.Language.JSON;
            this.textBox_response_stats.LeftBracket = '[';
            this.textBox_response_stats.LeftBracket2 = '{';
            this.textBox_response_stats.Location = new System.Drawing.Point(593, -2);
            this.textBox_response_stats.Name = "textBox_response_stats";
            this.textBox_response_stats.Paddings = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.textBox_response_stats.RightBracket = ']';
            this.textBox_response_stats.RightBracket2 = '}';
            this.textBox_response_stats.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.textBox_response_stats.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("textBox_response_stats.ServiceColors")));
            this.textBox_response_stats.ShowLineNumbers = false;
            this.textBox_response_stats.Size = new System.Drawing.Size(281, 200);
            this.textBox_response_stats.TabIndex = 2;
            this.textBox_response_stats.Zoom = 100;
            // 
            // textBox_response_body
            // 
            this.textBox_response_body.AutoCompleteBracketsList = new char[] {
        '(',
        ')',
        '{',
        '}',
        '[',
        ']',
        '\"',
        '\"',
        '\'',
        '\''};
            this.textBox_response_body.AutoIndentCharsPatterns = "\r\n^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;]+);\r\n";
            this.textBox_response_body.AutoScrollMinSize = new System.Drawing.Size(2, 19);
            this.textBox_response_body.BackBrush = null;
            this.textBox_response_body.BracketsHighlightStrategy = FastColoredTextBoxNS.BracketsHighlightStrategy.Strategy2;
            this.textBox_response_body.CharHeight = 14;
            this.textBox_response_body.CharWidth = 7;
            this.textBox_response_body.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.textBox_response_body.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.textBox_response_body.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textBox_response_body.Font = new System.Drawing.Font("Cascadia Code", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.textBox_response_body.IsReplaceMode = false;
            this.textBox_response_body.LeftBracket = '(';
            this.textBox_response_body.LeftBracket2 = '{';
            this.textBox_response_body.Location = new System.Drawing.Point(0, 0);
            this.textBox_response_body.Name = "textBox_response_body";
            this.textBox_response_body.Paddings = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.textBox_response_body.RightBracket = ')';
            this.textBox_response_body.RightBracket2 = '}';
            this.textBox_response_body.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.textBox_response_body.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("textBox_response_body.ServiceColors")));
            this.textBox_response_body.ShowFoldingLines = true;
            this.textBox_response_body.Size = new System.Drawing.Size(871, 582);
            this.textBox_response_body.TabIndex = 1;
            this.textBox_response_body.Zoom = 100;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToResizeRows = false;
            this.dataGridView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridView1.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.AllCellsExceptHeader;
            this.dataGridView1.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
            this.dataGridView1.BackgroundColor = System.Drawing.SystemColors.ControlLight;
            this.dataGridView1.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridView1.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            this.dataGridView1.ColumnHeadersVisible = false;
            this.dataGridView1.EnableHeadersVisualStyles = false;
            this.dataGridView1.GridColor = System.Drawing.SystemColors.Control;
            this.dataGridView1.Location = new System.Drawing.Point(3, 28);
            this.dataGridView1.MultiSelect = false;
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersVisible = false;
            this.dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridView1.Size = new System.Drawing.Size(430, 763);
            this.dataGridView1.TabIndex = 3;
            this.dataGridView1.CellPainting += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.dataGridView1_CellPainting);
            this.dataGridView1.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_CellValueChanged);
            this.dataGridView1.RowEnter += new System.Windows.Forms.DataGridViewCellEventHandler(this.dataGridView1_RowEnter);
            this.dataGridView1.UserDeletingRow += new System.Windows.Forms.DataGridViewRowCancelEventHandler(this.dataGridView1_UserDeletingRow);
            this.dataGridView1.KeyDown += new System.Windows.Forms.KeyEventHandler(this.dataGridView1_KeyDown);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.tabControl2);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.tabControl1);
            this.splitContainer1.Size = new System.Drawing.Size(1333, 822);
            this.splitContainer1.SplitterDistance = 444;
            this.splitContainer1.TabIndex = 4;
            // 
            // tabControl2
            // 
            this.tabControl2.Alignment = System.Windows.Forms.TabAlignment.Bottom;
            this.tabControl2.Controls.Add(this.tabPage3);
            this.tabControl2.Controls.Add(this.tabPage4);
            this.tabControl2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl2.Location = new System.Drawing.Point(0, 0);
            this.tabControl2.Name = "tabControl2";
            this.tabControl2.SelectedIndex = 0;
            this.tabControl2.Size = new System.Drawing.Size(444, 822);
            this.tabControl2.TabIndex = 5;
            this.tabControl2.SelectedIndexChanged += new System.EventHandler(this.tabControl2_SelectedIndexChanged);
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.dataGridView1);
            this.tabPage3.Controls.Add(this.textBox_filter);
            this.tabPage3.Location = new System.Drawing.Point(4, 4);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(436, 794);
            this.tabPage3.TabIndex = 0;
            this.tabPage3.Text = "Sessions";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // textBox_filter
            // 
            this.textBox_filter.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBox_filter.Dock = System.Windows.Forms.DockStyle.Top;
            this.textBox_filter.Location = new System.Drawing.Point(3, 3);
            this.textBox_filter.Name = "textBox_filter";
            this.textBox_filter.Size = new System.Drawing.Size(430, 23);
            this.textBox_filter.TabIndex = 4;
            this.textBox_filter.TextChanged += new System.EventHandler(this.textBox_filter_TextChanged);
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.label7);
            this.tabPage4.Controls.Add(this.comboBox_settings_profiles);
            this.tabPage4.Controls.Add(this.groupBox1);
            this.tabPage4.Location = new System.Drawing.Point(4, 4);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(436, 794);
            this.tabPage4.TabIndex = 1;
            this.tabPage4.Text = "Settings";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(14, 19);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(100, 15);
            this.label7.TabIndex = 5;
            this.label7.Text = "Database profiles:";
            // 
            // comboBox_settings_profiles
            // 
            this.comboBox_settings_profiles.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBox_settings_profiles.FormattingEnabled = true;
            this.comboBox_settings_profiles.Location = new System.Drawing.Point(133, 16);
            this.comboBox_settings_profiles.Name = "comboBox_settings_profiles";
            this.comboBox_settings_profiles.Size = new System.Drawing.Size(292, 23);
            this.comboBox_settings_profiles.TabIndex = 4;
            this.comboBox_settings_profiles.SelectedIndexChanged += new System.EventHandler(this.comboBox_settings_profiles_SelectedIndexChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.button_settings_import);
            this.groupBox1.Controls.Add(this.button_settings_export);
            this.groupBox1.Controls.Add(this.button_settings_insert);
            this.groupBox1.Controls.Add(this.label6);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.button_settings_delete);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.Controls.Add(this.button_settings_save);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.textBox_cosmos_ContainerId);
            this.groupBox1.Controls.Add(this.textBox_cosmos_DatabaseId);
            this.groupBox1.Controls.Add(this.textBox_cosmos_AuthorizationKey);
            this.groupBox1.Controls.Add(this.textBox_cosmos_EndpointUrl);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Location = new System.Drawing.Point(8, 59);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(417, 300);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Database profile";
            // 
            // button_settings_import
            // 
            this.button_settings_import.Location = new System.Drawing.Point(16, 226);
            this.button_settings_import.Name = "button_settings_import";
            this.button_settings_import.Size = new System.Drawing.Size(75, 23);
            this.button_settings_import.TabIndex = 7;
            this.button_settings_import.Text = "Import db";
            this.button_settings_import.UseVisualStyleBackColor = true;
            this.button_settings_import.Click += new System.EventHandler(this.button_settings_import_Click);
            // 
            // button_settings_export
            // 
            this.button_settings_export.Location = new System.Drawing.Point(16, 260);
            this.button_settings_export.Name = "button_settings_export";
            this.button_settings_export.Size = new System.Drawing.Size(75, 23);
            this.button_settings_export.TabIndex = 6;
            this.button_settings_export.Text = "Export db";
            this.button_settings_export.UseVisualStyleBackColor = true;
            this.button_settings_export.Click += new System.EventHandler(this.button_settings_export_Click);
            // 
            // button_settings_insert
            // 
            this.button_settings_insert.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_settings_insert.Location = new System.Drawing.Point(218, 260);
            this.button_settings_insert.Name = "button_settings_insert";
            this.button_settings_insert.Size = new System.Drawing.Size(85, 23);
            this.button_settings_insert.TabIndex = 5;
            this.button_settings_insert.Text = "Add as new";
            this.button_settings_insert.UseVisualStyleBackColor = true;
            this.button_settings_insert.Click += new System.EventHandler(this.button_settings_insert_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(15, 184);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(75, 15);
            this.label6.TabIndex = 2;
            this.label6.Text = "Container Id:";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(15, 155);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(71, 15);
            this.label5.TabIndex = 2;
            this.label5.Text = "Database Id:";
            // 
            // button_settings_delete
            // 
            this.button_settings_delete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_settings_delete.Location = new System.Drawing.Point(309, 260);
            this.button_settings_delete.Name = "button_settings_delete";
            this.button_settings_delete.Size = new System.Drawing.Size(85, 23);
            this.button_settings_delete.TabIndex = 4;
            this.button_settings_delete.Text = "Delete";
            this.button_settings_delete.UseVisualStyleBackColor = true;
            this.button_settings_delete.Click += new System.EventHandler(this.button_settings_delete_Click);
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(15, 126);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(104, 15);
            this.label4.TabIndex = 2;
            this.label4.Text = "Authorization Key:";
            // 
            // button_settings_save
            // 
            this.button_settings_save.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.button_settings_save.Location = new System.Drawing.Point(218, 226);
            this.button_settings_save.Name = "button_settings_save";
            this.button_settings_save.Size = new System.Drawing.Size(176, 28);
            this.button_settings_save.TabIndex = 3;
            this.button_settings_save.Text = "Update";
            this.button_settings_save.UseVisualStyleBackColor = true;
            this.button_settings_save.Click += new System.EventHandler(this.button_settings_save_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(15, 97);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(76, 15);
            this.label3.TabIndex = 2;
            this.label3.Text = "Endpoint Url:";
            // 
            // textBox_cosmos_ContainerId
            // 
            this.textBox_cosmos_ContainerId.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_cosmos_ContainerId.Location = new System.Drawing.Point(125, 181);
            this.textBox_cosmos_ContainerId.Name = "textBox_cosmos_ContainerId";
            this.textBox_cosmos_ContainerId.Size = new System.Drawing.Size(269, 23);
            this.textBox_cosmos_ContainerId.TabIndex = 0;
            // 
            // textBox_cosmos_DatabaseId
            // 
            this.textBox_cosmos_DatabaseId.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_cosmos_DatabaseId.Location = new System.Drawing.Point(125, 152);
            this.textBox_cosmos_DatabaseId.Name = "textBox_cosmos_DatabaseId";
            this.textBox_cosmos_DatabaseId.Size = new System.Drawing.Size(269, 23);
            this.textBox_cosmos_DatabaseId.TabIndex = 0;
            // 
            // textBox_cosmos_AuthorizationKey
            // 
            this.textBox_cosmos_AuthorizationKey.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_cosmos_AuthorizationKey.Location = new System.Drawing.Point(125, 123);
            this.textBox_cosmos_AuthorizationKey.Name = "textBox_cosmos_AuthorizationKey";
            this.textBox_cosmos_AuthorizationKey.Size = new System.Drawing.Size(269, 23);
            this.textBox_cosmos_AuthorizationKey.TabIndex = 0;
            // 
            // textBox_cosmos_EndpointUrl
            // 
            this.textBox_cosmos_EndpointUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBox_cosmos_EndpointUrl.Location = new System.Drawing.Point(125, 94);
            this.textBox_cosmos_EndpointUrl.Name = "textBox_cosmos_EndpointUrl";
            this.textBox_cosmos_EndpointUrl.Size = new System.Drawing.Size(269, 23);
            this.textBox_cosmos_EndpointUrl.TabIndex = 0;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label2.AutoEllipsis = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point);
            this.label2.Location = new System.Drawing.Point(6, 25);
            this.label2.Margin = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.label2.Name = "label2";
            this.label2.Padding = new System.Windows.Forms.Padding(5, 0, 5, 0);
            this.label2.Size = new System.Drawing.Size(405, 45);
            this.label2.TabIndex = 1;
            this.label2.Text = "Connection string to your Azure Cosmos database instance. There your sessions (re" +
    "quests and responses) will be stored so you can share them with your team. Cosmo" +
    "s serverless tier is recommended.";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1333, 822);
            this.Controls.Add(this.splitContainer1);
            this.DoubleBuffered = true;
            this.Name = "Form1";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            ((System.ComponentModel.ISupportInitialize)(this.textBox_response_headers)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.splitContainer2.Panel1.ResumeLayout(false);
            this.splitContainer2.Panel1.PerformLayout();
            this.splitContainer2.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer2)).EndInit();
            this.splitContainer2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDown_request)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_request_url)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_request_headers)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_request_body)).EndInit();
            this.tabPage2.ResumeLayout(false);
            this.splitContainer3.Panel1.ResumeLayout(false);
            this.splitContainer3.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer3)).EndInit();
            this.splitContainer3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.textBox_response_stats)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.textBox_response_body)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.tabControl2.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button button_request_send;
        private FastColoredTextBoxNS.FastColoredTextBox textBox_response_headers;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.ComboBox comboBox_http_method;
        private System.Windows.Forms.TabPage tabPage2;
        private FastColoredTextBoxNS.FastColoredTextBox textBox_response_stats;
        private FastColoredTextBoxNS.FastColoredTextBox textBox_response_body;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.SplitContainer splitContainer1;
        protected FastColoredTextBoxNS.FastColoredTextBox textBox_request_body;
        protected FastColoredTextBoxNS.FastColoredTextBox textBox_request_headers;
        private System.Windows.Forms.SplitContainer splitContainer2;
        private System.Windows.Forms.SplitContainer splitContainer3;
        private System.Windows.Forms.ComboBox comboBox_certificates;
        private System.Windows.Forms.ComboBox comboBox_http_version;
        private FastColoredTextBoxNS.FastColoredTextBox textBox_request_url;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.NumericUpDown numericUpDown_request;
        private System.Windows.Forms.TextBox textBox_filter;
        private System.Windows.Forms.TabControl tabControl2;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textBox_cosmos_EndpointUrl;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox textBox_cosmos_ContainerId;
        private System.Windows.Forms.TextBox textBox_cosmos_AuthorizationKey;
        private System.Windows.Forms.TextBox textBox_cosmos_DatabaseId;
        private System.Windows.Forms.Button button_settings_save;
        private System.Windows.Forms.Button button_settings_delete;
        private System.Windows.Forms.ComboBox comboBox_settings_profiles;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button button_settings_insert;
        private System.Windows.Forms.Button button_settings_export;
        private System.Windows.Forms.Button button_settings_import;
    }
}

