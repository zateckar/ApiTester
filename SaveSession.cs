using System;
using System.Collections.Generic;
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
            CursorWait(true);

            var sb = new StringBuilder();
            foreach (var header in response.Headers)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            foreach (var header in response.TrailingHeaders)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            foreach (var header in response.Content.Headers)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            string requestHeaders = String.Empty;

            //Content is null for a bodyless GET/HEAD.
            foreach (var item in request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            {
                string key = item.Key;
                if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;

                string val = item.Value.FirstOrDefault() ?? string.Empty;
                requestHeaders += key + ": " + val + Environment.NewLine;
            }

            foreach (var item in request.Headers)
            {
                string key = item.Key;
                if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;

                string val = item.Value.FirstOrDefault() ?? string.Empty;
                requestHeaders += key + ": " + val + Environment.NewLine;
            }

            //Response can be quite large - need to compress it.
            var ResponseBody_string = await response.Content.ReadAsStringAsync();
            var ResponseBody_zip = Zip(ResponseBody_string);

            //Content-Length is absent on chunked and most HTTP/2 responses - fall back to the body we actually received.
            int responseLength = (int)(response.Content.Headers.ContentLength ?? Encoding.UTF8.GetByteCount(ResponseBody_string));

            var session = new Session()
            {
                DateTime = DateTime.Now.ToString("s"),
                RequestHeaders = requestHeaders,
                RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(),
                Method = request.Method.Method,
                UriAbsoluteUri = request.RequestUri.AbsoluteUri,
                UriAbsolutePath = request.RequestUri.AbsolutePath,
                UriQuery = request.RequestUri.Query,
                UriHost = request.RequestUri.Host,
                ResponseBody = ResponseBody_zip,
                ResponseHeaders = sb.ToString(),
                ResponseTime = (int)watch.ElapsedMilliseconds,

                ResponseLength = responseLength,
                ResponseStatusCode = (int)response.StatusCode,
                ResponseHttpVersion = response.Version.ToString(),
                RequestHttpVersion = toolStripComboBox_http_version.Text,
                Group = comboBox_group.Text,

                DurationRequest = RequestTelemetry.Duration(requestTelemetry.RequestStart, requestTelemetry.RequestStop),
                DurationResolution = RequestTelemetry.Duration(requestTelemetry.ResolutionStart, requestTelemetry.ResolutionStop),
                DurationConnect = RequestTelemetry.Duration(requestTelemetry.ConnectStart, requestTelemetry.ConnectStop),
                DurationHandshake = RequestTelemetry.Duration(requestTelemetry.HandshakeStart, requestTelemetry.HandshakeStop),

                DurationRequestHeaders = RequestTelemetry.Duration(requestTelemetry.RequestHeadersStart, requestTelemetry.RequestHeadersStop),
                DurationRequestContent = RequestTelemetry.Duration(requestTelemetry.RequestContentStart, requestTelemetry.RequestContentStop),
                DurationResponseHeaders = RequestTelemetry.Duration(requestTelemetry.ResponseHeadersStart, requestTelemetry.ResponseHeadersStop),
                DurationResponseContent = RequestTelemetry.Duration(requestTelemetry.ResponseContentStart, requestTelemetry.ResponseContentStop),

                //Identifies the session to the other instances, and marks it as theirs to fetch.
                Uid = NewUid(),
                UpdatedUtc = SyncRow.NowUtc(),
                Dirty = true,
            };

            if (session.UriAbsoluteUri.Equals(serverCertificate.RequestUri, StringComparison.OrdinalIgnoreCase))
            {
                session.ServerCertSubject = serverCertificate.Subject;
                session.ServerCertIssuer = serverCertificate.Issuer;
                session.ServerCertValidFrom = serverCertificate.ValidFrom;
                session.ServerCertValidTo = serverCertificate.ValidTo;
                session.ServerCertIsValid = serverCertificate.IsValid;
            }

            if (comboBox_certificates.Text.Length > 0 && handler.ClientCertificates.Count > 0)
            {
                session.ClientCertSubject = handler.ClientCertificates[0].Subject;
            }

            try
            {
                //InsertAsync assigns session.Id itself (Database.InsertCore). Re-fetching the
                //"latest" row instead would race a sync pull inserting one in between, and the
                //grid would append somebody else's session.
                await sessionsConn.InsertAsync(session);
            }
            catch (Exception ex)
            {
                CursorWait(false);
                MessageBox.Show(ex.Message);
                return;
            }

            //Append to the model and repaint; RefreshGrid handles suppressing note persistence.
            await AppendSessionRow(session);

            //Publish it, along with anything else still waiting.
            RequestSync();

            SelectLastGridRow();

            //display content of the newly saved session
            if (dataGridView1.RowCount > 0) await DisplaySession(dataGridView1.RowCount - 1);

            CursorWait(false);
        }

        /// <summary>
        /// Scrolls to and selects the most recent row in the session grid.
        /// </summary>
        private void SelectLastGridRow()
        {
            if (dataGridView1.RowCount == 0) return;

            int last = dataGridView1.RowCount - 1;

            dataGridView1.FirstDisplayedScrollingRowIndex = last;

            //The current cell moves too, not just the selection: it is what the grid keeps hold
            //of across a repaint, so leaving it behind would send the user back to the top the
            //next time the sync brings a session in.
            SelectViewRow(last);
        }
    }
}
