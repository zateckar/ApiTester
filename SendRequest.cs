using Azure.Cosmos;
using SQLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        public async Task SendRequest(string request_body, string request_headers, string http_method, string request_url, string http_version, string certificate)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = ServerCertificateCustomValidation;

            HttpClient client = new HttpClient(handler);

            //var databasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MyData.db");
            HttpRequestMessage request = new HttpRequestMessage();

            StringContent content = new StringContent(request_body);
            content.Headers.Remove("Content-Type");

            using (StringReader reader = new StringReader(request_headers))
            {
                string s;
                while ((s = reader.ReadLine()) != null)
                {
                    int pom1 = s.IndexOf(":");
                    string key = s.Substring(0, pom1).Trim();
                    string value = s.Substring(pom1 + 1, s.Length - (pom1 + 1)).Trim();

                    //request.Headers.TryAddWithoutValidation(key, value);
                    content.Headers.TryAddWithoutValidation(key, value);
                }
            }

            if (content.Headers.Contains("traceparent") == false) content.Headers.Add("traceparent", GetTraceparent());

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
                await response.Content.LoadIntoBufferAsync();
                watch.Stop();
            }
            catch (HttpRequestException ex)
            {
                response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable;
                response.Content = new StringContent(ex.InnerException.Message);
            }

            //Response processing//
            await SaveSession(request, response, watch, handler);

            request.Dispose();
            response.Dispose();
            handler.Dispose();
            client.Dispose();
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
}
