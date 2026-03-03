// LargePagedTextIndex.cs
// Indexa un archivo de texto grande guardando el offset (byte position) del inicio de cada página (PageSize líneas) para permitir Seek rápido.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AureusControl.Core.Services
{
    public sealed class LargePagedTextIndex
    {
        public string Path { get; }
        public int PageSize { get; }
        public IReadOnlyList<long> PageOffsets => _pageOffsets;
        public int TotalPages => _pageOffsets.Count;
        public string HeaderLine { get; private set; } = "";

        private readonly List<long> _pageOffsets = new();

        public LargePagedTextIndex(string path, int pageSize)
        {
            Path = path;
            PageSize = pageSize;
        }

        public Task BuildAsync(bool firstLineIsHeader, CancellationToken ct)
        {
            return Task.Run(() => Build(firstLineIsHeader, ct), ct);
        }

        private void Build(bool firstLineIsHeader, CancellationToken ct)
        {
            _pageOffsets.Clear();
            HeaderLine = "";

            using var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            long firstPageOffset = 0;

            if (firstLineIsHeader)
            {
                HeaderLine = ReadLineText(fs);
                firstPageOffset = fs.Position;
            }

            _pageOffsets.Add(firstPageOffset);

            int dataLineCount = 0;

            while (TryConsumeLine(fs, ct))
            {
                dataLineCount++;

                if (dataLineCount % PageSize == 0 && fs.Position < fs.Length)
                    _pageOffsets.Add(fs.Position);
            }

            if (_pageOffsets.Count == 0)
                _pageOffsets.Add(firstPageOffset);
        }

        private static string ReadLineText(FileStream fs)
        {
            var bytes = new List<byte>();
            int current;

            while ((current = fs.ReadByte()) != -1)
            {
                if (current == '\n')
                    break;

                bytes.Add((byte)current);
            }

            if (bytes.Count > 0 && bytes[^1] == '\r')
                bytes.RemoveAt(bytes.Count - 1);

            var line = Encoding.UTF8.GetString(bytes.ToArray());
            return line.TrimStart('\uFEFF');
        }

        private static bool TryConsumeLine(FileStream fs, CancellationToken ct)
        {
            bool consumed = false;
            int current;

            while ((current = fs.ReadByte()) != -1)
            {
                ct.ThrowIfCancellationRequested();
                consumed = true;

                if (current == '\n')
                    break;
            }

            return consumed;
        }
    }
}
