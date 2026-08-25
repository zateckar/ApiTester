using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        //HttpClient defaults to 100s, which cut off slow endpoints mid-response.
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(240);

        public async Task SendRequest(string requestBody, string requestHeaders, string httpMethod, string requestUrl, string httpVersion, string certificate)
        {
            CursorWait(true);

            serverCertificate = new ServerCertificate();

            //Each request gets a listener of its own with its own telemetry instance. A
            //process-wide EventSource feeds every live listener every System.Net event -
            //shared state would let the sync's requests overwrite a request in flight, and
            //stages that do not fire (a pooled connection skips DNS/TCP/TLS) keep the fresh
            //zero values rather than the previous request's.
            using var eventSourceListener = new NetEventListener();

            using HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = ServerCertificateCustomValidation;

            if (certificate.Length > 0)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.AllowAutoRedirect = true;
                handler.SslProtocols = SslProtocols.None;

                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    X509Certificate2 clientCert = FindCert(store, certificate);

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
            StringContent content = new StringContent(requestBody);
            content.Headers.Remove("Content-Type");

            using (StringReader reader = new StringReader(requestHeaders))
            {
                string s;
                while ((s = reader.ReadLine()) != null)
                {
                    if (s.Contains(':'))
                    {
                        int pom1 = s.IndexOf(':');
                        string key = s.Substring(0, pom1).Trim();
                        string value = s.Substring(pom1 + 1).Trim();

                        //add authorization headers
                        bool headerAdded = request.Headers.TryAddWithoutValidation(key, value);

                        if (headerAdded == false)
                        {
                            //add content-type and other content related headers
                            content.Headers.TryAddWithoutValidation(key, value);
                        }
                    }
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

            //Long requests are otherwise invisible - nothing but a wait cursor says one is
            //still in flight. The placeholder is dropped again before the saved session is
            //appended, so the grid never shows both.
            PendingRequest pending = BeginPendingRow(httpMethod, requestUri);

            try
            {
                watch.Start();
                sent = await client.SendAsync(request);
                watch.Stop();
                await sent.Content.LoadIntoBufferAsync();
            }
            catch (HttpRequestException ex)
            {
                watch.Stop();
                response.Content = new StringContent("Request failed: " + ex.Message);
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
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
                EndPendingRow(pending);
            }

            //Response processing - must complete before request/response/handler are disposed,
            //because SaveSession reads their content and headers.
            try
            {
                await SaveSession(request, sent ?? response, watch, handler, eventSourceListener.Telemetry);
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

        private static bool ServerCertificateCustomValidation(HttpRequestMessage requestMessage, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslErrors)
        {
            serverCertificate.RequestUri = requestMessage.RequestUri.AbsoluteUri;
            serverCertificate.ValidFrom = certificate.GetEffectiveDateString();
            serverCertificate.ValidTo = certificate.GetExpirationDateString();
            serverCertificate.Subject = certificate.Subject;
            serverCertificate.Issuer = certificate.Issuer;
            serverCertificate.IsValid = certificate.Verify();

            return sslErrors == SslPolicyErrors.None;
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
            System.Diagnostics.Debug.WriteLine(eventData.EventName + ": " + eventData.TimeStamp.ToString("o"));

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
