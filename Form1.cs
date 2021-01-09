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
        public static SQLiteAsyncConnection db = new SQLiteAsyncConnection("settings.db");
        public static ServerCertificate serverCertificate = new ServerCertificate();
        DataTable dtSessions = new DataTable();
        static Setting _settings = new Setting();
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

            comboBox_settings_profiles.DisplayMember = "ContainerId";
            comboBox_settings_profiles.ValueMember = "Id";

            LoadSettings();
            LoadCertificates();
        }

        private async void LoadSettings()
        {
            comboBox_settings_profiles.Items.Clear();

            AsyncTableQuery<Setting> query;
            List<Setting> result = new List<Setting>();
            try
            {
                await db.CreateTableAsync<Setting>();
                query = db.Table<Setting>();
                result = await query.ToListAsync();
            }
            catch (Exception)
            {  }

            if (result.Count > 0)
            {
                foreach (var item in result)
                {

                    comboBox_settings_profiles.Items.Add(item);

                    if (item.Selected)
                    {
                        comboBox_settings_profiles.SelectedItem = item;

                        await LoadSettingProfile();

                        LoadSessions();

                    }

                }


            }
            else
            {
                MessageBox.Show("Provide connection information to your Cosmos database, please.");
                tabControl2.SelectedTab = tabPage4;
            }

        }

        public async void LoadSessions()
        {
            dtSessions.Clear();

            //Azure Cosmos
            cosmosClient = new CosmosClient(_settings.EndpointUrl, _settings.AuthorizationKey);
            CosmosDatabase database = await cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.DatabaseId);
            CosmosContainer container = await cosmosClient.GetDatabase(_settings.DatabaseId).CreateContainerIfNotExistsAsync(_settings.ContainerId, "/partition");
            var sqlQueryText = "SELECT * FROM c";
            QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);
            await foreach (Session s in container.GetItemQueryIterator<Session>(queryDefinition))
            {
                dtSessions.Rows.Add(new object[] { s.id, s.ResponseStatusCode, s.UriHost, s.UriAbsolutePath, s.Note });
            }

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

            CosmosContainer container = cosmosClient.GetContainer(_settings.DatabaseId, _settings.ContainerId);
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
            CosmosContainer container = cosmosClient.GetContainer(_settings.DatabaseId, _settings.ContainerId);
            Session session = new Session();
            ItemResponse<Session> sessionCosmos = await container.ReadItemAsync<Session>(clickedId, new PartitionKey(session.partition));
            session = sessionCosmos.Value;

            comboBox_http_method.SelectedItem = session.Method;
            comboBox_certificates.SelectedItem = session.ClientCertSubject;
            comboBox_http_version.SelectedItem = session.RequestHttpVersion;

            textBox_request_body.Text = session.RequestBody;
            textBox_request_headers.Text = session.RequestHeaders;
            textBox_request_url.Text = session.UriAbsoluteUri;

            //Response is store zipped as a string
            var ResponseBody_zip = Convert.FromBase64String(session.ResponseBody);
            var ResponseBody_string = Unzip(ResponseBody_zip);

            var pretty = PrettyPrint(ResponseBody_string);
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


        /// <summary>
        /// Insert or update Note to the database
        /// </summary>
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
                    CosmosContainer container = cosmosClient.GetContainer(_settings.DatabaseId, _settings.ContainerId);
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


        private void textBox_filter_TextChanged(object sender, EventArgs e)
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

        private async void comboBox_settings_profiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSettingProfile();
        }

        private async Task LoadSettingProfile()
        {
            AsyncTableQuery<Setting> query;
            Setting result = new Setting();

            try
            {
                await db.CreateTableAsync<Setting>();


                int selectedId = ((Setting)comboBox_settings_profiles.SelectedItem).Id;

                query = db.Table<Setting>().Where(s => s.Id == selectedId);
                result = await query.FirstAsync();

            }
            catch (Exception ex)
            {

            }

            if (result is not null)
            {

                _settings.EndpointUrl = result.EndpointUrl;
                textBox_cosmos_EndpointUrl.Text = _settings.EndpointUrl;

                _settings.AuthorizationKey = result.AuthorizationKey;
                textBox_cosmos_AuthorizationKey.Text = _settings.AuthorizationKey;

                _settings.DatabaseId = result.DatabaseId;
                textBox_cosmos_DatabaseId.Text = _settings.DatabaseId;

                _settings.ContainerId = result.ContainerId;
                textBox_cosmos_ContainerId.Text = _settings.ContainerId;

                //_settings.Selected = true;

                cosmosClient = new CosmosClient(_settings.EndpointUrl, _settings.AuthorizationKey);
            }

        }




        private async void button_settings_save_Click(object sender, EventArgs e)
        {
            _settings.AuthorizationKey = textBox_cosmos_AuthorizationKey.Text;
            _settings.EndpointUrl = textBox_cosmos_EndpointUrl.Text;
            _settings.DatabaseId = textBox_cosmos_DatabaseId.Text;
            _settings.ContainerId = textBox_cosmos_ContainerId.Text;
             
            await db.UpdateAsync(_settings);
   
        }

        private async void button_settings_insert_Click(object sender, EventArgs e)
        {
            _settings.AuthorizationKey = textBox_cosmos_AuthorizationKey.Text;
            _settings.EndpointUrl = textBox_cosmos_EndpointUrl.Text;
            _settings.DatabaseId = textBox_cosmos_DatabaseId.Text;
            _settings.ContainerId = textBox_cosmos_ContainerId.Text;

   
            await db.InsertAsync(_settings);

        }

        private async void button_settings_delete_Click(object sender, EventArgs e)
        {
            await db.DeleteAsync(_settings);
        }

        private async void tabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            //TO-DO: updatovat všechny selected na false 

            var count = await db.ExecuteAsync("update Setting set Selected = false");

            _settings.Selected = true;
            await db.UpdateAsync(_settings);


            if (tabControl2.SelectedTab == tabPage3)
            {
                LoadSessions();
            }
        }

        public static byte[] Zip(string textToZip)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var demoFile = zipArchive.CreateEntry("zipped.txt");

                    using (var entryStream = demoFile.Open())
                    {
                        using (var streamWriter = new StreamWriter(entryStream))
                        {
                            streamWriter.Write(textToZip);
                        }
                    }
                }

                return memoryStream.ToArray();
            }
        }

        public static string Unzip(byte[] zippedBuffer)
        {
            using (var zippedStream = new MemoryStream(zippedBuffer))
            {
                using (var archive = new ZipArchive(zippedStream))
                {
                    var entry = archive.Entries.FirstOrDefault();

                    if (entry != null)
                    {
                        using (var unzippedEntryStream = entry.Open())
                        {
                            using (var ms = new MemoryStream())
                            {
                                unzippedEntryStream.CopyTo(ms);
                                var unzippedArray = ms.ToArray();

                                return Encoding.Default.GetString(unzippedArray);
                            }
                        }
                    }

                    return null;
                }
            }
        }


    }

   
}