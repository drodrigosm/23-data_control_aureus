using System;
using Windows.UI;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AureusControl.Core.Services;
using AureusControl.ViewModels;

namespace AureusControl.Views
{
    public sealed partial class LargeFileViewerPage : Page
    {
        private readonly LargeFileViewerViewModel _vm = new();
        private List<Dictionary<string, string>> _pageRows = new();
        private List<string> _visibleColumns = new();

        public LargeFileViewerPage()
        {
            this.InitializeComponent();
        }

        public async System.Threading.Tasks.Task OpenAsync(string path)
        {
            var type = FileTypeDetector.Detect(path);
            await _vm.OpenAsync(path, type);
            SyncHeader();

            _pageRows = _vm.Rows.Select(r => new Dictionary<string, string>(r)).ToList();
            _visibleColumns = _vm.Columns.ToList();

            BuildColumnFilterControls();
            RefreshGrid();
        }

        public void Clear()
        {
            TitleText.Text = "";
            StatusText.Text = "";
            FooterText.Text = "";
            CsvGrid.ItemsSource = null;
            ColumnFilterCombo.ItemsSource = null;
            ColumnTogglePanel.Children.Clear();
            _pageRows.Clear();
            _visibleColumns.Clear();
        }

        private async System.Threading.Tasks.Task ChangePageAsync(int page)
        {
            if (page < 0 || page >= _vm.TotalPages)
                return;

            await _vm.LoadPageAsync(page, true);
            SyncHeader();
            _pageRows = _vm.Rows.Select(r => new Dictionary<string, string>(r)).ToList();
            RefreshGrid();
        }

        private void SyncHeader()
        {
            TitleText.Text = _vm.Title;
            StatusText.Text = _vm.Status;
            LoadingRing.IsActive = _vm.IsLoading;
            PageNumberBox.Value = _vm.CurrentPage + 1;
            PrevButton.IsEnabled = _vm.CurrentPage > 0;
            NextButton.IsEnabled = _vm.CurrentPage + 1 < _vm.TotalPages;
        }

        private void BuildColumnFilterControls()
        {
            ColumnFilterCombo.ItemsSource = _vm.Columns;
            ColumnFilterCombo.SelectedIndex = -1;
            ColumnTogglePanel.Children.Clear();

            foreach (var column in _vm.Columns)
            {
                var check = new CheckBox
                {
                    Content = column,
                    IsChecked = true,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                check.Checked += (_, _) => ToggleColumn(column, true);
                check.Unchecked += (_, _) => ToggleColumn(column, false);

                ColumnTogglePanel.Children.Add(check);
            }
        }

        private void ToggleColumn(string column, bool visible)
        {
            if (visible && !_visibleColumns.Contains(column))
                _visibleColumns.Add(column);
            else if (!visible)
                _visibleColumns.Remove(column);

            _visibleColumns = _vm.Columns.Where(_visibleColumns.Contains).ToList();
            RefreshGrid();
        }

        private void RefreshGrid()
        {
            var rows = ApplyFilters(_pageRows);

            if (_vm.FileType == LargeFileType.Csv)
                CsvGrid.ItemsSource = BuildCsvTableRows(rows);
            else
                CsvGrid.ItemsSource = rows.Select(FormatRow).Cast<object>().ToList();

            FooterText.Text = $"Página {_vm.CurrentPage + 1}/{_vm.TotalPages} · Mostrando {rows.Count} de {_pageRows.Count} filas";
        }

        private List<Dictionary<string, string>> ApplyFilters(List<Dictionary<string, string>> inputRows)
        {
            var search = SearchTextBox.Text?.Trim() ?? "";
            var selectedColumn = ColumnFilterCombo.SelectedItem as string;
            var filterValue = ColumnFilterTextBox.Text?.Trim() ?? "";

            var output = inputRows.Where(row =>
            {
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var globalMatch = row
                        .Where(kv => _visibleColumns.Contains(kv.Key))
                        .Any(kv => kv.Value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);

                    if (!globalMatch)
                        return false;
                }

                if (!string.IsNullOrWhiteSpace(selectedColumn) && !string.IsNullOrWhiteSpace(filterValue))
                {
                    if (!row.TryGetValue(selectedColumn, out var value) ||
                        value?.Contains(filterValue, StringComparison.OrdinalIgnoreCase) != true)
                        return false;
                }

                return true;
            }).ToList();

            return output;
        }

        private List<object> BuildCsvTableRows(List<Dictionary<string, string>> sourceRows)
        {
            var list = new List<object>();
            if (_visibleColumns.Count == 0)
                return list;

            list.Add(BuildCsvGridRow(_visibleColumns, true));

            foreach (var row in sourceRows)
            {
                var values = _visibleColumns
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

        private async void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            await ChangePageAsync(_vm.CurrentPage - 1);
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            await ChangePageAsync(_vm.CurrentPage + 1);
        }

        private async void GoPageButton_Click(object sender, RoutedEventArgs e)
        {
            var targetPage = (int)Math.Max(1, PageNumberBox.Value) - 1;
            await ChangePageAsync(targetPage);
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshGrid();
        }

        private void ColumnFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RefreshGrid();
        }

        private void ColumnFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshGrid();
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            ColumnFilterTextBox.Text = "";
            ColumnFilterCombo.SelectedIndex = -1;

            foreach (var child in ColumnTogglePanel.Children.OfType<CheckBox>())
                child.IsChecked = true;

            _visibleColumns = _vm.Columns.ToList();
            RefreshGrid();
        }
    }
}
