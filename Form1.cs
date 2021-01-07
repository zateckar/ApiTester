using Azure.Cosmos;
using SQLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
        public static SQLiteAsyncConnection db = new SQLiteAsyncConnection( "apitester.db");
        public static ServerCertificate serverCertificate = new ServerCertificate();
        DataTable dtSessions = new DataTable();
        static Settings _settings = new Settings();
        CosmosClient cosmosClient;

        public Form1()
        {
            InitializeComponent();

            this.Text = "API Tester,   version: " + this.ProductVersion;

            typeof(DataGridView).InvokeMember("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty, null, dataGridView1, new object[] { true });

            dtSessions.Columns.Add("Id", typeof(string));
            dtSessions.Columns.Add("ResponseStatusCode", typeof(int));
            dtSessions.Columns.Add("UriHost", typeof(string));
            dtSessions.Columns.Add("UriAbsolutePath", typeof(string));
            dtSessions.Columns.Add("Note", typeof(string));

            LoadSettings();
            LoadCertificates();
        }

        private async void LoadSettings()
        {
            AsyncTableQuery<Settings> query;
            Settings result = new Settings();
            try
            {
                await db.CreateTableAsync<Settings>();
                query = db.Table<Settings>();
                result = await query.FirstOrDefaultAsync();
            }
            catch (Exception)
            { }

            if (result is not null)
            {
                _settings.CosmosEndpointUrl = result.CosmosEndpointUrl;
                textBox_cosmos_EndpointUrl.Text = _settings.CosmosEndpointUrl;

                _settings.CosmosAuthorizationKey = result.CosmosAuthorizationKey;
                textBox_cosmos_AuthorizationKey.Text = _settings.CosmosAuthorizationKey;

                _settings.CosmosDatabaseId = result.CosmosDatabaseId;
                textBox_cosmos_DatabaseId.Text = _settings.CosmosDatabaseId;

                _settings.CosmosContainerId = result.CosmosContainerId;
                textBox_cosmos_ContainerId.Text = _settings.CosmosContainerId;

                cosmosClient = new CosmosClient(_settings.CosmosEndpointUrl, _settings.CosmosAuthorizationKey);
       
                LoadSessions();
            }
            else
            {
                MessageBox.Show("Provide connection information to your Cosmos database, please.");
                tabControl2.SelectedTab = tabPage4;
            }

        }

        public async void LoadSessions()
        {
            dataGridView1.Rows.Clear();

            //Sqlite
            //await db.CreateTableAsync<Session>();
            //var query = db.Table<Session>();
            //var result = await query.ToListAsync();

            //Azure Cosmos
            CosmosDatabase database = await cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.CosmosDatabaseId);
            CosmosContainer container = await cosmosClient.GetDatabase(_settings.CosmosDatabaseId).CreateContainerIfNotExistsAsync(_settings.CosmosContainerId, "/partition");
            var sqlQueryText = "SELECT * FROM c";
            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            // List<Session> sessions = new List<Session>();
            await foreach (Session s in container.GetItemQueryIterator<Session>(queryDefinition))
            {
                //sessions.Add(session);
                dtSessions.Rows.Add(new object[] { s.id, s.ResponseStatusCode, s.UriHost, s.UriAbsolutePath, s.Note });
            }

            //foreach (var s in result)
            //    dataGridView1.Rows.Add(s.Id, s.ResponseStatusCode, s.UriHost, s.UriAbsolutePath, s.Note);

            dataGridView1.DataSource = dtSessions;
            dataGridView1.Columns[0].Visible = false;

            dataGridView1.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            dataGridView1.Columns[1].Width = 30;
            dataGridView1.Columns[1].MinimumWidth = 30;
            dataGridView1.Columns[1].ReadOnly = true;

            dataGridView1.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
            dataGridView1.Columns[2].ReadOnly = true;

            dataGridView1.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
            dataGridView1.Columns[4].ReadOnly = true;

            dataGridView1.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

        }

        private async void button_request_send_Click(object sender, EventArgs e)
        {
            //Works only when MaxDegreeOfParallelism = 1 - need to separate actual call from UI
            //var options = new ParallelOptions()
            //{
            //    MaxDegreeOfParallelism = 1
            //};

            //Parallel.For(0, Convert.ToInt32(numericUpDown_request.Text), options, (i) =>
            //{
            //    SendRequest();
            //});


            for (int y = 0; y < Convert.ToInt32(numericUpDown_request.Text); y++)
            {
                await SendRequest(
                    textBox_request_body.Text, 
                    textBox_request_headers.Text, 
                    comboBox_http_method.Text,
                    textBox_request_url.Text,
                    comboBox_http_version.Text,
                    comboBox_certificates.Text
                    );
            }

        }

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

        public async Task SaveSession(HttpRequestMessage request, HttpResponseMessage response, System.Diagnostics.Stopwatch watch, HttpClientHandler handler)
        {
            var sb = new StringBuilder();
            foreach (var header in response.Headers)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            foreach (var header in response.TrailingHeaders)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            foreach (var header in response.Content.Headers)
                sb.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            var sr = new StringBuilder();
            //foreach (var header in request.Headers)
            //{
            //    sr.AppendLine(header.Key + ":" + string.Join(", ", header.Value));
            //}

            foreach (var header in request.Content.Headers)
                sr.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");

            //foreach (var header in client.DefaultRequestHeaders)
            //{
            //    sr.AppendLine(header.Key == "Set-Cookie" ? $"{header.Key}: {string.Join("\r\nSet-Cookie: ", header.Value)}" : $"{header.Key}: {string.Join(", ", header.Value)}");
            //}

            var session = new Session()
            {
                DateTime = DateTime.Now.ToString("s"),
                RequestHeaders = sr.ToString(),
                RequestBody = await request.Content.ReadAsStringAsync(),
                Method = request.Method.Method,
                UriAbsoluteUri = request.RequestUri.AbsoluteUri,
                UriAbsolutePath = request.RequestUri.AbsolutePath,
                UriQuery = request.RequestUri.Query,
                UriHost = request.RequestUri.Host,
                ResponseBody = await response.Content.ReadAsStringAsync(),
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


            //Sqlite
            //await db.InsertAsync(session);
            //session.id = session.sqliteId.ToString();


            try
            {
                //Azure Cosmos
                CosmosDatabase database = await cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.CosmosDatabaseId);
                CosmosContainer container = await cosmosClient.GetDatabase(_settings.CosmosDatabaseId).CreateContainerIfNotExistsAsync(_settings.CosmosContainerId, "/partition");
                session.id = Guid.NewGuid().ToString();
                ItemResponse<Session> createResponse = await container.CreateItemAsync(session, new PartitionKey(session.partition));
            }
            catch (Exception)
            {

                throw;
            }


            dtSessions.Rows.Add(new object[] { session.id, session.ResponseStatusCode, session.UriHost, session.UriAbsolutePath, session.Note });
            //int newRowIndex = dataGridView1.Rows.Add(session.Id, session.ResponseStatusCode, session.UriHost, session.UriAbsolutePath, session.Note);
            //dataGridView1.Rows[newRowIndex].Selected = true;
            // await DisplaySession(newRowIndex);
            dataGridView1.DataSource = dtSessions;
            dataGridView1.Rows[dataGridView1.Rows.Count - 1].Selected = true;
            await DisplaySession(dataGridView1.Rows.Count - 1);

            tabControl1.SelectedTab = tabPage2;
        }

       

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        public async void LoadCertificates()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

            store.Open(OpenFlags.ReadOnly);

            foreach (X509Certificate2 certificate in store.Certificates)
            {
                //var kvs = certificate.Subject.Split(',').Select(x => new KeyValuePair<string, string>(x.Split('=')[0], x.Split('=')[1])).ToList();
                //if(kvs[0].Value.Contains("*") == false) comboBox_certificates.Items.Add(kvs[0].Value);
                if (certificate.SubjectName.Name.Contains("*") == false) comboBox_certificates.Items.Add(certificate.SubjectName.Name);
            }
        }

        private async void dataGridView1_UserDeletingRow(object sender, DataGridViewRowCancelEventArgs e)
        {
            //var db = new SQLiteAsyncConnection("sessions.db");

            var clickedId = dataGridView1.Rows[e.Row.Index].Cells[0].Value.ToString();

           // var query = db.Table<Session>().Where(s => s.sqliteId.Equals(clickedId));
           // var result = await query.DeleteAsync();

            CosmosContainer container = cosmosClient.GetContainer(_settings.CosmosDatabaseId, _settings.CosmosContainerId);
            Session session = new Session();
            ItemResponse<Session> sessionCosmos = await container.DeleteItemAsync<Session>(clickedId, new PartitionKey(session.partition));
        }

        private async void dataGridView1_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            await DisplaySession(e.RowIndex);
        }

        private async Task DisplaySession(int RowIndex)
        {
            var clickedId = dataGridView1.Rows[RowIndex].Cells["Id"].Value.ToString();

            //Sqlite
            //var db = new SQLiteAsyncConnection("sessions.db");
            //var query = db.Table<Session>().Where(s => s.sqliteId.Equals(clickedId));
            //var session_pom = await query.FirstAsync();

            //Azure Cosmos
            CosmosContainer container = cosmosClient.GetContainer(_settings.CosmosDatabaseId, _settings.CosmosContainerId);
            Session session = new Session();
            ItemResponse<Session> sessionCosmos = await container.ReadItemAsync<Session>(clickedId,new PartitionKey(session.partition));
            session = sessionCosmos.Value;

            comboBox_http_method.SelectedItem = session.Method;
            comboBox_certificates.SelectedItem = session.ClientCertSubject;
            comboBox_http_version.SelectedItem = session.RequestHttpVersion;

            textBox_request_body.Text = session.RequestBody;
            textBox_request_headers.Text = session.RequestHeaders;
            textBox_request_url.Text = session.UriAbsoluteUri;

            var pretty = PrettyPrint(session.ResponseBody);
            if (pretty[0, 0].Equals("JSON")) textBox_response_body.Language = FastColoredTextBoxNS.Language.JSON;
            if (pretty[0, 0].Equals("XML")) textBox_response_body.Language = FastColoredTextBoxNS.Language.XML;
            textBox_response_body.Text = pretty[0, 1];

            textBox_response_headers.Text = session.ResponseHeaders;

            //Additional info
            textBox_response_stats.Text = "HTTP version: " + session.ResponseHttpVersion + Environment.NewLine;
            textBox_response_stats.Text += "Date: " + session.DateTime + Environment.NewLine;
            textBox_response_stats.Text += "Response time: " + session.ResponseTime.ToString() + "ms" + Environment.NewLine;
            textBox_response_stats.Text += "Server certificate: " + Environment.NewLine;
            textBox_response_stats.Text += " - Subject: " + session.ServerCertSubject + Environment.NewLine;
            textBox_response_stats.Text += " - Issuer: " + session.ServerCertIssuer + Environment.NewLine;
            textBox_response_stats.Text += " - ValidFrom: " + session.ServerCertValidFrom + Environment.NewLine;
            textBox_response_stats.Text += " - ValidTo: " + session.ServerCertValidTo + Environment.NewLine;
            textBox_response_stats.Text += " - IsValid: " + session.ServerCertIsValid.ToString() + Environment.NewLine;
        }

        //public string Prettify(string jsonString)
        //{
        //    using var stream = new MemoryStream();
        //    var document = JsonDocument.Parse(jsonString);
        //    var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
        //    document.WriteTo(writer);
        //    writer.Flush();
        //    return Encoding.UTF8.GetString(stream.ToArray());
        //}

        //public string PrettyJson(string unPrettyJson)
        //{
        //    var options = new JsonSerializerOptions()
        //    {
        //        WriteIndented = true
        //    };

        //    var jsonElement = JsonSerializer.Deserialize<JsonElement>(unPrettyJson);

        //    return JsonSerializer.Serialize(jsonElement, options);
        //}

        //public static string FormatJson(string json, string indent = "  ")
        //{
        //    var indentation = 0;
        //    var quoteCount = 0;
        //    var escapeCount = 0;

        //    var result =
        //        from ch in json ?? string.Empty
        //        let escaped = (ch == '\\' ? escapeCount++ : escapeCount > 0 ? escapeCount-- : escapeCount) > 0
        //        let quotes = ch == '"' && !escaped ? quoteCount++ : quoteCount
        //        let unquoted = quotes % 2 == 0
        //        let colon = ch == ':' && unquoted ? ": " : null
        //        let nospace = char.IsWhiteSpace(ch) && unquoted ? string.Empty : null
        //        let lineBreak = ch == ',' && unquoted ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(indent, indentation)) : null
        //        let openChar = (ch == '{' || ch == '[') && unquoted ? ch + Environment.NewLine + string.Concat(Enumerable.Repeat(indent, ++indentation)) : ch.ToString()
        //        let closeChar = (ch == '}' || ch == ']') && unquoted ? Environment.NewLine + string.Concat(Enumerable.Repeat(indent, --indentation)) + ch : ch.ToString()
        //        select colon ?? nospace ?? lineBreak ?? (
        //            openChar.Length > 1 ? openChar : closeChar
        //        );

        //    return string.Concat(result);
        //}

        public static string[,] PrettyPrint(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                string[,] output = { { "", input } };

                return output;
            }

            try
            {
                string[,] output = { { "XML", XDocument.Parse(input).ToString() } };
                return output;
            }
            catch (Exception) { }

            try
            {
                var options = new JsonSerializerOptions()
                {
                    WriteIndented = true
                };
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(input);

                string[,] output = { { "JSON", JsonSerializer.Serialize(jsonElement, options) } };
                return output;
            }
            catch (Exception) { }

            string[,] output1 = { { "", input } };

            return output1;
        }

        public string GetTraceparent()
        {
            string version = "00";
            string traceid = Guid.NewGuid().ToString("N");
            string parentid = Guid.NewGuid().ToString("N").Remove(16, 16);
            string traceflags = "01";

            return (version + "-" + traceid + "-" + parentid + "-" + traceflags);
        }

        public Version ConvertHttpVersion(string customVersion)
        {
            Version result = new Version();

            if (customVersion.Contains("HTTP 1.0")) result = new Version(1, 0);
            if (customVersion.Contains("HTTP 1.1")) result = new Version(1, 1);
            if (customVersion.Contains("HTTP 2.0")) result = new Version(2, 0);
            if (customVersion.Contains("HTTP 3.0")) result = new Version(3, 0);

            return result;
        }

        private static X509Certificate2 FindCert(X509Store store, string subject)
        {
            foreach (var cert in store.Certificates)
                if (cert.SubjectName.Name.Equals(subject,
                    StringComparison.CurrentCultureIgnoreCase))
                    return cert;
            return null;
        }

        private void dataGridView1_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (this.dataGridView1.Columns["ResponseStatusCode"].Index == e.ColumnIndex && e.RowIndex >= 0)
            {
                if (((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value >= 100) && ((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value < 300))
                {
                    e.CellStyle.BackColor = System.Drawing.Color.Green;
                    e.CellStyle.ForeColor = System.Drawing.Color.White;
                }

                if (((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value >= 300) && ((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value < 400))
                {
                    e.CellStyle.BackColor = System.Drawing.Color.Yellow;
                }

                if (((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value >= 400) && ((int)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value < 600))
                {
                    e.CellStyle.BackColor = System.Drawing.Color.Red;
                    e.CellStyle.ForeColor = System.Drawing.Color.White;
                }
            }
        }

        private async void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                string clickedId = dataGridView1.Rows[e.RowIndex].Cells["Id"].Value.ToString();

                //Sqlite
                //var query = db.Table<Session>().Where(s => s.sqliteId.Equals(clickedId));
                //var session_pom = await query.FirstAsync();

                //Azure Cosmos
                Session session = new Session();
                try
                {
                    CosmosContainer container = cosmosClient.GetContainer(_settings.CosmosDatabaseId, _settings.CosmosContainerId);
                    ItemResponse<Session> sessionCosmos = await container.ReadItemAsync<Session>(clickedId, new PartitionKey(session.partition));
                    session = sessionCosmos.Value;

                    session.Note = (string)dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;

                    // await db.UpdateAsync(session);

                    sessionCosmos = await container.ReplaceItemAsync<Session>(session, session.id, new PartitionKey(session.partition));
                }
                catch (Exception)
                {

                    throw;
                }

                
            }
        }

        private  void textBox_filter_TextChanged(object sender, EventArgs e)
        {
            if (textBox_filter.Text.Length > 0)
            {
               // dtSessions.DefaultView.RowFilter = string.Format("[{0}] LIKE '%{1}%'", "Note", textBox_filter.Text);
                dtSessions.DefaultView.RowFilter = "Note LIKE '%" + textBox_filter.Text.Trim() + "%' OR UriHost LIKE '%" + textBox_filter.Text.Trim() + "%' OR UriAbsolutePath LIKE '%" + textBox_filter.Text.Trim() + "%'";
            }
            else
            {
                dtSessions.DefaultView.RowFilter = String.Empty;
            }
            
        }

        private async void button_settings_save_Click(object sender, EventArgs e)
        {
            _settings.CosmosAuthorizationKey = textBox_cosmos_AuthorizationKey.Text;
            _settings.CosmosEndpointUrl = textBox_cosmos_EndpointUrl.Text;
            _settings.CosmosDatabaseId = textBox_cosmos_DatabaseId.Text;
            _settings.CosmosContainerId = textBox_cosmos_ContainerId.Text;

            await db.InsertOrReplaceAsync(_settings);

            tabControl2.SelectedTab = tabPage3;

            LoadSessions();
        }
    }

    public class ServerCertificate
    {
        public string RequestUri { get; set; }
        public string ValidFrom { get; set; }
        public string ValidTo { get; set; }
        public string Subject { get; set; }
        public string Issuer { get; set; }
        public bool IsValid { get; set; }
    }

    public class Settings
    {
        public string CosmosEndpointUrl { get; set; }
        public string CosmosAuthorizationKey { get; set; }
        public string CosmosDatabaseId { get; set; }
        public string CosmosContainerId { get; set; }

    }

    public class Session
    {
        //[PrimaryKey, AutoIncrement]
        //public int sqliteId { get; set; }

        //id for Azure Cosmos must be a string
        public string id { get; set; }

        //for Azure Cosmos
        public string partition { get; set; }
        
        public string DateTime { get; set; }
        public string UriAbsoluteUri { get; set; }
        public string UriAbsolutePath { get; set; }
        public string UriQuery { get; set; }
        public string UriHost { get; set; }
        public string Method { get; set; }
        public string RequestHeaders { get; set; }
        public string RequestBody { get; set; }
        public string ResponseHeaders { get; set; }
        public string ResponseBody { get; set; }
        public int ResponseStatusCode { get; set; }
        public string ResponseHttpVersion { get; set; }
        public string RequestHttpVersion { get; set; }
        public int ResponseLength { get; set; }
        public int ResponseTime { get; set; }
        public string Note { get; set; }
        public string Application { get; set; }

        public bool ServerCertIsValid { get; set; }
        public string ServerCertValidFrom { get; set; }
        public string ServerCertValidTo { get; set; }
        public string ServerCertSubject { get; set; }
        public string ServerCertIssuer { get; set; }

        public string ClientCertSubject { get; set; }
    }
}