using System;
using System.CodeDom.Compiler;
using System.Collections.Specialized;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using System.Xml;
using static SQLite.SQLite3;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace ApiTester
{

    public partial class Form1 : Form
    {
        private async Task BlobUpload(string fullName, int blobVersion)
        {
            FileInfo fileInfo = new(fullName);
            string blobName = fileInfo.Name;

            string sasToken = _settings.BlobSASToken;
            //"sp=racwdl&st=2023-03-24T22:30:53Z&se=2025-09-05T05:30:53Z&spr=https&sv=2021-12-02&sr=c&sig=x2cOPkgalz5cDCFbnWHVvo6UO2cEOv7cOA4C1tmOstk%3D";
            string storageAccount = _settings.BlobStorageAccount;
            //"eaiapimbackup";
            string containerName = _settings.BlobContainer;
                //"apitester";

            DateTime now = DateTime.UtcNow;
            string contentType = "application/octet-stream";

            string blobURI = $"https://{_settings.BlobStorageAccount}.blob.core.windows.net/{_settings.BlobContainer}/{blobName}?{_settings.BlobSASToken}";

            using (var httpRequestMessage = new HttpRequestMessage(HttpMethod.Put, blobURI))
            {
                httpRequestMessage.Headers.Add("x-ms-date", now.ToString("R", CultureInfo.InvariantCulture));
                httpRequestMessage.Headers.Add("x-ms-version", "2021-12-02");
                httpRequestMessage.Headers.Add("x-ms-blob-type", "BlockBlob");

                httpRequestMessage.Headers.Add("x-ms-blob-content-type", contentType);
                httpRequestMessage.Content = new StreamContent(fileInfo.OpenRead());

                httpRequestMessage.Headers.Add("x-ms-meta-version", blobVersion.ToString());

                using (HttpResponseMessage httpResponseMessage = await new HttpClient().SendAsync(httpRequestMessage))
                {
                    if (httpResponseMessage.StatusCode == HttpStatusCode.Created)
                    {
                        //Console.WriteLine("OK");
                        listBox_blob.Items.Add("Uploaded local to blob!");
                    }
                    else
                    {
                        MessageBox.Show("Error uploading to blob!");
                    }

                }
            }
        }

        private async Task BlobDownload(string fileName, string fullName)
        {
            string blobURI = $"https://{_settings.BlobStorageAccount}.blob.core.windows.net/{_settings.BlobContainer}/{fileName}?{_settings.BlobSASToken}";

            var httpClient = new HttpClient();
            try
            {
                using (var stream = await httpClient.GetStreamAsync(blobURI))
                {
                    using (var fileStream = new FileStream(fullName, FileMode.CreateNew))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }
            }
            catch (Exception ex)
            {
                listBox_blob.Items.Add("Error downloading blob: " + ex.Message);
            }
        }

        private int GetBlobVersion(string fileName)
        {
            string blobURI = $"https://{_settings.BlobStorageAccount}.blob.core.windows.net/{_settings.BlobContainer}/{fileName}?{_settings.BlobSASToken}&comp=metadata";

            var httpClient = new HttpClient();
            var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Head, blobURI));

            //Read value of the x-ms-meta-version header from the results
            if (response.Headers.Contains("x-ms-meta-version"))
            {
                string blobVersion = response.Headers.GetValues("x-ms-meta-version").First();
                return Convert.ToInt32(blobVersion);
            }
            else
            {
                listBox_blob.Items.Add("No blob version!");
                MessageBox.Show("No blob version!");
                return Convert.ToInt32(0);
            }



      }


        private void GetBlobList()
        {
            string blobURI = $"https://{_settings.BlobStorageAccount}.blob.core.windows.net/{_settings.BlobContainer}?{_settings.BlobSASToken}&restype=container&comp=list&include=metadata";

            var httpClient = new HttpClient();
            var response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, blobURI));

            if (response.IsSuccessStatusCode)
            {
                var blob = response.Content;

                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(blob.ReadAsStream());
                XmlNodeList blobNodes = xmlDoc.SelectNodes("/EnumerationResults/Blobs/Blob");
                foreach (XmlNode blobNode in blobNodes)
                {
                    var name = blobNode.SelectSingleNode("Name").InnerText;
                    var version = blobNode.SelectSingleNode("Metadata/version")?.InnerText;
                    var lastModified = blobNode.SelectSingleNode("Properties/Last-Modified")?.InnerText;
                    var creationTime = blobNode.SelectSingleNode("Properties/Creation-Time")?.InnerText;

                    listBox_blob.Items.Insert(0, "Blob: "+ name + ", version: " + version + ", updated: " + lastModified);
                }
            }
            else
            {
                listBox_blob.Items.Insert(0,DateTime.Now.ToString("G") + " - No blob there!");
            }
        }


    }


}
