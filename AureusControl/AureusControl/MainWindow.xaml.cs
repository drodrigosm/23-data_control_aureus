using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AureusControl.Core.Models;
using AureusControl.Core.Services;
using AureusControl.Core.Services.Parsers;
using AureusControl.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AureusControl
{
    public sealed partial class MainWindow : Window
    {
        private readonly BotFileLocatorService _locator = new();
        private readonly LargeFileViewerPage _viewerPage = new();
        private BotExecutionFiles _loadedFiles = new();

        private bool _suppressSelectorEvents;

        public MainWindow()
        {
            this.InitializeComponent();
            ContentFrame.Content = _viewerPage;
            NavView.SelectedItem = DataTab;

            LoadModeComboBox.SelectedItem = "Symbol";
            SymbolComboBox.Visibility = Visibility.Visible;
        }

        private async void LoadBot_Click(object sender, RoutedEventArgs e)
        {
            var botId = BotIdTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(botId))
                return;

            _loadedFiles = _locator.Locate(botId);

            MachineText.Text = $"Machine: {_loadedFiles.MachineName ?? "Not found"}";

            await PopulateSymbolSelectorAsync(_loadedFiles.CsvPath);
            await OpenSelectedTabFileAsync();
        }

        private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            await OpenSelectedTabFileAsync();
        }

        private async void LoadModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectorEvents)
                return;

            SymbolComboBox.Visibility = string.Equals(CurrentLoadMode(), "Symbol", StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible
                : Visibility.Collapsed;

            await OpenSelectedTabFileAsync();
        }

        private async void SymbolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectorEvents)
                return;

            if (!string.Equals(CurrentLoadMode(), "Symbol", StringComparison.OrdinalIgnoreCase))
                return;

            await OpenSelectedTabFileAsync();
        }

        private string CurrentLoadMode()
        {
            return LoadModeComboBox.SelectedItem?.ToString() ?? "Symbol";
        }

        private async System.Threading.Tasks.Task OpenSelectedTabFileAsync()
        {
            if (NavView.SelectedItem is not NavigationViewItem selectedItem)
                return;

            if (string.Equals(selectedItem.Tag?.ToString(), "charts", StringComparison.OrdinalIgnoreCase))
            {
                var csvReadable = CanReadFile(_loadedFiles.CsvPath);
                var entriesReadable = CanReadFile(_loadedFiles.EntriesPath);

                ViewerStatusText.Text = $"Charts: CSV {(csvReadable ? "OK" : "NO")}, Entries {(entriesReadable ? "OK" : "NO")}";
                _viewerPage.Clear();
                return;
            }

            string? path = selectedItem.Tag?.ToString() switch
            {
                "csv" => _loadedFiles.CsvPath,
                "log" => _loadedFiles.LogPath,
                "entries" => _loadedFiles.EntriesPath,
                "config" => _loadedFiles.ConfigPath,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(path))
            {
                ViewerStatusText.Text = "No se encontró fichero para esta pestaña.";
                _viewerPage.Clear();
                return;
            }

            try
            {
                var mode = CurrentLoadMode();
                var selectedSymbol = SymbolComboBox.SelectedItem?.ToString();

                ViewerStatusText.Text = $"Mostrando: {System.IO.Path.GetFileName(path)} · Modo {mode}";
                await _viewerPage.OpenAsync(path, mode, selectedSymbol);
            }
            catch (System.Exception ex)
            {
                ViewerStatusText.Text = $"Error al abrir: {ex.Message}";
                _viewerPage.Clear();
            }
        }

        private async System.Threading.Tasks.Task PopulateSymbolSelectorAsync(string? csvPath)
        {
            _suppressSelectorEvents = true;
            try
            {
                SymbolComboBox.ItemsSource = null;

                if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
                    return;

                var symbols = await System.Threading.Tasks.Task.Run(() => ExtractSymbols(csvPath));

                SymbolComboBox.ItemsSource = symbols;

                if (symbols.Count == 0)
                    return;

                var defaultSymbol = symbols.FirstOrDefault(s => string.Equals(s, "BTCUSDC", StringComparison.OrdinalIgnoreCase))
                                    ?? symbols[0];

                SymbolComboBox.SelectedItem = defaultSymbol;
            }
            finally
            {
                _suppressSelectorEvents = false;
            }
        }

        private static List<string> ExtractSymbols(string csvPath)
        {
            using var stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);

            var header = reader.ReadLine() ?? string.Empty;
            var delimiter = CsvLineParser.DetectDelimiter(header);
            var headers = CsvLineParser.Parse(header, delimiter);
            var symbolIndex = Array.FindIndex(headers, h => string.Equals(h, "Symbol", StringComparison.OrdinalIgnoreCase));

            if (symbolIndex < 0)
                return new List<string>();

            var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var fields = CsvLineParser.Parse(line, delimiter);
                if (symbolIndex >= fields.Length)
                    continue;

                var symbol = fields[symbolIndex]?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(symbol))
                    symbols.Add(symbol);
            }

            return symbols.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool CanReadFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}