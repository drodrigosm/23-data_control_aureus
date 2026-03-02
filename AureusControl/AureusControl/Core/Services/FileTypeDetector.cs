// FileTypeDetector.cs
// Detecta el tipo de fichero (CSV, JSON, JSONL, LOG, TEXT) en base a extensión y contenido inicial.

using System;
using System.IO;

namespace AureusControl.Core.Services
{
    public enum LargeFileType { Csv, Json, Jsonl, Log, Text }

    public static class FileTypeDetector
    {
        public static LargeFileType Detect(string path)
        {
            var ext = Path.GetExtension(path)?.ToLowerInvariant() ?? "";
            if (ext == ".csv") return LargeFileType.Csv;
            if (ext == ".jsonl") return LargeFileType.Jsonl;
            if (ext == ".log") return LargeFileType.Log;
            if (ext == ".json") return LargeFileType.Json;

            // Fallback: inspección rápida del contenido
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                var line = sr.ReadLine() ?? "";
                var t = line.TrimStart();
                if (t.StartsWith("{") || t.StartsWith("[")) return LargeFileType.Json;
                if (t.StartsWith("{") && (line.Contains("}{") || line.Contains("\"") || line.Contains(":"))) return LargeFileType.Jsonl;
            }
            catch { }

            return LargeFileType.Text;
        }
    }
}