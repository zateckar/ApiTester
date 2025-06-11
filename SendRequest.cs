using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        public static RequestTelemetry _requestTelemetry = new();
        public async Task SendRequest(string request_body, string request_headers, string http_method, string request_url, string http_version, string certificate)
        {
 
            CursorWait(true);

            using var eventSourceListener = new NetEventListener();

            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = ServerCertificateCustomValidation;

            HttpClient client = new HttpClient(handler);

            //var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyData.settingsConn");
            HttpRequestMessage request = new HttpRequestMessage();
            StringContent content = new StringContent(request_body);
            content.Headers.Remove("Content-Type");


            using (StringReader reader = new StringReader(request_headers))
            {
                string s;
                while ((s = reader.ReadLine()) != null)
                {
                    if (s.Contains(":"))
                    {
                        int pom1 = s.IndexOf(":");
                        string key = s.Substring(0, pom1).Trim();
                        string value = s.Substring(pom1 + 1, s.Length - (pom1 + 1)).Trim();

                        //add authorization headers
                        bool headerAdded = request.Headers.TryAddWithoutValidation(key, value);

                        if (headerAdded == false)
                        {
                            //add content-type and other content relatedt headers
                            content.Headers.TryAddWithoutValidation(key, value);
                        }
                    }
                }
            }

            //Probably no need to automatically add a trace header....
            //if (content.Headers.Contains("traceparent") == false) content.Headers.Add("traceparent", GetTraceparent());

            request.Method = new HttpMethod(http_method);
            request.RequestUri = new Uri(request_url);
            request.Content = content;
            request.Version = ConvertHttpVersion(http_version);
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

            if (certificate.Length > 0)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.AllowAutoRedirect = true;
                handler.SslProtocols = SslProtocols.None;

                var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    X509Certificate2 clientCert = FindCert(store, certificate);
                    handler.ClientCertificates.Add(clientCert);
                }
                catch (Exception)
                {
                    CursorWait(false);
                    MessageBox.Show("Can´t retrieve selected certificate from your local certificate store.");
                }
                store.Dispose();

            }

            HttpResponseMessage response = new HttpResponseMessage();
            var watch = new System.Diagnostics.Stopwatch();

            try
            {
                watch.Start();
                response = await client.SendAsync(request);
                watch.Stop();
                await response.Content.LoadIntoBufferAsync();

            }
            catch (HttpRequestException)
            {
                CursorWait(false);
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
            }
            // Filter by InnerException.
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                // Handle timeout.
                response.Content = new StringContent("Timed out: " + ex.Message);
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
                CursorWait(false);
            }
            catch (TaskCanceledException ex)
            {
                // Handle cancellation.
                response.Content = new StringContent("Canceled: " + ex.Message);
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
                CursorWait(false);
            }

            //Response processing//
            SaveSession(request, response, watch, handler, _requestTelemetry);

            request.Dispose();
            response.Dispose();
            handler.Dispose();
            client.Dispose();

            CursorWait(false);
        }

        private void CursorWait(bool wait)
        {
         if(wait)
            {
                this.Cursor = Cursors.WaitCursor;
                dataGridView1.Cursor = Cursors.WaitCursor;
                textBox_request_body.Cursor = Cursors.WaitCursor;
                textBox_request_headers.Cursor = Cursors.WaitCursor;
                textBox_response_body.Cursor = Cursors.WaitCursor;
                textBox_response_headers.Cursor = Cursors.WaitCursor;
                textBox_request_url.Cursor = Cursors.WaitCursor;
            }
            else
            {
                this.Cursor = Cursors.Default;
                dataGridView1.Cursor = Cursors.Default;
                textBox_request_body.Cursor = Cursors.Default;
                textBox_request_headers.Cursor = Cursors.Default;
                textBox_response_body.Cursor = Cursors.Default;
                textBox_response_headers.Cursor = Cursors.Default;
                textBox_request_url.Cursor = Cursors.Default;
            }
            
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
        
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.StartsWith("System.Net"))
                EnableEvents(eventSource, EventLevel.Informational);
        }
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            System.Diagnostics.Debug.WriteLine(eventData.EventName + ": " + eventData.TimeStamp.ToString("o"));
            
            if (eventData.EventName == "RequestStart") Form1._requestTelemetry.RequestStart = eventData.TimeStamp;
            if (eventData.EventName == "RequestStop") Form1._requestTelemetry.RequestStop = eventData.TimeStamp;

            if (eventData.EventName == "ResolutionStart") Form1._requestTelemetry.ResolutionStart = eventData.TimeStamp;
            if (eventData.EventName == "ResolutionStop") Form1._requestTelemetry.ResolutionStop = eventData.TimeStamp;

            if (eventData.EventName == "ConnectStart") Form1._requestTelemetry.ConnectStart = eventData.TimeStamp;
            if (eventData.EventName == "ConnectStop") Form1._requestTelemetry.ConnectStop = eventData.TimeStamp;

            if (eventData.EventName == "HandshakeStart") Form1._requestTelemetry.HandshakeStart = eventData.TimeStamp;
            if (eventData.EventName == "HandshakeStop") Form1._requestTelemetry.HandshakeStop = eventData.TimeStamp;

            if (eventData.EventName == "RequestHeadersStart") Form1._requestTelemetry.RequestHeadersStart = eventData.TimeStamp;
            if (eventData.EventName == "RequestHeadersStop") Form1._requestTelemetry.RequestHeadersStop = eventData.TimeStamp;

            if (eventData.EventName == "RequestContentStart") Form1._requestTelemetry.RequestContentStart = eventData.TimeStamp;
            if (eventData.EventName == "RequestContentStop") Form1._requestTelemetry.RequestContentStop = eventData.TimeStamp;

            if (eventData.EventName == "ResponseHeadersStart") Form1._requestTelemetry.ResponseHeadersStart = eventData.TimeStamp;
            if (eventData.EventName == "ResponseHeadersStop") Form1._requestTelemetry.ResponseHeadersStop = eventData.TimeStamp;

            if (eventData.EventName == "ResponseContentStart") Form1._requestTelemetry.ResponseContentStart = eventData.TimeStamp;
            if (eventData.EventName == "ResponseContentStop") Form1._requestTelemetry.ResponseContentStop = eventData.TimeStamp;


        }
    }

}
