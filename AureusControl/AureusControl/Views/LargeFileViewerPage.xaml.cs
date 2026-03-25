using System;
using Windows.UI;
using System.Collections.Generic;
using System.Linq;
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

        private readonly List<Dictionary<string, string>> _allRows = new();
        private readonly List<Dictionary<string, string>> _filteredRows = new();
        private List<string> _visibleColumns = new();
        private readonly Dictionary<string, HashSet<string>> _columnValueFilters = new();
        private readonly Dictionary<string, List<string>> _columnDistinctCache = new();

        public LargeFileViewerPage()
        {
            this.InitializeComponent();
        }

        public async System.Threading.Tasks.Task OpenAsync(string path, string loadMode = "Symbol", string? selectedSymbol = null)
        {
            var type = FileTypeDetector.Detect(path);
            await _vm.OpenAsync(path, type);

            TitleText.Text = _vm.Title;
            StatusText.Text = _vm.Status;
            LoadingRing.IsActive = true;

            _visibleColumns = _vm.Columns.ToList();
            _columnValueFilters.Clear();
            _columnDistinctCache.Clear();
            _filteredRows.Clear();
            _allRows.Clear();

            var allRows = await _vm.LoadAllRowsAsync(default);
            _allRows.AddRange(ApplyLoadMode(allRows, loadMode, selectedSymbol));

            BuildColumnFilterControls();
            ApplyFiltersAndRefresh();

            LoadingRing.IsActive = false;
            StatusText.Text = $"Modo {loadMode}: {_allRows.Count} filas";
        }

        private List<Dictionary<string, string>> ApplyLoadMode(List<Dictionary<string, string>> rows, string loadMode, string? selectedSymbol)
        {
            if (_vm.FileType != LargeFileType.Csv)
                return rows;

            if (string.Equals(loadMode, "All", StringComparison.OrdinalIgnoreCase))
                return rows;

            if (string.Equals(loadMode, "BUY/SELL", StringComparison.OrdinalIgnoreCase))
            {
                return rows.Where(r =>
                {
                    if (!r.TryGetValue("Operation", out var op))
                        return false;

                    var value = op?.Trim() ?? string.Empty;
                    return value.Equals("BUY", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("SELL", StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            var symbol = string.IsNullOrWhiteSpace(selectedSymbol) ? "BTCUSDC" : selectedSymbol.Trim();

            return rows.Where(r =>
                r.TryGetValue("Symbol", out var rowSymbol)
                && string.Equals(rowSymbol?.Trim(), symbol, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public void Clear()
        {
            TitleText.Text = "";
            StatusText.Text = "";
            FooterText.Text = "";
            CsvGrid.ItemsSource = null;
            ColumnTogglePanel.Children.Clear();

            _allRows.Clear();
            _filteredRows.Clear();
            _visibleColumns.Clear();
            _columnValueFilters.Clear();
            _columnDistinctCache.Clear();
        }

        private bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(SearchTextBox.Text) ||
            _columnValueFilters.Any(kv => kv.Value.Count > 0);

        private void BuildColumnFilterControls()
        {
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

        private void ApplyFiltersAndRefresh()
        {
            _filteredRows.Clear();

            if (!HasActiveFilters)
            {
                RefreshGrid();
                return;
            }

            foreach (var row in _allRows)
            {
                if (RowMatchesFilters(row))
                    _filteredRows.Add(row);
            }

            RefreshGrid();
        }

        private bool RowMatchesFilters(Dictionary<string, string> row)
        {
            var search = SearchTextBox.Text?.Trim() ?? "";

            if (!string.IsNullOrWhiteSpace(search))
            {
                var globalMatch = row.Any(kv => kv.Value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true);
                if (!globalMatch)
                    return false;
            }

            foreach (var filter in _columnValueFilters)
            {
                if (filter.Value.Count == 0)
                    continue;

                if (!row.TryGetValue(filter.Key, out var value))
                    value = string.Empty;

                if (!filter.Value.Contains(value ?? string.Empty))
                    return false;
            }

            return true;
        }

        private void RefreshGrid()
        {
            var sourceRows = HasActiveFilters ? _filteredRows : _allRows;

            if (_vm.FileType == LargeFileType.Csv)
                CsvGrid.ItemsSource = BuildCsvTableRows(sourceRows);
            else
                CsvGrid.ItemsSource = sourceRows.Select(FormatRow).Cast<object>().ToList();

            if (HasActiveFilters)
                FooterText.Text = $"Filtradas {_filteredRows.Count} de {_allRows.Count} filas";
            else
                FooterText.Text = $"Mostrando {_allRows.Count} filas";
        }

        private List<object> BuildCsvTableRows(List<Dictionary<string, string>> sourceRows)
        {
            var list = new List<object>();
            if (_visibleColumns.Count == 0)
                return list;

            list.Add(BuildCsvHeaderRow());

            foreach (var row in sourceRows)
            {
                var values = _visibleColumns
                    .Select(c => row.TryGetValue(c, out var v) ? v : "")
                    .ToList();

                list.Add(BuildCsvDataRow(values));
            }

            return list;
        }

        private Grid BuildCsvHeaderRow()
        {
            var surfaceBrush = GetThemeBrush("AppSurfaceBrush", Color.FromArgb(255, 34, 57, 69));
            var primaryBrush = GetThemeBrush("AppPrimaryTextBrush", Color.FromArgb(255, 242, 187, 119));

            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 2),
                Background = surfaceBrush
            };

            for (int i = 0; i < _visibleColumns.Count; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            for (int i = 0; i < _visibleColumns.Count; i++)
            {
                var columnName = _visibleColumns[i];

                var cell = new Grid { Margin = new Thickness(4, 2, 4, 2) };
                cell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                cell.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var title = new TextBlock
                {
                    Text = columnName,
                    Margin = new Thickness(4, 2, 4, 2),
                    Foreground = primaryBrush,
                    FontWeight = FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var filterButton = new Button
                {
                    Content = "▼",
                    Padding = new Thickness(6, 0, 6, 0),
                    MinWidth = 28,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                filterButton.Click += async (_, _) => await ShowColumnFilterFlyoutAsync(filterButton, columnName);

                Grid.SetColumn(title, 0);
                Grid.SetColumn(filterButton, 1);
                cell.Children.Add(title);
                cell.Children.Add(filterButton);

                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }

            return grid;
        }

        private Grid BuildCsvDataRow(IReadOnlyList<string> values)
        {
            var primaryBrush = GetThemeBrush("AppPrimaryTextBrush", Color.FromArgb(255, 242, 187, 119));
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };

            for (int i = 0; i < values.Count; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            for (int i = 0; i < values.Count; i++)
            {
                var tb = new TextBlock
                {
                    Text = values[i] ?? "",
                    Margin = new Thickness(8, 4, 12, 4),
                    Foreground = primaryBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontWeight = FontWeights.Normal
                };

                Grid.SetColumn(tb, i);
                grid.Children.Add(tb);
            }

            return grid;
        }

        private async System.Threading.Tasks.Task ShowColumnFilterFlyoutAsync(FrameworkElement target, string columnName)
        {
            if (!_columnDistinctCache.TryGetValue(columnName, out var uniqueValues))
            {
                uniqueValues = _allRows
                    .Select(r => r.TryGetValue(columnName, out var value) ? value ?? string.Empty : string.Empty)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .ToList();

                _columnDistinctCache[columnName] = uniqueValues;
            }

            var selectedValues = _columnValueFilters.TryGetValue(columnName, out var existing)
                ? new HashSet<string>(existing)
                : new HashSet<string>(uniqueValues);

            var valuesPanel = new StackPanel { Spacing = 4 };
            var valueChecks = new List<CheckBox>();

            foreach (var value in uniqueValues)
            {
                var checkbox = new CheckBox
                {
                    Content = string.IsNullOrEmpty(value) ? "(vacío)" : value,
                    IsChecked = selectedValues.Contains(value),
                    Tag = value
                };

                valueChecks.Add(checkbox);
                valuesPanel.Children.Add(checkbox);
            }

            var selectAll = new CheckBox { Content = "Seleccionar todo", IsChecked = valueChecks.All(v => v.IsChecked == true) };
            selectAll.Checked += (_, _) =>
            {
                foreach (var cb in valueChecks)
                    cb.IsChecked = true;
            };
            selectAll.Unchecked += (_, _) =>
            {
                foreach (var cb in valueChecks)
                    cb.IsChecked = false;
            };

            var applyButton = new Button { Content = "Aplicar", HorizontalAlignment = HorizontalAlignment.Stretch };
            var clearButton = new Button { Content = "Quitar filtro", HorizontalAlignment = HorizontalAlignment.Stretch };

            var footerButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            footerButtons.Children.Add(applyButton);
            footerButtons.Children.Add(clearButton);

            var flyoutContent = new StackPanel { Spacing = 8, MinWidth = 280, MaxWidth = 360 };
            flyoutContent.Children.Add(new TextBlock
            {
                Text = $"Filtro: {columnName}",
                FontWeight = FontWeights.SemiBold
            });
            flyoutContent.Children.Add(selectAll);
            flyoutContent.Children.Add(new ScrollViewer
            {
                MaxHeight = 300,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = valuesPanel
            });
            flyoutContent.Children.Add(footerButtons);

            var flyout = new Flyout { Content = flyoutContent };

            applyButton.Click += (_, _) =>
            {
                var selected = valueChecks
                    .Where(v => v.IsChecked == true)
                    .Select(v => v.Tag?.ToString() ?? string.Empty)
                    .ToHashSet(StringComparer.Ordinal);

                _columnValueFilters[columnName] = selected;
                ApplyFiltersAndRefresh();
                flyout.Hide();
            };

            clearButton.Click += (_, _) =>
            {
                _columnValueFilters.Remove(columnName);
                ApplyFiltersAndRefresh();
                flyout.Hide();
            };

            flyout.ShowAt(target);
        }

        private static SolidColorBrush GetThemeBrush(string key, Color fallback)
        {
            if (Application.Current.Resources.TryGetValue(key, out var value) && value is SolidColorBrush brush)
                return brush;

            return new SolidColorBrush(fallback);
        }

        private static string FormatRow(Dictionary<string, string> row)
        {
            if (row.Count == 0)
                return string.Empty;

            return string.Join(" | ", row.Select(kv => $"{kv.Key}: {kv.Value}"));
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFiltersAndRefresh();
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";

            foreach (var child in ColumnTogglePanel.Children.OfType<CheckBox>())
                child.IsChecked = true;

            _visibleColumns = _vm.Columns.ToList();
            _columnValueFilters.Clear();
            _filteredRows.Clear();

            RefreshGrid();
        }
    }
}
