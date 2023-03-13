using FastColoredTextBoxNS;
using SQLite;
using System;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        private async Task DisplaySession(int RowIndex)
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;

            int clickedId = Convert.ToInt32(dataGridView1.Rows[RowIndex].Cells["Id"].Value);

            //Session session = new Session();
            //session = _allSessions.Where(c => c.Id == clickedId).FirstOrDefault();

            var query = sessionsConn.Table<Session>().Where(s => s.Id == clickedId);
            var session = await query.FirstAsync();

            label_displayed_Id.Text = session.Id.ToString();
            comboBox_http_method.SelectedItem = session.Method;
            comboBox_certificates.SelectedItem = session.ClientCertSubject;
            comboBox_http_version.SelectedItem = session.RequestHttpVersion;

            if ((session.Group != null) && (session.Group.Length > 2))
            {
                comboBox_group.SelectedItem = session.Group;
            }
            else
            {
                comboBox_group.SelectedIndex = 0;
            }

            var pretty_req = PrettyPrint(session.RequestBody);
            if (pretty_req[0, 0].Equals("JSON")) textBox_request_body.Language = Language.JSON;
            if (pretty_req[0, 0].Equals("XML")) textBox_request_body.Language = Language.XML;
            textBox_request_body.Clear();
            textBox_request_body.InsertText(session.RequestBody);

            textBox_request_headers.Clear();
            textBox_request_headers.InsertText(session.RequestHeaders);

            textBox_request_url.Clear();
            textBox_request_url.InsertText(session.UriAbsoluteUri);

            //Response is stored zipped as a string
            var ResponseBody_zip = Convert.FromBase64String(session.ResponseBody);
            var ResponseBody_string = Unzip(ResponseBody_zip);

            var pretty = PrettyPrint(ResponseBody_string);
            if (pretty[0, 0].Equals("JSON")) textBox_response_body.Language = Language.JSON;
            if (pretty[0, 0].Equals("XML")) textBox_response_body.Language = Language.XML;

            textBox_response_body.Clear();
            textBox_response_body.InsertText(pretty[0, 1]);

            textBox_response_headers.Clear();
            textBox_response_headers.InsertText(session.ResponseHeaders);

            //Additional info in the status bar
            toolStripStatusLabel_response_stats_http_version.Text = "HTTP " + session.ResponseHttpVersion;
            toolStripStatusLabel_response_stats_datetime.Text = " " + session.DateTime;
            toolStripStatusLabel_response_stats_response_time.Text = " " + session.ResponseTime.ToString() + "ms ";

            toolStripDropDownButton_response_stats_certificate.DropDown.Items.Clear();
            toolStripDropDownButton_response_stats_certificate.DropDown.Items.Add("DN: " + session.ServerCertSubject);
            toolStripDropDownButton_response_stats_certificate.DropDown.Items.Add("Issuer: " + session.ServerCertIssuer);
            toolStripDropDownButton_response_stats_certificate.DropDown.Items.Add("Valid from: " + session.ServerCertValidFrom);
            toolStripDropDownButton_response_stats_certificate.DropDown.Items.Add("Valid to: " + session.ServerCertValidTo);
            toolStripDropDownButton_response_stats_certificate.DropDown.Items.Add("Is Valid: " + session.ServerCertIsValid.ToString());

            if (session.ServerCertIsValid)
            {
                toolStripDropDownButton_response_stats_certificate.BackColor = Color.Green;
                toolStripDropDownButton_response_stats_certificate.ForeColor = Color.White;
            }
            else if (!session.ServerCertIsValid)
            {
                toolStripDropDownButton_response_stats_certificate.BackColor = Color.Red;
                toolStripDropDownButton_response_stats_certificate.ForeColor = Color.White;
            }

            this.Cursor = Cursors.Default;
        }
    }
}
