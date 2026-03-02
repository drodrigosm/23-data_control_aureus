// LargeFilePageReader.cs
// Lee páginas de un fichero grande haciendo Seek por offset y leyendo N líneas. No carga todo el fichero en memoria.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AureusControl.Core.Services
{
    public sealed class LargeFilePageReader
    {
        private readonly string _path;

        public LargeFilePageReader(string path)
        {
            _path = path;
        }

        public Task<List<string>> ReadPageLinesAsync(long offset, int pageSize, CancellationToken ct)
        {
            return Task.Run(() => ReadPageLines(offset, pageSize, ct), ct);
        }

        private List<string> ReadPageLines(long offset, int pageSize, CancellationToken ct)
        {
            var lines = new List<string>(pageSize);

            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(offset, SeekOrigin.Begin);

            using var sr = new StreamReader(fs);

            while (!sr.EndOfStream && lines.Count < pageSize)
            {
                ct.ThrowIfCancellationRequested();
                var line = sr.ReadLine();
                if (line == null) break;
                lines.Add(line);
            }

            return lines;
        }
    }
}