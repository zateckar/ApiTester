using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace ApiTester
{
    /// <summary>
    /// Loads the toolbar images and the window icon straight out of the assembly's embedded
    /// resources.
    ///
    /// The designer would normally put these in Form1.resx and read them back through
    /// ComponentResourceManager. That path stores the resource's CLR type as a string and
    /// resolves it at run time, which Native AOT cannot do once metadata is trimmed - it
    /// fails with "Could not resolve assembly 'System.Reflection.Metadata.AssemblyNameInfo'".
    /// Reading the bytes ourselves avoids type-name resolution entirely.
    /// </summary>
    internal static class Assets
    {
        private const string Prefix = "ApiTester.Assets.";

        private static readonly Dictionary<string, Image> imageCache = new(StringComparer.Ordinal);

        private static Stream Open(string fileName)
        {
            Assembly assembly = typeof(Assets).Assembly;
            Stream stream = assembly.GetManifestResourceStream(Prefix + fileName);

            if (stream is null)
            {
                throw new InvalidOperationException(
                    "Embedded resource '" + Prefix + fileName + "' is missing. Available: "
                    + string.Join(", ", assembly.GetManifestResourceNames()));
            }

            return stream;
        }

        /// <summary>
        /// An embedded bitmap. Cached, because several toolbar buttons reuse the same art.
        /// </summary>
        public static Image Image(string fileName)
        {
            if (imageCache.TryGetValue(fileName, out Image cached)) return cached;

            using Stream stream = Open(fileName);

            //Copy to memory first: Bitmap keeps the stream alive for its lifetime.
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;

            var image = new Bitmap(buffer);
            imageCache[fileName] = image;

            return image;
        }

        public static Icon Icon(string fileName)
        {
            using Stream stream = Open(fileName);
            return new Icon(stream);
        }
    }
}
