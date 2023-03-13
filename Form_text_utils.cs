using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form_text_utils : Form
    {
        public Form_text_utils()
        {
            InitializeComponent();
        }
        
        private void comboBox_operation_SelectionChangeCommitted(object sender, EventArgs e)
        {
            try
            {
                fastColoredTextBox_output.Clear();

                //to Base64
                if (comboBox_operation.SelectedIndex == 0)
                {
                    var plainTextBytes = Encoding.UTF8.GetBytes(fastColoredTextBox_input.Text);
                    fastColoredTextBox_output.Text = Convert.ToBase64String(plainTextBytes);
                }

                //from Base64
                if (comboBox_operation.SelectedIndex == 1)
                {
                    var base64EncodedBytes = Convert.FromBase64String(fastColoredTextBox_input.Text);
                    fastColoredTextBox_output.Text = Encoding.UTF8.GetString(base64EncodedBytes);
                    
                }

                //Escape
                if (comboBox_operation.SelectedIndex == 2)
                {
                    fastColoredTextBox_output.Text = Uri.EscapeDataString(fastColoredTextBox_input.Text);
                }

                //Unescape
                if (comboBox_operation.SelectedIndex == 3)
                {
                    fastColoredTextBox_output.Text = Uri.UnescapeDataString(fastColoredTextBox_input.Text);
                }

                fastColoredTextBox_output.SelectAll();
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }
        }

        private void Form_text_utils_Load(object sender, EventArgs e)
        {
            fastColoredTextBox_input.Text  = Clipboard.GetText();
        }
    }
}
