using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using AureusControl.Core.Services;
using AureusControl.Core.Services.Parsers;

namespace AureusControl.ViewModels
{
    public sealed class LargeFileViewerViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<Dictionary<string, string>> Rows { get; } = new();

        public string Title { get; private set; } = "";
        public bool IsLoading { get; private set; }
        public string Status { get; private set; } = "";

        public LargeFileType FileType { get; private set; }
        public string Path { get; private set; } = "";

        public int PageSize { get; private set; } = 300;
        public int CurrentPage { get; private set; }
        public int TotalPages { get; private set; }

        public List<string> Columns { get; } = new();

        private LargePagedTextIndex? _index;
        private LargeFilePageReader? _reader;
        private readonly PageCache<List<Dictionary<string, string>>> _cache = new(3);
        private CancellationTokenSource? _cts;

        private char _csvDelimiter = ',';

        public async Task OpenAsync(string path, LargeFileType type)
        {
            Cancel();
            _cts = new CancellationTokenSource();

            Path = path;
            FileType = type;
            CurrentPage = 0;
            Rows.Clear();
            Columns.Clear();
            _cache.Clear();

            Title = System.IO.Path.GetFileName(path);

            SetLoading(true, "Indexing...");

            _reader = new LargeFilePageReader(path);

            bool firstLineHeader = type == LargeFileType.Csv;
            _index = new LargePagedTextIndex(path, PageSize);

            await _index.BuildAsync(firstLineHeader, _cts.Token);

            TotalPages = Math.Max(1, _index.TotalPages);

            if (type == LargeFileType.Csv)
            {
                _csvDelimiter = CsvLineParser.DetectDelimiter(_index.HeaderLine);
                var headerFields = CsvLineParser.Parse(_index.HeaderLine, _csvDelimiter);
                for (int i = 0; i < headerFields.Length; i++)
                    Columns.Add(headerFields[i]);
            }
            else if (type == LargeFileType.Log || type == LargeFileType.Text)
            {
                Columns.Add("LineNumber");
                Columns.Add("LineText");
            }

            SetLoading(false, $"Ready. Pages: {TotalPages}");

            await LoadPageAsync(0, true);
        }

        public async Task LoadNextPageIfNeededAsync()
        {
            if (IsLoading) return;
            if (_index == null) return;
            if (CurrentPage + 1 >= TotalPages) return;

            await LoadPageAsync(CurrentPage + 1, false);
        }

        public async Task LoadPrevPageAsync()
        {
            if (IsLoading) return;
            if (_index == null) return;
            if (CurrentPage <= 0) return;

            await LoadPageAsync(CurrentPage - 1, true);
        }

        public async Task LoadPageAsync(int pageIndex, bool replace)
        {
            if (_index == null || _reader == null || _cts == null)
                return;

            if (pageIndex < 0 || pageIndex >= TotalPages)
                return;

            SetLoading(true, $"Loading page {pageIndex + 1}/{TotalPages}...");

            if (!_cache.TryGet(pageIndex, out var parsedRows))
            {
                var offset = _index.PageOffsets[pageIndex];
                var lines = await _reader.ReadPageLinesAsync(offset, PageSize, _cts.Token);
                parsedRows = ParseLines(lines, pageIndex);
                _cache.Set(pageIndex, parsedRows);
            }

            if (replace)
                Rows.Clear();

            foreach (var row in parsedRows)
                Rows.Add(row);

            CurrentPage = pageIndex;

            SetLoading(false, $"Page {CurrentPage + 1}/{TotalPages}");
        }

        private List<Dictionary<string, string>> ParseLines(List<string> lines, int pageIndex)
        {
            var result = new List<Dictionary<string, string>>(lines.Count);

            if (FileType == LargeFileType.Csv)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var fields = CsvLineParser.Parse(lines[i], _csvDelimiter);
                    var row = new Dictionary<string, string>();

                    for (int c = 0; c < Columns.Count; c++)
                    {
                        var value = c < fields.Length ? fields[c] : "";
                        row[Columns[c]] = value;
                    }

                    result.Add(row);
                }

                return result;
            }

            if (FileType == LargeFileType.Jsonl)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var map = JsonlLineParser.ParseToMap(lines[i]);

                    foreach (var key in map.Keys)
                        if (!Columns.Contains(key))
                            Columns.Add(key);

                    result.Add(map);
                }

                return result;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var row = new Dictionary<string, string>
                {
                    ["LineNumber"] = ((pageIndex * PageSize) + i + 1).ToString(),
                    ["LineText"] = lines[i] ?? ""
                };

                result.Add(row);
            }

            return result;
        }

        public void Cancel()
        {
            try { _cts?.Cancel(); } catch { }
        }

        private void SetLoading(bool loading, string status)
        {
            IsLoading = loading;
            Status = status;
            Raise(nameof(IsLoading));
            Raise(nameof(Status));
            Raise(nameof(Title));
        }

        private void Raise(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}