using SQLite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
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
