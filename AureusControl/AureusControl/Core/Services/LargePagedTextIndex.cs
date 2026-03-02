// LargePagedTextIndex.cs
// Indexa un archivo de texto grande guardando el offset (byte position) del inicio de cada página (PageSize líneas) para permitir Seek rápido.

using System;
using System.Collections.Generic;
using System.IO;
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

            using var fs = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);

            long currentOffset = 0;
            int lineIndex = 0;
            bool headerRead = false;

            // Offset de página 0 siempre al inicio (después de header si procede)
            if (firstLineIsHeader)
            {
                var headerOffset = fs.Position;
                HeaderLine = sr.ReadLine() ?? "";
                headerRead = true;
                currentOffset = fs.Position;
            }

            _pageOffsets.Add(currentOffset);

            while (!sr.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();

                var before = fs.Position;
                var line = sr.ReadLine();
                if (line == null) break;

                lineIndex++;

                if (lineIndex % PageSize == 0)
                {
                    var nextPageOffset = fs.Position;
                    _pageOffsets.Add(nextPageOffset);
                }

                // protección de progreso: si no avanza por alguna razón, corta
                if (fs.Position <= before) break;
            }

            if (!headerRead && firstLineIsHeader) HeaderLine = "";
        }
    }
}