using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ApiTester
{
    public partial class Form1 : Form
    {
        private async void LoadSettings()
        {
            comboBox_settings_profiles.Items.Clear();

            List<Setting> result = new List<Setting>();
            try
            {
                await settingsConn.CreateTableAsync<Setting>();
                AsyncTableQuery<Setting> query = settingsConn.Table<Setting>();
                result = await query.ToListAsync();
            }
            catch (Exception ex)
            { MessageBox.Show(ex.Message); }

            if (result.Count > 0)
            {
                foreach (Setting item in result)
                {
                    comboBox_settings_profiles.Items.Add(item);
                    //copyToToolStripMenuItem.DropDownItems.Add(item.ProfileName);

                    ToolStripItem tsItem = new ToolStripMenuItem();
                    tsItem.Text = item.ProfileName;
                    tsItem.Name = item.Id.ToString();

                    copyToToolStripMenuItem.DropDownItems.Add(tsItem);

                    if (item.Selected)
                    {
                        comboBox_settings_profiles.SelectedItem = item;
                        await LoadSettingProfile(item.Id);
                    }
                }
                LoadSessions();
            }
            else
            {
                MessageBox.Show("Provide connection to your database, please.");
                tabControl2.SelectedTab = tabPage4;
            }
        }

        private async Task LoadSettingProfile(int Id)
        {
            try
            {
                var query = settingsConn.Table<Setting>().Where(s => s.Id == Id);
                _settings = await query.FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            if (_settings is not null)
            {
                textBox_cosmos_Endpoint.Text = _settings.Endpoint;
                textBox_profileName.Text = _settings.ProfileName;
                sessionsConn = new SQLiteAsyncConnection(_settings.Endpoint);

                ApplySavedSplitterData();
            }
        }


        private async void TabControl2_SelectedIndexChanged(object sender, EventArgs e)
        {
            await settingsConn.ExecuteAsync("update Setting set Selected = false");

            _settings.Selected = true;
            await settingsConn.UpdateAsync(_settings);

            if (tabControl2.SelectedTab == tabPage3)
            {
                LoadSessions();
            }
        }

        private async void ComboBox_settings_profiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadSettingProfile(((Setting)comboBox_settings_profiles.SelectedItem).Id);
        }

        private async void button_settings_save_Click(object sender, EventArgs e)
        {
            _settings.Endpoint = textBox_cosmos_Endpoint.Text;
            _settings.ProfileName = textBox_profileName.Text;

            await settingsConn.UpdateAsync(_settings);
            LoadSettings();

        }

        private async void button_settings_insert_Click(object sender, EventArgs e)
        {
            _settings.Endpoint = textBox_cosmos_Endpoint.Text;
            _settings.ProfileName = textBox_profileName.Text;

            await settingsConn.InsertAsync(_settings);
            LoadSettings();
        }

        private async void button_settings_delete_Click(object sender, EventArgs e)
        {
            await settingsConn.DeleteAsync(_settings);
            LoadSettings();
        }


        private async void button_settings_export_Click(object sender, EventArgs e)
        {
            //cosmosClient = new CosmosClient(_settings.Endpoint, _settings.AuthorizationKey);

            try
            {
                //string timestamp = DateTime.Now.ToString("yyyymmddHHmmss");
                //SQLiteAsyncConnection settingsConn = new SQLiteAsyncConnection("export-" + timestamp + ".settingsConn");

                //await settingsConn.CreateTableAsync<Session>();

                ////Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.DatabaseId);

                //Container container = await cosmosClient.GetDatabase(_settings.DatabaseId).CreateContainerIfNotExistsAsync(_settings.ContainerId, "/partition");

                //var sqlQueryText = "SELECT * FROM c";
                //QueryDefinition queryDefinition = new QueryDefinition(sqlQueryText);

                //using FeedIterator<Session> feed = container.GetItemQueryIterator<Session>(queryDefinition: queryDefinition);

                //while (feed.HasMoreResults)
                //{
                //    FeedResponse<Session> response = await feed.ReadNextAsync();
                //    foreach (Session s in response)
                //    {
                //        await settingsConn.InsertAsync(s);
                //    }
                //}

                ////await foreach (Session s in container.GetItemQueryIterator<Session>(queryDefinition))
                ////{
                ////    await settingsConn.InsertAsync(s);
                ////}

                //string exportFilepath = new FileInfo("export-" + timestamp + ".settingsConn").DirectoryName;

                //if (MessageBox.Show("Exported in Sqlite format to: " + exportFilepath + "export-" + timestamp + ".settingsConn", "Open export directory?", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk) == DialogResult.Yes)
                //{
                //    System.Diagnostics.Process.Start("explorer.exe", exportFilepath);
                //};
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

                        //Database database = await cosmosClient.CreateDatabaseIfNotExistsAsync(_settings.DatabaseId);
                        //Container container = await cosmosClient.GetDatabase(_settings.DatabaseId).CreateContainerIfNotExistsAsync(_settings.ContainerId, "/partition");

                        //foreach (var item in result)
                        //{
                        //    ItemResponse<Session> createResponse = await container.UpsertItemAsync(item, new PartitionKey(item.partition));
                        //}
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
