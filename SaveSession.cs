using SQLite;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        public async Task SaveSession(HttpRequestMessage request, HttpResponseMessage response, System.Diagnostics.Stopwatch watch, HttpClientHandler handler, RequestTelemetry requestTelemetry)
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;

            var sb = new StringBuilder();
            foreach (var header in response.Headers)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            foreach (var header in response.TrailingHeaders)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            foreach (var header in response.Content.Headers)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            //var sr = new StringBuilder();
            string requestHeaders = String.Empty;
            foreach (var item in request.Content.Headers)
            {
                //sr.AppendLine(item.Key == "Set-Cookie" ? $"{item.Key}: {string.Join("\r\nSet-Cookie: ", item.Value)}" : $"{item.Key}: {string.Join(", ", item.Value)}");

                string key = item.Key;
                if (key.Equals("Content-Length")) continue;

                string val = item.Value.First();
                requestHeaders += key + ": " + val + Environment.NewLine;
            }

            foreach (var item in request.Headers)
            {
                string key = item.Key;
                if (key.Equals("Content-Length")) continue;

                string val = item.Value.First();
                requestHeaders += key + ": " + val + Environment.NewLine;
            }

            //Response can be quite large - need to compress it.
            var ResponseBody_string = await response.Content.ReadAsStringAsync();
            var ResponseBody_zip = Zip(ResponseBody_string);
            //var ResponseBody_base64 = Convert.ToBase64String(ResponseBody_zip);

            var session = new Session()
            {
                DateTime = DateTime.Now.ToString("s"),
                RequestHeaders = requestHeaders,
                RequestBody = await request.Content.ReadAsStringAsync(),
                Method = request.Method.Method,
                UriAbsoluteUri = request.RequestUri.AbsoluteUri,
                UriAbsolutePath = request.RequestUri.AbsolutePath,
                UriQuery = request.RequestUri.Query,
                UriHost = request.RequestUri.Host,
                ResponseBody = ResponseBody_zip,
                ResponseHeaders = sb.ToString(),
                ResponseTime = (int)watch.ElapsedMilliseconds,

                ResponseLength = Convert.ToInt32(response.Content.Headers.ContentLength.Value),
                ResponseStatusCode = (int)response.StatusCode,
                ResponseHttpVersion = response.Version.ToString(),
                RequestHttpVersion = toolStripComboBox_http_version.Text,
                Group = comboBox_group.Text,

                DurationRequest = (requestTelemetry.RequestStop - requestTelemetry.RequestStart).TotalMilliseconds,
                DurationResolution = (requestTelemetry.ResolutionStop - requestTelemetry.ResolutionStart).TotalMilliseconds,
                DurationConnect = (requestTelemetry.ConnectStop - requestTelemetry.ConnectStart).TotalMilliseconds,
                DurationHandshake = (requestTelemetry.HandshakeStop - requestTelemetry.HandshakeStart).TotalMilliseconds,
               
                DurationRequestHeaders = (requestTelemetry.RequestHeadersStop - requestTelemetry.RequestHeadersStart).TotalMilliseconds,
                DurationRequestContent = (requestTelemetry.RequestContentStop - requestTelemetry.RequestContentStart).TotalMilliseconds,
                DurationResponseHeaders = (requestTelemetry.ResponseHeadersStop - requestTelemetry.ResponseHeadersStart).TotalMilliseconds,
                DurationResponseContent = (requestTelemetry.ResponseContentStop - requestTelemetry.ResponseContentStart).TotalMilliseconds,

            };

            if (session.UriAbsoluteUri.Equals(serverCertificate.RequestUri))
            {
                session.ServerCertSubject = serverCertificate.Subject;
                session.ServerCertIssuer = serverCertificate.Issuer;
                session.ServerCertValidFrom = serverCertificate.ValidFrom;
                session.ServerCertValidTo = serverCertificate.ValidTo;
                session.ServerCertIsValid = serverCertificate.IsValid;
            }

            if (comboBox_certificates.Text.Length > 0)
            {
                session.ClientCertSubject = handler.ClientCertificates[0].Subject;
            }

            try
            {
                await sessionsConn.InsertAsync(session);
                var localVersion = Convert.ToInt32(await sessionsConn.ExecuteScalarAsync<int>("pragma user_version;"));
                await sessionsConn.ExecuteAsync($"pragma user_version = "+ (localVersion+1) +";");
            }
            catch (Exception ex)
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
                MessageBox.Show(ex.Message);
            }

            AsyncTableQuery<Session> query = sessionsConn.Table<Session>();
            session = await query.OrderByDescending(c => c.Id).FirstOrDefaultAsync();

            //dtSessions.Rows.Add(new object[] { session.Id, session.DateTime, session.ResponseStatusCode, session.Method, session.UriHost, session.UriAbsolutePath, session.Note, session.Group });
            dtSessions.Rows.Add(new object[] { session.Id, session.DateTime, session.ResponseStatusCode, session.Method +" "+ session.UriHost, session.UriAbsolutePath, session.Note, session.Group });

            dataGridView1.SuspendLayout();
            dataGridView1.DataSource = dtSessions;
            dataGridView1.ResumeLayout();

            //scroll to the new row
            dataGridView1.FirstDisplayedScrollingRowIndex = dataGridView1.RowCount - 1;

            //select new row
            dataGridView1.CurrentRow.Selected = false;
            dataGridView1.Rows[dataGridView1.Rows.Count - 1].Selected = true;

            //display content of the newly saved session
            await DisplaySession(dataGridView1.Rows.Count - 1);

            this.Cursor = System.Windows.Forms.Cursors.Default;
        }
    }
}