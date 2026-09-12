using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        //HttpClient defaults to 100s, which cut off slow endpoints mid-response.
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(240);

        //Saved with a request that collected no telemetry: every stage reads as zero,
        //the same shape a fully pooled connection already produces.
        private static readonly RequestTelemetry NoTelemetry = new();

        /// <param name="collectTelemetry">
        /// Each request gets a listener of its own with its own telemetry instance. A
        /// process-wide EventSource feeds every live listener every System.Net event -
        /// shared state would let the sync's requests overwrite a request in flight, and
        /// stages that do not fire (a pooled connection skips DNS/TCP/TLS) keep the fresh
        /// zero values rather than the previous request's. A repeat loop enables the
        /// listener only on its last iteration: the intermediate timings are overwritten
        /// by the next request anyway, and an enabled listener bills every System.Net
        /// event process-wide for its whole lifetime.
        /// </param>
        /// <param name="cancellationToken">
        /// Cancelled by the Cancel affordance on the send button, or Esc. A cancelled request
        /// stores nothing: the user asked for it to stop, not to be recorded.
        /// </param>
        public async Task SendRequest(string requestBody, string requestHeaders, string httpMethod, string requestUrl, string httpVersion, string certificate, CancellationToken cancellationToken, bool collectTelemetry = true, bool display = true)
        {
            CursorWait(true);

            //Per request, not a field: the validation callback runs on a pool thread during the
            //handshake, so a shared instance lets a second request in flight - or the sync's own
            //client - overwrite the certificate before SaveSession has read it.
            var certificateSeen = new ServerCertificate();

            using var eventSourceListener = collectTelemetry ? new NetEventListener() : null;

            using HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                (message, cert, chain, errors) => CaptureServerCertificate(certificateSeen, message, cert, errors);

            //The default headers advertise gzip/deflate/br - without this those bodies would
            //arrive still compressed and be stored undisplayable under a second compression.
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli;

            if (certificate.Length > 0)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.AllowAutoRedirect = true;
                handler.SslProtocols = SslProtocols.None;

                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

                try
                {
                    store.Open(OpenFlags.ReadOnly);

                    //Resolve to the exact thumbprint captured when the combo was filled. The old
                    //substring search picked any cert whose subject contained the text - two
                    //overlapping subjects differ only in which credential TLS presents.
                    X509Certificate2 clientCert = null;

                    if (certificateThumbprints.TryGetValue(certificate, out (string Thumbprint, DateTime Expires) selected))
                    {
                        clientCert = store.Certificates
                            .Find(X509FindType.FindByThumbprint, selected.Thumbprint, validOnly: false)
                            .OfType<X509Certificate2>()
                            .FirstOrDefault();
                    }

                    if (clientCert is null)
                    {
                        CursorWait(false);
                        MessageBox.Show("Can´t retrieve selected certificate from your local certificate store.");
                        return;
                    }

                    handler.ClientCertificates.Add(clientCert);
                }
                catch (Exception ex) when (ex is CryptographicException or System.Security.SecurityException)
                {
                    CursorWait(false);
                    MessageBox.Show("Can´t retrieve selected certificate from your local certificate store.\n\n" + ex.Message);
                    return;
                }
            }

            using HttpClient client = new HttpClient(handler) { Timeout = RequestTimeout };

            using HttpRequestMessage request = new HttpRequestMessage();

            //Read the header block before building the content: the body's encoding comes out
            //of the user's own Content-Type, so it has to be known first. StringContent used
            //to encode as UTF-8 whatever charset the header asked for, which silently mangled
            //a windows-1250 payload on the way to a legacy endpoint.
            List<(string Key, string Value)> headerLines = ParseHeaderLines(requestHeaders);

            StringContent content = new StringContent(requestBody, BodyEncoding(headerLines));

            //StringContent set one from the encoding; the user's own header is applied below.
            content.Headers.Remove("Content-Type");

            foreach ((string key, string value) in headerLines)
            {
                //add authorization headers
                bool headerAdded = request.Headers.TryAddWithoutValidation(key, value);

                if (headerAdded == false)
                {
                    //add content-type and other content related headers
                    content.Headers.TryAddWithoutValidation(key, value);
                }
            }

            request.Method = new HttpMethod(httpMethod);

            if (!Uri.TryCreate(requestUrl, UriKind.Absolute, out Uri requestUri))
            {
                CursorWait(false);
                MessageBox.Show("\"" + requestUrl + "\" is not a valid absolute URL.");
                return;
            }

            request.RequestUri = requestUri;

            //Some servers reject a bodyless GET/HEAD that still carries a Content-Length.
            //Only attach content when there is something to send, or the verb expects a body.
            bool bodylessVerb = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
            if (!bodylessVerb || requestBody.Length > 0) request.Content = content;
            else content.Dispose();

            request.Version = ConvertHttpVersion(httpVersion);
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

            using HttpResponseMessage response = new HttpResponseMessage();
            var watch = new System.Diagnostics.Stopwatch();

            HttpResponseMessage sent = null;

            //What the failure paths below store: a body they wrote themselves, never truncated,
            //always text.
            var buffered = new ContentCap.Buffered(false, 0, true, null);
            bool canceled = false;

            //Long requests are otherwise invisible - nothing but a wait cursor says one is
            //still in flight. The placeholder is dropped again before the saved session is
            //appended, so the grid never shows both.
            PendingRequest pending = BeginPendingRow(httpMethod, requestUri);

            try
            {
                watch.Start();
                sent = await client.SendAsync(request, cancellationToken);
                watch.Stop();
                buffered = await ContentCap.BufferAsync(sent, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                watch.Stop();
                response.Content = new StringContent("Request failed: " + ex.Message);
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
            }
            //The user stopped it. Has to be told apart from the client's own timeout, which
            //surfaces as the same exception type - hence the token check rather than the
            //InnerException one below. Filtered, so the derived catches after it stay legal.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                watch.Stop();
                canceled = true;
            }
            // Filter by InnerException.
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // Handle timeout.
                watch.Stop();
                response.Content = new StringContent("Timed out: " + ex.Message);
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
            }
            catch (TaskCanceledException ex)
            {
                // Handle cancellation.
                watch.Stop();
                response.Content = new StringContent("Canceled: " + ex.Message);
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
            }
            finally
            {
                //Awaited, not async-void fired: a refresh that throws must not vanish, and the
                //placeholder must be gone before the saved session's row is appended.
                await EndPendingRow(pending);
            }

            //Cancelled: no response was received and none is invented. Storing a row for it
            //would fill the grid with sessions the user deliberately abandoned.
            if (canceled)
            {
                sent?.Dispose();
                CursorWait(false);
                return;
            }

            //Response processing - must complete before request/response/handler are disposed,
            //because SaveSession reads their content and headers.
            try
            {
                //Without a listener the durations are zero, which is what a pooled connection's
                //skipped stages already read as - the previous request's numbers would be worse.
                await SaveSession(request, sent ?? response, watch, handler, eventSourceListener?.Telemetry ?? NoTelemetry, buffered, certificateSeen, display);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the session: " + ex.Message);
            }
            finally
            {
                sent?.Dispose();
            }

            CursorWait(false);
        }

        private void CursorWait(bool wait)
        {
            Cursor cursor = wait ? Cursors.WaitCursor : Cursors.Default;

            this.Cursor = cursor;
            dataGridView1.Cursor = cursor;
            textBox_request_body.Cursor = cursor;
            textBox_request_headers.Cursor = cursor;
            textBox_response_body.Cursor = cursor;
            textBox_response_headers.Cursor = cursor;
            textBox_request_url.Cursor = cursor;
        }

        /// <summary>
        /// Records what the server presented, into the instance belonging to this request.
        /// Runs on whichever thread is driving the handshake.
        /// </summary>
        private static bool CaptureServerCertificate(ServerCertificate into, HttpRequestMessage requestMessage, X509Certificate2 certificate, SslPolicyErrors sslErrors)
        {
            //An anonymous-cipher or otherwise certificate-less handshake still reaches here.
            if (certificate is null) return sslErrors == SslPolicyErrors.None;

            into.RequestUri = requestMessage.RequestUri.AbsoluteUri;
            into.ValidFrom = certificate.GetEffectiveDateString();
            into.ValidTo = certificate.GetExpirationDateString();
            into.Subject = certificate.Subject;
            into.Issuer = certificate.Issuer;
            into.IsValid = certificate.Verify();

            return sslErrors == SslPolicyErrors.None;
        }

        /// <summary>
        /// Splits the header pane's text into key/value pairs. Lines without a colon are not
        /// headers and are skipped, as they always were.
        /// </summary>
        private static List<(string Key, string Value)> ParseHeaderLines(string requestHeaders)
        {
            var parsed = new List<(string, string)>();

            using StringReader reader = new StringReader(requestHeaders);

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                int colon = line.IndexOf(':');
                if (colon < 0) continue;

                parsed.Add((line.Substring(0, colon).Trim(), line.Substring(colon + 1).Trim()));
            }

            return parsed;
        }

        /// <summary>
        /// The encoding the request body is written in, taken from the charset parameter of the
        /// user's own Content-Type. UTF-8 when there is none, or when the name is one the
        /// runtime does not know - a bad charset is not worth failing the whole request over.
        /// </summary>
        private static Encoding BodyEncoding(List<(string Key, string Value)> headerLines)
        {
            foreach ((string key, string value) in headerLines)
            {
                if (!key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)) continue;

                int at = value.IndexOf("charset=", StringComparison.OrdinalIgnoreCase);
                if (at < 0) break;

                string name = value.Substring(at + "charset=".Length).Trim();

                //"application/json; charset=utf-8; v=2" - the charset ends at the next parameter.
                int end = name.IndexOf(';');
                if (end >= 0) name = name.Substring(0, end);

                name = name.Trim().Trim('"');
                if (name.Length == 0) break;

                try
                {
                    return Encoding.GetEncoding(name);
                }
                catch (ArgumentException)
                {
                    break;
                }
            }

            return Encoding.UTF8;
        }
    }



    public sealed class NetEventListener : EventListener
    {
        //Per listener, not shared: the System.Net event sources are process-wide, so any other
        //HttpClient in the process (the sync's, for one) fires here too while it is enabled.
        internal RequestTelemetry Telemetry { get; } = new();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith("System.Net", StringComparison.Ordinal))
                EnableEvents(eventSource, EventLevel.Informational);
        }
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            RequestTelemetry telemetry = Telemetry;

            switch (eventData.EventName)
            {
                case "RequestStart": telemetry.RequestStart = eventData.TimeStamp; break;
                case "RequestStop": telemetry.RequestStop = eventData.TimeStamp; break;

                case "ResolutionStart": telemetry.ResolutionStart = eventData.TimeStamp; break;
                case "ResolutionStop": telemetry.ResolutionStop = eventData.TimeStamp; break;

                case "ConnectStart": telemetry.ConnectStart = eventData.TimeStamp; break;
                case "ConnectStop": telemetry.ConnectStop = eventData.TimeStamp; break;

                case "HandshakeStart": telemetry.HandshakeStart = eventData.TimeStamp; break;
                case "HandshakeStop": telemetry.HandshakeStop = eventData.TimeStamp; break;

                case "RequestHeadersStart": telemetry.RequestHeadersStart = eventData.TimeStamp; break;
                case "RequestHeadersStop": telemetry.RequestHeadersStop = eventData.TimeStamp; break;

                case "RequestContentStart": telemetry.RequestContentStart = eventData.TimeStamp; break;
                case "RequestContentStop": telemetry.RequestContentStop = eventData.TimeStamp; break;

                case "ResponseHeadersStart": telemetry.ResponseHeadersStart = eventData.TimeStamp; break;
                case "ResponseHeadersStop": telemetry.ResponseHeadersStop = eventData.TimeStamp; break;

                case "ResponseContentStart": telemetry.ResponseContentStart = eventData.TimeStamp; break;
                case "ResponseContentStop": telemetry.ResponseContentStop = eventData.TimeStamp; break;
            }
        }
    }

}
