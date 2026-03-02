// JsonTextLoader.cs
// Carga JSON como texto. Para archivos grandes, carga solo un máximo (en bytes) para no reventar la UI.

using System;
using System.IO;
using System.Text;

namespace AureusControl.Core.Services.Parsers
{
    public static class JsonTextLoader
    {
        public static string LoadText(string path, int maxBytes)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var toRead = (int)Math.Min(fs.Length, maxBytes);
            var buffer = new byte[toRead];
            var read = fs.Read(buffer, 0, toRead);
            return Encoding.UTF8.GetString(buffer, 0, read);
        }
    }
}