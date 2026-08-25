using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ApiTester
{
    /// <summary>
    /// Turns a session into the bytes of its blob and back. See docs/blob-sync.md.
    ///
    /// Layout: a four byte little endian header length, a UTF-8 JSON object of the scalar
    /// columns, then the response body exactly as stored - already Brotli compressed, so it is
    /// passed through rather than re-encoded (base64 inside the JSON would inflate the bulk of
    /// the payload by a third for nothing).
    /// </summary>
    internal static class SyncRow
    {
        //Written by hand through Utf8JsonWriter/JsonDocument rather than JsonSerializer, which
        //needs the reflection-based serializer that Native AOT switches off.
        private const int HeaderPrefix = 4;

        //The default encoder escapes every non-ASCII character as \uXXXX, which costs six bytes
        //where UTF-8 needs two - a Czech note or response body ends up several times its own
        //size. The relaxed encoder is only "unsafe" for text pasted into HTML or script, and this
        //header is read by JsonDocument and nothing else.
        private static readonly JsonWriterOptions HeaderOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        //Local bookkeeping: the key every instance assigns itself, the body that travels after
        //the header, and the sync flags that describe this instance's state, not the session's.
        private static readonly HashSet<string> NotShared = new(StringComparer.Ordinal)
        {
            nameof(Session.Id), nameof(Session.ResponseBody),
            nameof(Session.Dirty), nameof(Session.Uploaded), nameof(Session.Deleted)
        };

        public static byte[] Write(Session session)
        {
            var map = TableMap.For(typeof(Session));

            using var buffer = new MemoryStream();

            using (var writer = new Utf8JsonWriter(buffer, HeaderOptions))
            {
                writer.WriteStartObject();

                foreach (PropertyInfo property in map.Columns)
                {
                    if (NotShared.Contains(property.Name)) continue;

                    WriteProperty(writer, TableMap.ColumnOf(property), property.PropertyType, property.GetValue(session));
                }

                writer.WriteEndObject();
            }

            byte[] header = buffer.ToArray();
            byte[] body = session.ResponseBody ?? Array.Empty<byte>();

            var blob = new byte[HeaderPrefix + header.Length + body.Length];

            BinaryPrimitives.WriteInt32LittleEndian(blob, header.Length);
            header.CopyTo(blob, HeaderPrefix);
            body.CopyTo(blob, HeaderPrefix + header.Length);

            return blob;
        }

        /// <summary>
        /// Rebuilds a session from its blob.
        /// </summary>
        /// <returns>The session, or null when the content is not in this format - a blob written
        /// by something else must be skipped, not half applied.</returns>
        public static Session Read(byte[] blob)
        {
            if (blob is null || blob.Length < HeaderPrefix) return null;

            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(blob);
            if (headerLength < 0 || headerLength > blob.Length - HeaderPrefix) return null;

            var session = new Session();
            var map = TableMap.For(typeof(Session));

            try
            {
                using var document = JsonDocument.Parse(new ReadOnlyMemory<byte>(blob, HeaderPrefix, headerLength));

                if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

                foreach (PropertyInfo property in map.Columns)
                {
                    if (NotShared.Contains(property.Name)) continue;
                    if (!document.RootElement.TryGetProperty(TableMap.ColumnOf(property), out JsonElement value)) continue;

                    ReadProperty(property, session, value);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                return null;
            }

            int bodyStart = HeaderPrefix + headerLength;
            session.ResponseBody = new byte[blob.Length - bodyStart];
            Array.Copy(blob, bodyStart, session.ResponseBody, 0, session.ResponseBody.Length);

            return session;
        }

        private static void WriteProperty(Utf8JsonWriter writer, string name, Type type, object value)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (value is null) { writer.WriteNull(name); return; }

            if (type == typeof(string)) writer.WriteString(name, (string)value);
            else if (type == typeof(bool)) writer.WriteBoolean(name, (bool)value);
            else if (type == typeof(int)) writer.WriteNumber(name, (int)value);
            else if (type == typeof(long)) writer.WriteNumber(name, (long)value);
            else if (type == typeof(double)) writer.WriteNumber(name, (double)value);
            else if (type == typeof(float)) writer.WriteNumber(name, (float)value);
        }

        private static void ReadProperty(PropertyInfo property, Session session, JsonElement value)
        {
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return;

            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (type == typeof(string)) property.SetValue(session, value.GetString());
            else if (type == typeof(bool)) property.SetValue(session, value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.Number && value.GetInt32() != 0));
            else if (type == typeof(int)) property.SetValue(session, value.GetInt32());
            else if (type == typeof(long)) property.SetValue(session, value.GetInt64());
            else if (type == typeof(double)) property.SetValue(session, value.GetDouble());
            else if (type == typeof(float)) property.SetValue(session, value.GetSingle());
        }

        /// <summary>
        /// Timestamps are compared as instants, not as strings: two instances can format the
        /// same moment differently, and a string comparison would order them by their text.
        /// </summary>
        public static DateTime ParseUtc(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                                     DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                     out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
        }

        public static string NowUtc() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    }
}
