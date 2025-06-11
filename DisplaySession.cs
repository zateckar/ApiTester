using FastColoredTextBoxNS;
using SQLite;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
            toolStripComboBox_http_version.SelectedItem = session.RequestHttpVersion;

            //if session has different certificate than is available, let user know
            comboBox_certificates.BackColor = SystemColors.Window;
            comboBox_certificates.ForeColor = SystemColors.WindowText;

            foreach (var item in comboBox_certificates.Items)
            {
                if(item.ToString().Equals(session.ClientCertSubject))
                    comboBox_certificates.SelectedItem = session.ClientCertSubject;
            }
            if (comboBox_certificates.SelectedItem == null && session.ClientCertSubject != null)
            {
                comboBox_certificates.BackColor = Color.Red;
                comboBox_certificates.ForeColor = Color.White;
                comboBox_certificates.Text = session.ClientCertSubject;
            }
            

            if ((session.Group != null) && (session.Group.Length > 1))
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
            textBox_request_body.InsertText(pretty_req[0, 1]);

            textBox_request_headers.Clear();
            textBox_request_headers.InsertText(session.RequestHeaders);

            textBox_request_url.Clear();
            textBox_request_url.InsertText(session.UriAbsoluteUri);

            //Response is stored zipped as a string
            //var ResponseBody_zip = Convert.FromBase64String(session.ResponseBody);
            var ResponseBody_string = Unzip(session.ResponseBody);

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
            toolStripStatusLabel_response_stats_response_time.Text = " " + session.ResponseTime.ToString() + "ms  " + Math.Round((double)session.ResponseLength/1024.0,2).ToString() + " kB";

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

             
            // Request/response statistics
            listView1.Clear();
            listView1.View = View.Details;
            listView1.FullRowSelect = true;
            listView1.GridLines = true;

            listView1.Columns.Add("Name");
            listView1.Columns.Add("Value");


            ListViewItem entryListItem = listView1.Items.Add("Total duration (ms)");
            entryListItem.Font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold);
            entryListItem.UseItemStyleForSubItems = false;
            ListViewItem.ListViewSubItem expenseItem = entryListItem.SubItems.Add(Math.Round(session.DurationRequest, 2).ToString());
            //expenseItem.ForeColor = System.Drawing.Color.Red;
            expenseItem.Font = new System.Drawing.Font( "Arial", 11, System.Drawing.FontStyle.Bold);


            //listView1.Items.Add(new ListViewItem(new[] { "Total request duration (ms)", Math.Round(session.DurationRequest, 2).ToString() }));
            listView1.Items.Add(new ListViewItem(new[] { "DNS resolution duration (ms)", Math.Round(session.DurationResolution, 2).ToString() }));
            listView1.Items.Add(new ListViewItem(new[] { "TCP connect (ms)", Math.Round(session.DurationConnect, 2).ToString() }));
            listView1.Items.Add(new ListViewItem(new[] { "TLS handshake duration (ms)", Math.Round(session.DurationHandshake, 2).ToString() } ));

            listView1.Items.Add(new ListViewItem(new[] { "Request header (ms)", Math.Round(session.DurationRequestHeaders, 2).ToString() }));
            listView1.Items.Add(new ListViewItem(new[] { "Request content (ms)", Math.Round(session.DurationRequestContent, 2).ToString() }));
            listView1.Items.Add(new ListViewItem(new[] { "Response headers (ms)", Math.Round(session.DurationResponseHeaders, 2).ToString() }));
            listView1.Items.Add(new ListViewItem(new[] { "Response content (ms)", Math.Round(session.DurationResponseContent, 2).ToString() }));

            listView1.Items.Add(new ListViewItem(new[] { "", "" }));
            listView1.Items.Add(new ListViewItem(new[] { "Request date", session.DateTime }));
            listView1.Items.Add(new ListViewItem(new[] { "Response HTTP version", session.ResponseHttpVersion }));
            listView1.Items.Add(new ListViewItem(new[] { "Response size (kB)", Math.Round((double)session.ResponseLength / 1024.0, 2).ToString() }));

            listView1.Items.Add(new ListViewItem(new[] { "", "" }));
            listView1.Items.Add(new ListViewItem(new[] { "Server certificate DN", session.ServerCertSubject }));
            listView1.Items.Add(new ListViewItem(new[] { "Server certificate Issuer", session.ServerCertIssuer }));
            listView1.Items.Add(new ListViewItem(new[] { "Server certificate valid from", session.ServerCertValidFrom }));
            listView1.Items.Add(new ListViewItem(new[] { "Server certificate valid to", session.ServerCertValidTo }));
            listView1.Items.Add(new ListViewItem(new[] { "Server certificate Is valid?", session.ServerCertIsValid.ToString() }));


            listView1.Columns[0].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            listView1.Columns[0].Width = listView1.Columns[0].Width + 15;

            listView1.Columns[1].AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            listView1.Columns[1].Width = listView1.Columns[1].Width + 15;
            listView1.Columns[1].TextAlign = HorizontalAlignment.Right;

            this.Cursor = Cursors.Default;
        }
    }
}
