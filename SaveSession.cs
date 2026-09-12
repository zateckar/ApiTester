using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {

        /// <param name="buffered">What ContentCap actually received - size, whether it was cut
        /// at the cap, and whether it is text at all.</param>
        /// <param name="serverCertificate">What the server presented on this request's
        /// handshake. Passed in rather than read off a field: the handshake runs on a pool
        /// thread and a shared instance belongs to whichever request finished last.</param>
        /// <param name="display">
        /// Refresh the panes for the session just saved. A repeat loop passes false on every
        /// iteration but the last: re-parsing and re-indenting both bodies on the UI thread
        /// only to be overwritten by the next iteration is wasted work, and the telemetry of an
        /// intermediate iteration is overwritten before anyone can read it.
        /// </param>
        //Internal, not public: ContentCap.Buffered is, and nothing outside the assembly calls this.
        internal async Task SaveSession(HttpRequestMessage request, HttpResponseMessage response, System.Diagnostics.Stopwatch watch, HttpClientHandler handler, RequestTelemetry requestTelemetry, ContentCap.Buffered buffered, ServerCertificate serverCertificate, bool display)
        {
            CursorWait(true);

            var sb = new StringBuilder();
            foreach (var header in response.Headers)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            foreach (var header in response.TrailingHeaders)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            foreach (var header in response.Content.Headers)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            var requestHeaders = new StringBuilder();

            //Content is null for a bodyless GET/HEAD.
            foreach (var item in request.Content?.Headers ?? Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
            {
                string key = item.Key;
                if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;

                string val = item.Value.FirstOrDefault() ?? string.Empty;
                requestHeaders.Append(key).Append(": ").AppendLine(val);
            }

            foreach (var item in request.Headers)
            {
                string key = item.Key;
                if (key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) continue;

                string val = item.Value.FirstOrDefault() ?? string.Empty;
                requestHeaders.Append(key).Append(": ").AppendLine(val);
            }

            //The store keeps response bodies as text. Decoding an image or a protobuf into a
            //string yields replacement characters, which read as a corrupted response rather
            //than a binary one - so what is stored says plainly what arrived instead.
            string ResponseBody_string = buffered.IsText
                ? await response.Content.ReadAsStringAsync()
                : BinaryBodyPlaceholder(buffered);

            //Response can be quite large - need to compress it.
            var ResponseBody_zip = Zip(ResponseBody_string);

            //Measured by ContentCap, not read off Content-Length: that header is absent on
            //chunked and most HTTP/2 responses, and only a claim when it is there. Counting
            //the decoded string instead would be wrong for any charset that is not UTF-8.
            //The failure paths in SendRequest build their own body and report no bytes.
            int responseLength = buffered.Bytes > 0
                ? (int)Math.Min(buffered.Bytes, int.MaxValue)
                : (int)(response.Content.Headers.ContentLength ?? Encoding.UTF8.GetByteCount(ResponseBody_string));

            var session = new Session()
            {
                DateTime = DateTime.Now.ToString("s"),
                RequestHeaders = requestHeaders.ToString(),
                RequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(),
                Method = request.Method.Method,
                UriAbsoluteUri = request.RequestUri.AbsoluteUri,
                UriAbsolutePath = request.RequestUri.AbsolutePath,
                UriQuery = request.RequestUri.Query,
                UriHost = request.RequestUri.Host,
                ResponseBody = ResponseBody_zip,
                ResponseHeaders = sb.ToString(),
                ResponseTime = (int)watch.ElapsedMilliseconds,
                Truncated = buffered.Truncated,

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

            //By id, not by position. With a text or group filter on, or the duplicates view,
            //the session just saved is not necessarily the last row - and taking the last row
            //selected an unrelated session and loaded it into the panes as if it were this one.
            int index = SelectSessionRow(session.Id);

            //Display only when the caller asked for it - a repeat loop saves every iteration
            //but only the last one's panes are worth rendering. A session the current filters
            //exclude has no row to show, and the panes keep what they had.
            if (display && index >= 0) await DisplaySession(index);

            CursorWait(false);
        }

        /// <summary>
        /// Describes a body the text store cannot hold, in place of the mojibake that decoding
        /// it would produce.
        /// </summary>
        private static string BinaryBodyPlaceholder(ContentCap.Buffered buffered)
            => "[binary response body - " + (buffered.MediaType ?? "unknown content type")
               + ", " + buffered.Bytes.ToString(CultureInfo.CurrentCulture) + " bytes."
               + " Not stored: the session store keeps response bodies as text.]";

        /// <summary>
        /// Scrolls to and selects the most recent row in the session grid. Only correct where
        /// "newest" really is the last row, which is the case on load; a freshly saved session
        /// goes through <see cref="SelectSessionRow"/>, because a filter can put it anywhere.
        /// </summary>
        private void SelectLastGridRow()
        {
            if (dataGridView1.RowCount == 0) return;

            //The current cell moves too, not just the selection: it is what the grid keeps hold
            //of across a repaint, so leaving it behind would send the user back to the top the
            //next time the sync brings a session in. Setting it also scrolls it into view.
            SelectViewRow(dataGridView1.RowCount - 1);
        }

        /// <summary>
        /// Selects the row showing one session, wherever the current filters and grouping have
        /// put it.
        /// </summary>
        /// <returns>Its row index, or -1 when the session is not in the current view.</returns>
        private int SelectSessionRow(int id)
        {
            int index = viewRows.FindIndex(r => r.Id == id);

            if (index < 0) return -1;

            SelectViewRow(index);

            return index;
        }
    }
}
