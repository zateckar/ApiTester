using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using System.Xml.Linq;

namespace ApiTester
{

    public partial class Form1 : Form
    {
        private static readonly JsonWriterOptions PrettyJsonOptions = new() { Indented = true };

        /// <summary>
        /// Detects the body format and returns it indented.
        /// </summary>
        /// <returns>A 1x2 array: [0,0] is the detected language ("XML", "JSON" or ""), [0,1] the formatted text.</returns>
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
                //JsonDocument + Utf8JsonWriter rather than JsonSerializer: the serializer needs
                //reflection, which is disabled under Native AOT and warns as IL2026/IL3050.
                using var document = JsonDocument.Parse(input);
                using var buffer = new MemoryStream();

                using (var writer = new Utf8JsonWriter(buffer, PrettyJsonOptions))
                {
                    document.WriteTo(writer);
                }

                string[,] output = { { "JSON", Encoding.UTF8.GetString(buffer.ToArray()) } };
                return output;
            }
            catch (JsonException) { }

            string[,] output1 = { { "", input } };

            return output1;
        }

        public static Version ConvertHttpVersion(string customVersion)
        {
            Version result = new Version();

            if (customVersion.Contains("HTTP 1.0")) result = new Version(1, 0);
            if (customVersion.Contains("HTTP 1.1")) result = new Version(1, 1);
            if (customVersion.Contains("HTTP 2.0")) result = new Version(2, 0);
            if (customVersion.Contains("HTTP 3.0")) result = new Version(3, 0);

            return result;
        }

        //Response bodies have been stored three ways over the life of this app. New rows are
        //always Brotli; the others are recognised on read so old databases stay readable.
        private static readonly byte[] ZipMagic = { 0x50, 0x4B, 0x03, 0x04 };          // "PK\x03\x04"
        private static readonly byte[] ZstdMagic = { 0x28, 0xB5, 0x2F, 0xFD };         // zstd frame
        private const string Base64ZipPrefix = "UEsDB";                                 // base64 of "PK\x03\x04"

        /// <summary>
        /// Compresses a response body for storage. Brotli, from System.IO.Compression.
        /// </summary>
        public static byte[] Zip(string textToZip)
        {
            if (string.IsNullOrEmpty(textToZip)) return Array.Empty<byte>();

            using var output = new MemoryStream();

            using (var brotli = new BrotliStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                byte[] input = Encoding.UTF8.GetBytes(textToZip);
                brotli.Write(input, 0, input.Length);
            }

            return output.ToArray();
        }

        /// <summary>
        /// Decompresses a stored response body, detecting which of the historical formats
        /// it was written in.
        /// </summary>
        public static string Unzip(byte[] zippedBuffer)
        {
            if (zippedBuffer is null || zippedBuffer.Length == 0) return string.Empty;

            //Oldest format: a ZIP archive, base64 encoded, stored in a TEXT column.
            if (LooksLikeBase64Zip(zippedBuffer))
            {
                try
                {
                    return UnzipArchive(Convert.FromBase64String(Encoding.ASCII.GetString(zippedBuffer)));
                }
                catch (Exception ex) when (ex is FormatException or InvalidDataException)
                {
                    return "[Could not read the stored response body: " + ex.Message + "]";
                }
            }

            //Same archive, stored raw rather than base64 encoded.
            if (StartsWith(zippedBuffer, ZipMagic))
            {
                try
                {
                    return UnzipArchive(zippedBuffer);
                }
                catch (InvalidDataException ex)
                {
                    return "[Could not read the stored response body: " + ex.Message + "]";
                }
            }

            //Interim format. Zstd was dropped in favour of the in-box Brotli codec; a body in
            //this format needs the one-off converter rather than silently showing as garbage.
            if (StartsWith(zippedBuffer, ZstdMagic))
            {
                return "[This response body was stored with the old zstd codec. Run the migration tool to convert this database.]";
            }

            try
            {
                using var input = new MemoryStream(zippedBuffer);
                using var brotli = new BrotliStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();

                brotli.CopyTo(output);
                return Encoding.UTF8.GetString(output.ToArray());
            }
            catch (InvalidDataException)
            {
                //Not any format we know - most likely an uncompressed body from a hand-edited row.
                return Encoding.UTF8.GetString(zippedBuffer);
            }
        }

        private static string UnzipArchive(byte[] archiveBytes)
        {
            using var stream = new MemoryStream(archiveBytes);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            if (archive.Entries.Count == 0) return string.Empty;

            using var entryStream = archive.Entries[0].Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8);

            return reader.ReadToEnd();
        }

        private static bool LooksLikeBase64Zip(byte[] buffer)
        {
            if (buffer.Length < Base64ZipPrefix.Length) return false;

            for (int i = 0; i < Base64ZipPrefix.Length; i++)
            {
                if (buffer[i] != (byte)Base64ZipPrefix[i]) return false;
            }

            return true;
        }

        private static bool StartsWith(byte[] buffer, byte[] magic)
        {
            if (buffer.Length < magic.Length) return false;

            for (int i = 0; i < magic.Length; i++)
            {
                if (buffer[i] != magic[i]) return false;
            }

            return true;
        }
    }


}
