using Azure.Cosmos;
using SQLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        public static SQLiteAsyncConnection db = new SQLiteAsyncConnection("settings.db");
        public static ServerCertificate serverCertificate = new ServerCertificate();
        DataTable dtSessions = new DataTable();
        static Setting _settings = new Setting();
        List<Session> _newSessions = new List<Session>();
        List<Session> _allSessions = new List<Session>();
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

                    }
                }

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
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;

            dtSessions.Clear();

            //Azure Cosmos
            cosmosClient = new CosmosClient(_settings.EndpointUrl, _settings.AuthorizationKey);

            try
            {
                CosmosDatabase database = await cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.DatabaseId);
                CosmosContainer container = await cosmosClient.GetDatabase(_settings.DatabaseId).CreateContainerIfNotExistsAsync(_settings.ContainerId, "/partition");

                var sqlQueryText = "SELECT * FROM c";
                QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

                await foreach (Session s in container.GetItemQueryIterator<Session>(queryDefinition))
                {
                    dtSessions.Rows.Add(new object[] { s.id, s.ResponseStatusCode, s.UriHost, s.UriAbsolutePath, s.Note });
                    _allSessions.Add(s);

                }
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
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
            dataGridView1.Columns[3].ReadOnly = true;

            dataGridView1.Columns[4].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            this.Cursor = System.Windows.Forms.Cursors.Default;

        }

        private async void button_request_send_Click(object sender, EventArgs e)
        {
            SendRequestConsolidate();
        }

        private void dataGridView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.R) SendRequestConsolidate();
        }

        public async void SendRequestConsolidate()
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

            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;

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

            this.Cursor = System.Windows.Forms.Cursors.Default;
        }

        public async Task LoadCertificates()
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
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
            var clickedId = dataGridView1.Rows[e.Row.Index].Cells[0].Value.ToString();
            
            try
            {
                CosmosContainer container = cosmosClient.GetContainer(_settings.DatabaseId, _settings.ContainerId);
                Session session = new Session();
                ItemResponse<Session> sessionCosmos = await container.DeleteItemAsync<Session>(clickedId, new PartitionKey(session.partition));
                _allSessions.Remove(_allSessions.Where(c => c.id.Equals(clickedId)).FirstOrDefault());
            }
            catch (Exception)
            {
                this.Cursor = System.Windows.Forms.Cursors.Default;
            }
            
            
            Thread.Sleep(100);
            this.Cursor = System.Windows.Forms.Cursors.Default;
        }

        private async void dataGridView1_RowEnter(object sender, DataGridViewCellEventArgs e)
        {
            await DisplaySession(e.RowIndex);
        }

        private async Task DisplaySession(int RowIndex)
        {
            this.Cursor = System.Windows.Forms.Cursors.WaitCursor;

            var clickedId = dataGridView1.Rows[RowIndex].Cells["Id"].Value.ToString();

            //Azure Cosmos
            //CosmosContainer container = cosmosClient.GetContainer(_settings.DatabaseId, _settings.ContainerId);
            //Session session = new Session();
            //ItemResponse<Session> sessionCosmos = await container.ReadItemAsync<Session>(clickedId, new PartitionKey(session.partition));
            //session = sessionCosmos.Value;

            Session session = new Session();

            session = _allSessions.Where(c => c.id.Equals(clickedId)).FirstOrDefault();

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

            this.Cursor = System.Windows.Forms.Cursors.Default;
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

                   // throw;
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

                _settings.Id = result.Id;

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

            LoadSettings();

        }

        private async void button_settings_insert_Click(object sender, EventArgs e)
        {
            _settings.AuthorizationKey = textBox_cosmos_AuthorizationKey.Text;
            _settings.EndpointUrl = textBox_cosmos_EndpointUrl.Text;
            _settings.DatabaseId = textBox_cosmos_DatabaseId.Text;
            _settings.ContainerId = textBox_cosmos_ContainerId.Text;

   
            await db.InsertAsync(_settings);
            LoadSettings();
        }

        private async void button_settings_delete_Click(object sender, EventArgs e)
        {
            await db.DeleteAsync(_settings);
            LoadSettings();
        }

        private async void tabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            var count = await db.ExecuteAsync("update Setting set Selected = false");

            _settings.Selected = true;
            await db.UpdateAsync(_settings);


            if (tabControl2.SelectedTab == tabPage3)
            {
                LoadSessions();
            }
        }

        private async void button_settings_export_Click(object sender, EventArgs e)
        {
            cosmosClient = new CosmosClient(_settings.EndpointUrl, _settings.AuthorizationKey);

            try
            {
                string timestamp = DateTime.Now.ToString("yyyymmddHHmmss");
                SQLiteAsyncConnection db = new SQLiteAsyncConnection("export-"+timestamp+".db");

                await db.CreateTableAsync<Session>();

                CosmosDatabase database = await cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.DatabaseId);

                CosmosContainer container = await cosmosClient.GetDatabase(_settings.DatabaseId).CreateContainerIfNotExistsAsync(_settings.ContainerId, "/partition");

                var sqlQueryText = "SELECT * FROM c";
                QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

                await foreach (Session s in container.GetItemQueryIterator<Session>(queryDefinition))
                {
                    await db.InsertAsync(s);
                }

                string exportFilepath = new FileInfo("export-" + timestamp + ".db").DirectoryName;

                if (MessageBox.Show("Exported in Sqlite format to: " + exportFilepath + "export-" + timestamp + ".db", "Open export directory?", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start("explorer.exe", exportFilepath);
                };
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.Message);
            }
        }

        private async void button_settings_import_Click(object sender, EventArgs e)
        {
           var openFileDialog1 = new OpenFileDialog();
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var filePath = openFileDialog1.FileName;
                    using (Stream str = openFileDialog1.OpenFile())
                    {
                        SQLiteAsyncConnection db = new SQLiteAsyncConnection(filePath);
                        var query = db.Table<Session>();
                        var result = await query.ToListAsync();

                        CosmosDatabase database = await cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.DatabaseId);
                        CosmosContainer container = await cosmosClient.GetDatabase(_settings.DatabaseId).CreateContainerIfNotExistsAsync(_settings.ContainerId, "/partition");

                        foreach (var item in result)
                        {
                            ItemResponse<Session> createResponse = await container.UpsertItemAsync(item, new PartitionKey(item.partition));
                        }
                    }
                }
                catch (SecurityException ex)
                {
                    MessageBox.Show($"Security error.\n\nError message: {ex.Message}\n\n" +
                    $"Details:\n\n{ex.StackTrace}");
                }
            }
        }


    }

   
}