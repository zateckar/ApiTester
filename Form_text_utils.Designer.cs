namespace ApiTester
{
    partial class Form_text_utils
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form_text_utils));
            this.fastColoredTextBox_input = new FastColoredTextBoxNS.FastColoredTextBox();
            this.fastColoredTextBox_output = new FastColoredTextBoxNS.FastColoredTextBox();
            this.comboBox_operation = new System.Windows.Forms.ComboBox();
            this.splitContainer1 = new System.Windows.Forms.SplitContainer();
            ((System.ComponentModel.ISupportInitialize)(this.fastColoredTextBox_input)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.fastColoredTextBox_output)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).BeginInit();
            this.splitContainer1.Panel1.SuspendLayout();
            this.splitContainer1.Panel2.SuspendLayout();
            this.splitContainer1.SuspendLayout();
            this.SuspendLayout();
            // 
            // fastColoredTextBox_input
            // 
            this.fastColoredTextBox_input.AutoCompleteBracketsList = new char[] {
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
            this.fastColoredTextBox_input.AutoIndent = false;
            this.fastColoredTextBox_input.AutoIndentChars = false;
            this.fastColoredTextBox_input.AutoIndentCharsPatterns = "^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;=]+);\r\n^\\s*(case|default)\\s*[^:]*" +
    "(?<range>:)\\s*(?<range>[^;]+);";
            this.fastColoredTextBox_input.AutoIndentExistingLines = false;
            this.fastColoredTextBox_input.AutoScrollMinSize = new System.Drawing.Size(31, 18);
            this.fastColoredTextBox_input.BackBrush = null;
            this.fastColoredTextBox_input.CharHeight = 18;
            this.fastColoredTextBox_input.CharWidth = 10;
            this.fastColoredTextBox_input.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.fastColoredTextBox_input.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fastColoredTextBox_input.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.fastColoredTextBox_input.IsReplaceMode = false;
            this.fastColoredTextBox_input.Location = new System.Drawing.Point(0, 0);
            this.fastColoredTextBox_input.Name = "fastColoredTextBox_input";
            this.fastColoredTextBox_input.Paddings = new System.Windows.Forms.Padding(0);
            this.fastColoredTextBox_input.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.fastColoredTextBox_input.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("fastColoredTextBox_input.ServiceColors")));
            this.fastColoredTextBox_input.Size = new System.Drawing.Size(1434, 172);
            this.fastColoredTextBox_input.TabIndex = 0;
            this.fastColoredTextBox_input.Zoom = 100;
            // 
            // fastColoredTextBox_output
            // 
            this.fastColoredTextBox_output.AutoCompleteBracketsList = new char[] {
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
            this.fastColoredTextBox_output.AutoIndent = false;
            this.fastColoredTextBox_output.AutoIndentChars = false;
            this.fastColoredTextBox_output.AutoIndentCharsPatterns = "^\\s*[\\w\\.]+(\\s\\w+)?\\s*(?<range>=)\\s*(?<range>[^;=]+);\r\n^\\s*(case|default)\\s*[^:]*" +
    "(?<range>:)\\s*(?<range>[^;]+);";
            this.fastColoredTextBox_output.AutoIndentExistingLines = false;
            this.fastColoredTextBox_output.AutoScrollMinSize = new System.Drawing.Size(31, 18);
            this.fastColoredTextBox_output.BackBrush = null;
            this.fastColoredTextBox_output.BackColor = System.Drawing.Color.Gainsboro;
            this.fastColoredTextBox_output.CharHeight = 18;
            this.fastColoredTextBox_output.CharWidth = 10;
            this.fastColoredTextBox_output.DisabledColor = System.Drawing.Color.FromArgb(((int)(((byte)(100)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))), ((int)(((byte)(180)))));
            this.fastColoredTextBox_output.Dock = System.Windows.Forms.DockStyle.Fill;
            this.fastColoredTextBox_output.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.fastColoredTextBox_output.IndentBackColor = System.Drawing.Color.Gainsboro;
            this.fastColoredTextBox_output.IsReplaceMode = false;
            this.fastColoredTextBox_output.Location = new System.Drawing.Point(0, 28);
            this.fastColoredTextBox_output.Name = "fastColoredTextBox_output";
            this.fastColoredTextBox_output.Paddings = new System.Windows.Forms.Padding(0);
            this.fastColoredTextBox_output.SelectionColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(0)))), ((int)(((byte)(0)))), ((int)(((byte)(255)))));
            this.fastColoredTextBox_output.ServiceColors = ((FastColoredTextBoxNS.ServiceColors)(resources.GetObject("fastColoredTextBox_output.ServiceColors")));
            this.fastColoredTextBox_output.Size = new System.Drawing.Size(1434, 188);
            this.fastColoredTextBox_output.TabIndex = 1;
            this.fastColoredTextBox_output.Zoom = 100;
            // 
            // comboBox_operation
            // 
            this.comboBox_operation.Dock = System.Windows.Forms.DockStyle.Top;
            this.comboBox_operation.FormattingEnabled = true;
            this.comboBox_operation.Items.AddRange(new object[] {
            "to Base64",
            "from Base64",
            "Escape",
            "Unescape"});
            this.comboBox_operation.Location = new System.Drawing.Point(0, 0);
            this.comboBox_operation.Name = "comboBox_operation";
            this.comboBox_operation.Size = new System.Drawing.Size(1434, 28);
            this.comboBox_operation.TabIndex = 2;
            this.comboBox_operation.SelectionChangeCommitted += new System.EventHandler(this.comboBox_operation_SelectionChangeCommitted);
            // 
            // splitContainer1
            // 
            this.splitContainer1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer1.Location = new System.Drawing.Point(0, 0);
            this.splitContainer1.Name = "splitContainer1";
            this.splitContainer1.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            this.splitContainer1.Panel1.Controls.Add(this.fastColoredTextBox_input);
            // 
            // splitContainer1.Panel2
            // 
            this.splitContainer1.Panel2.Controls.Add(this.fastColoredTextBox_output);
            this.splitContainer1.Panel2.Controls.Add(this.comboBox_operation);
            this.splitContainer1.Size = new System.Drawing.Size(1434, 392);
            this.splitContainer1.SplitterDistance = 172;
            this.splitContainer1.TabIndex = 3;
            // 
            // Form_text_utils
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(120F, 120F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(1434, 392);
            this.Controls.Add(this.splitContainer1);
            this.Name = "Form_text_utils";
            this.Text = "Text utilities";
            this.Load += new System.EventHandler(this.Form_text_utils_Load);
            ((System.ComponentModel.ISupportInitialize)(this.fastColoredTextBox_input)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.fastColoredTextBox_output)).EndInit();
            this.splitContainer1.Panel1.ResumeLayout(false);
            this.splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer1)).EndInit();
            this.splitContainer1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private FastColoredTextBoxNS.FastColoredTextBox fastColoredTextBox_input;
        private FastColoredTextBoxNS.FastColoredTextBox fastColoredTextBox_output;
        private System.Windows.Forms.ComboBox comboBox_operation;
        private System.Windows.Forms.SplitContainer splitContainer1;
    }
}