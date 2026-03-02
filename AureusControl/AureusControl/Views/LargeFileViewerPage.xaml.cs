using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AureusControl.Core.Services;
using AureusControl.ViewModels;

namespace AureusControl.Views
{
    public sealed partial class LargeFileViewerPage : Page
    {
        private readonly LargeFileViewerViewModel _vm = new();

        public LargeFileViewerPage()
        {
            this.InitializeComponent();
        }

        public async System.Threading.Tasks.Task OpenAsync(string path)
        {
            var type = FileTypeDetector.Detect(path);
            await _vm.OpenAsync(path, type);

            TitleText.Text = _vm.Title;
            StatusText.Text = _vm.Status;
            FooterText.Text = $"Página {_vm.CurrentPage + 1}/{_vm.TotalPages} · {_vm.Rows.Count} filas";

            CsvGrid.ItemsSource = BuildRowsForView(type);
        }

        public void Clear()
        {
            TitleText.Text = "";
            StatusText.Text = "";
            FooterText.Text = "";
            CsvGrid.ItemsSource = null;
        }

        private List<object> BuildRowsForView(LargeFileType type)
        {
            if (type == LargeFileType.Csv)
                return BuildCsvTableRows();

            return _vm.Rows.Select(FormatRow).Cast<object>().ToList();
        }

        private List<object> BuildCsvTableRows()
        {
            var list = new List<object>();

            if (_vm.Columns.Count == 0)
                return list;

            // Header
            list.Add(BuildCsvGridRow(_vm.Columns, true));

            // Data rows
            foreach (var row in _vm.Rows)
            {
                var values = _vm.Columns
                    .Select(c => row.TryGetValue(c, out var v) ? v : "")
                    .ToList();

                list.Add(BuildCsvGridRow(values, false));
            }

            return list;
        }

        private static Grid BuildCsvGridRow(IReadOnlyList<string> values, bool isHeader)
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 2),
                Background = isHeader
                    ? new SolidColorBrush(Color.FromArgb(255, 230, 230, 230))
                    : null
            };

            for (int i = 0; i < values.Count; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            for (int i = 0; i < values.Count; i++)
            {
                var tb = new TextBlock
                {
                    Text = values[i] ?? "",
                    Margin = new Thickness(8, 4, 12, 4),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal
                };

                Grid.SetColumn(tb, i);
                grid.Children.Add(tb);
            }

            return grid;
        }

        private static string FormatRow(Dictionary<string, string> row)
        {
            if (row.Count == 0)
                return string.Empty;

            return string.Join(" | ", row.Select(kv => $"{kv.Key}: {kv.Value}"));
        }
    }
}
