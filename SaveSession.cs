﻿using Azure.Cosmos;
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
        public async Task SaveSession(HttpRequestMessage request, HttpResponseMessage response, System.Diagnostics.Stopwatch watch, HttpClientHandler handler)
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


            //Response can be quite large - need to compress it.
            var ResponseBody_string = await response.Content.ReadAsStringAsync();
            var ResponseBody_zip = Zip(ResponseBody_string);
            var ResponseBody_base64 = Convert.ToBase64String(ResponseBody_zip);

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
                ResponseBody = ResponseBody_base64,
                ResponseHeaders = sb.ToString(),
                ResponseTime = (int)watch.ElapsedMilliseconds,

                ResponseLength = Convert.ToInt32(response.Content.Headers.ContentLength.Value),
                ResponseStatusCode = (int)response.StatusCode,
                ResponseHttpVersion = response.Version.ToString(),
                RequestHttpVersion = comboBox_http_version.Text
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

            session.id = Guid.NewGuid().ToString();

            //Sqlite
            //await db.InsertAsync(session);
            //session.id = session.sqliteId.ToString();


            dtSessions.Rows.Add(new object[] { session.id, session.ResponseStatusCode, session.UriHost, session.UriAbsolutePath, session.Note });
            _allSessions.Add(session);
            //int newRowIndex = dataGridView1.Rows.Add(session.Id, session.ResponseStatusCode, session.UriHost, session.UriAbsolutePath, session.Note);
            //dataGridView1.Rows[newRowIndex].Selected = true;
            // await DisplaySession(newRowIndex);
            dataGridView1.DataSource = dtSessions;
            dataGridView1.Rows[dataGridView1.Rows.Count - 1].Selected = true;
            await DisplaySession(dataGridView1.Rows.Count - 1);

            tabControl1.SelectedTab = tabPage2;

            try
            {
                //Azure Cosmos
                CosmosDatabase database = await cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.DatabaseId);
                CosmosContainer container = await cosmosClient.GetDatabase(_settings.DatabaseId).CreateContainerIfNotExistsAsync(_settings.ContainerId, "/partition");
                
                ItemResponse<Session> createResponse = await container.CreateItemAsync(session, new PartitionKey(session.partition));
            }
            catch (Exception)
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
                throw;
            }

            this.Cursor = System.Windows.Forms.Cursors.Default;
        }
    }
}
