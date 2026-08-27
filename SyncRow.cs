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

        //Notes have no trailing body - the text travels inside the header. Everything but the
        //local key and the sync flags crosses the wire.
        private static readonly HashSet<string> NotSharedNote = new(StringComparer.Ordinal)
        {
            nameof(Note.Id), nameof(Note.Dirty), nameof(Note.Uploaded), nameof(Note.Deleted)
        };

        public static byte[] Write(Session session)
        {
            byte[] header = WriteHeader(session, NotShared);
            byte[] body = session.ResponseBody ?? Array.Empty<byte>();

            var blob = new byte[HeaderPrefix + header.Length + body.Length];

            BinaryPrimitives.WriteInt32LittleEndian(blob, header.Length);
            header.CopyTo(blob, HeaderPrefix);
            body.CopyTo(blob, HeaderPrefix + header.Length);

            return blob;
        }

        public static byte[] WriteNote(Note note)
        {
            byte[] header = WriteHeader(note, NotSharedNote);

            var blob = new byte[HeaderPrefix + header.Length];
            BinaryPrimitives.WriteInt32LittleEndian(blob, header.Length);
            header.CopyTo(blob, HeaderPrefix);

            return blob;
        }

        private static byte[] WriteHeader<
            [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(
            T item, HashSet<string> notShared)
        {
            var map = TableMap.For(typeof(T));

            using var buffer = new MemoryStream();

            using (var writer = new Utf8JsonWriter(buffer, HeaderOptions))
            {
                writer.WriteStartObject();

                foreach (PropertyInfo property in map.Columns)
                {
                    if (notShared.Contains(property.Name)) continue;

                    WriteProperty(writer, TableMap.ColumnOf(property), property.PropertyType, property.GetValue(item));
                }

                writer.WriteEndObject();
            }

            return buffer.ToArray();
        }

        /// <summary>
        /// Rebuilds a session from its blob.
        /// </summary>
        /// <returns>The session, or null when the content is not in this format - a blob written
        /// by something else must be skipped, not half applied.</returns>
        public static Session Read(byte[] blob)
        {
            int headerLength = HeaderLengthOf(blob, out bool valid);
            if (!valid) return null;

            var session = ReadHeader<Session>(blob, headerLength, NotShared);
            if (session is null) return null;

            int bodyStart = HeaderPrefix + headerLength;
            session.ResponseBody = new byte[blob.Length - bodyStart];
            Array.Copy(blob, bodyStart, session.ResponseBody, 0, session.ResponseBody.Length);

            return session;
        }

        /// <summary>
        /// Rebuilds a note from its blob. The whole note lives in the header - there is no
        /// trailing body.
        /// </summary>
        /// <returns>The note, or null when the content is not in this format.</returns>
        public static Note ReadNote(byte[] blob)
        {
            int headerLength = HeaderLengthOf(blob, out bool valid);
            if (!valid) return null;

            return ReadHeader<Note>(blob, headerLength, NotSharedNote);
        }

        private static int HeaderLengthOf(byte[] blob, out bool valid)
        {
            valid = false;

            if (blob is null || blob.Length < HeaderPrefix) return 0;

            int headerLength = BinaryPrimitives.ReadInt32LittleEndian(blob);
            if (headerLength < 0 || headerLength > blob.Length - HeaderPrefix) return 0;

            valid = true;
            return headerLength;
        }

        private static T ReadHeader<
            [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(TableMap.MappedMembers)] T>(
            byte[] blob, int headerLength, HashSet<string> notShared) where T : new()
        {
            var item = new T();
            var map = TableMap.For(typeof(T));

            try
            {
                using var document = JsonDocument.Parse(new ReadOnlyMemory<byte>(blob, HeaderPrefix, headerLength));

                if (document.RootElement.ValueKind != JsonValueKind.Object) return default;

                foreach (PropertyInfo property in map.Columns)
                {
                    if (notShared.Contains(property.Name)) continue;
                    if (!document.RootElement.TryGetProperty(TableMap.ColumnOf(property), out JsonElement value)) continue;

                    ReadProperty(property, item, value);
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                return default;
            }

            return item;
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

        private static void ReadProperty(PropertyInfo property, object target, JsonElement value)
        {
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return;

            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (type == typeof(string)) property.SetValue(target, value.GetString());
            else if (type == typeof(bool)) property.SetValue(target, value.ValueKind == JsonValueKind.True || (value.ValueKind == JsonValueKind.Number && value.GetInt32() != 0));
            else if (type == typeof(int)) property.SetValue(target, value.GetInt32());
            else if (type == typeof(long)) property.SetValue(target, value.GetInt64());
            else if (type == typeof(double)) property.SetValue(target, value.GetDouble());
            else if (type == typeof(float)) property.SetValue(target, value.GetSingle());
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
