using FastColoredTextBoxNS;
using FastColoredTextBoxNS.Text;
using FastColoredTextBoxNS.Types;
using System;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        private static readonly string[] BlankStatsRow = { "", "" };

        //One shared instance for the life of the process rather than a fresh Font per
        //displayed session - fonts handed to ListViewItems are never disposed by the
        //control, and creating them per row view starves the process's GDI handles.
        private static readonly Font StatsBoldFont = new("Arial", 11, FontStyle.Bold);

        private static string Ms(double value) => Math.Round(value, 2).ToString(CultureInfo.CurrentCulture);

        private async Task DisplaySession(int RowIndex)
        {
            CursorWait(true);

            try
            {
                if (ViewRow(RowIndex) is not { } source) return;
                int clickedId = source.Id;

                //A negative id is a request still in flight - nothing is stored for it yet, so
                //the panes keep showing whatever was selected before.
                if (clickedId < 0) return;

                //Claimed before the first await: RowEnter and the callers that display a row
                //directly can both ask for the same session, and two runs of the code below would
                //clear and fill the panes over each other.
                if (clickedId == displayedSessionId) return;
                displayedSessionId = clickedId;

                var session = await sessionsConn.FindAsync<Session>(clickedId);

                if (session is null) return;

                label_displayed_Id.Text = session.Id.ToString(CultureInfo.InvariantCulture);
                comboBox_http_method.SelectedItem = session.Method;
                toolStripComboBox_http_version.SelectedItem = session.RequestHttpVersion;

                //if session has different certificate than is available, let user know
                comboBox_certificates.BackColor = SystemColors.Window;
                comboBox_certificates.ForeColor = SystemColors.WindowText;

                //Clear first, otherwise the previous session's certificate stays selected.
                comboBox_certificates.SelectedItem = null;
                comboBox_certificates.Text = string.Empty;

                foreach (var item in comboBox_certificates.Items)
                {
                    if (item.ToString().Equals(session.ClientCertSubject, StringComparison.OrdinalIgnoreCase))
                        comboBox_certificates.SelectedItem = item;
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
                else if (comboBox_group.Items.Count > 0)
                {
                    comboBox_group.SelectedIndex = 0;
                }

                var pretty_req = PrettyPrint(session.RequestBody);
                if (pretty_req[0, 0].Equals("JSON", StringComparison.Ordinal)) textBox_request_body.Language = Language.JSON;
                if (pretty_req[0, 0].Equals("XML", StringComparison.Ordinal)) textBox_request_body.Language = Language.XML;
                textBox_request_body.Clear();
                textBox_request_body.InsertText(pretty_req[0, 1]);

                textBox_request_headers.Clear();
                textBox_request_headers.InsertText(session.RequestHeaders);

                textBox_request_url.Clear();
                textBox_request_url.InsertText(session.UriAbsoluteUri);

                //Response is stored zstd compressed
                var ResponseBody_string = Unzip(session.ResponseBody);

                var pretty = PrettyPrint(ResponseBody_string);
                if (pretty[0, 0].Equals("JSON", StringComparison.Ordinal)) textBox_response_body.Language = Language.JSON;
                if (pretty[0, 0].Equals("XML", StringComparison.Ordinal)) textBox_response_body.Language = Language.XML;

                textBox_response_body.Clear();
                textBox_response_body.InsertText(pretty[0, 1]);

                textBox_response_headers.Clear();
                textBox_response_headers.InsertText(session.ResponseHeaders);

                //Additional info in the status bar
                double responseKb = Math.Round(session.ResponseLength / 1024.0, 2);

                toolStripStatusLabel_response_stats_http_version.Text = "HTTP " + session.ResponseHttpVersion;
                toolStripStatusLabel_response_stats_datetime.Text = " " + session.DateTime;
                toolStripStatusLabel_response_stats_response_time.Text = " " + session.ResponseTime.ToString(CultureInfo.CurrentCulture) + "ms  " + responseKb.ToString(CultureInfo.CurrentCulture) + " kB";

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
                else
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
                entryListItem.Font = StatsBoldFont;
                entryListItem.UseItemStyleForSubItems = false;
                ListViewItem.ListViewSubItem expenseItem = entryListItem.SubItems.Add(Ms(session.DurationRequest));
                expenseItem.Font = StatsBoldFont;


                listView1.Items.Add(new ListViewItem(new[] { "DNS resolution duration (ms)", Ms(session.DurationResolution) }));
                listView1.Items.Add(new ListViewItem(new[] { "TCP connect (ms)", Ms(session.DurationConnect) }));
                listView1.Items.Add(new ListViewItem(new[] { "TLS handshake duration (ms)", Ms(session.DurationHandshake) }));

                listView1.Items.Add(new ListViewItem(new[] { "Request header (ms)", Ms(session.DurationRequestHeaders) }));
                listView1.Items.Add(new ListViewItem(new[] { "Request content (ms)", Ms(session.DurationRequestContent) }));
                listView1.Items.Add(new ListViewItem(new[] { "Response headers (ms)", Ms(session.DurationResponseHeaders) }));
                listView1.Items.Add(new ListViewItem(new[] { "Response content (ms)", Ms(session.DurationResponseContent) }));

                listView1.Items.Add(new ListViewItem(BlankStatsRow));
                listView1.Items.Add(new ListViewItem(new[] { "Request date", session.DateTime }));
                listView1.Items.Add(new ListViewItem(new[] { "Response HTTP version", session.ResponseHttpVersion }));
                listView1.Items.Add(new ListViewItem(new[] { "Response size (kB)", responseKb.ToString(CultureInfo.CurrentCulture) }));

                listView1.Items.Add(new ListViewItem(BlankStatsRow));
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
            }
            finally
            {
                CursorWait(false);
            }
        }
    }
}
