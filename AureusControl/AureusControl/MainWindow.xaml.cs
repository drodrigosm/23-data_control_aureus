using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AureusControl.Core.Models;
using AureusControl.Core.Services;
using AureusControl.Core.Services.Parsers;
using AureusControl.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace AureusControl
{
    public sealed partial class MainWindow : Window
    {
        private readonly BotFileLocatorService _locator = new();
        private readonly LargeFileViewerPage _viewerPage = new();
        private readonly DbConnectionSettingsStore _dbSettingsStore = new();
        private readonly DbConnectionService _dbConnectionService = new();
        private BotExecutionFiles _loadedFiles = new();
        private DbConnectionSettings _dbSettings = new();
        private IReadOnlyList<string> _liveTables = Array.Empty<string>();
        private IReadOnlyList<string> _testnetTables = Array.Empty<string>();

        private bool _suppressSelectorEvents;

        public MainWindow()
        {
            this.InitializeComponent();
            ContentFrame.Content = _viewerPage;
            NavView.SelectedItem = DataTab;

            LoadModeComboBox.SelectedItem = "Symbol";
            SymbolComboBox.Visibility = Visibility.Visible;

            _dbSettings = _dbSettingsStore.Load();
            SetDbStatusIndicator(false, "Sin conexión");
            Loaded += MainWindow_Loaded;
            _ = AutoConnectOnStartupAsync();
        }


        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDbModeText();
            UpdateDbTablesBindings();
        }

        private async System.Threading.Tasks.Task AutoConnectOnStartupAsync()
        {
            if (!_dbSettings.AutoConnectOnStartup)
                return;

            try
            {
                await ConnectAndReflectStatusAsync(_dbSettings);
            }
            catch
            {
                SetDbStatusIndicator(false, "Sin conexión");
            }
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

            if (string.Equals(selectedItem.Tag?.ToString(), "dbtables", StringComparison.OrdinalIgnoreCase))
            {
                await LoadAndShowDbTablesAsync();
                return;
            }

            if (string.Equals(selectedItem.Tag?.ToString(), "charts", StringComparison.OrdinalIgnoreCase))
            {
                var csvReadable = CanReadFile(_loadedFiles.CsvPath);
                var entriesReadable = CanReadFile(_loadedFiles.EntriesPath);

                ViewerStatusText.Text = $"Charts: CSV {(csvReadable ? "OK" : "NO")}, Entries {(entriesReadable ? "OK" : "NO")}";
                _viewerPage.Clear();
                return;
            }

            DbTablesPanel.Visibility = Visibility.Collapsed;
            ContentFrame.Visibility = Visibility.Visible;

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

        private async void DbConfig_Click(object sender, RoutedEventArgs e)
        {
            var hostBox = new TextBox { Header = "Host", Text = _dbSettings.Host };
            var portBox = new TextBox { Header = "Puerto", Text = _dbSettings.Port.ToString() };
            var dbBox = new TextBox { Header = "Database", Text = _dbSettings.Database };
            var userBox = new TextBox { Header = "Usuario", Text = _dbSettings.Username };
            var passBox = new PasswordBox { Header = "Password", Password = _dbSettings.Password };
            var autoConnectBox = new CheckBox
            {
                Content = "Conectar automáticamente al iniciar",
                IsChecked = _dbSettings.AutoConnectOnStartup
            };
            var testButton = new Button
            {
                Content = "Connect",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var feedbackText = new TextBlock { Opacity = 0.85, Text = "Prueba la conexión antes de guardar." };

            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(hostBox);
            panel.Children.Add(portBox);
            panel.Children.Add(dbBox);
            panel.Children.Add(userBox);
            panel.Children.Add(passBox);
            panel.Children.Add(autoConnectBox);
            panel.Children.Add(testButton);
            panel.Children.Add(feedbackText);

            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "DB Config",
                PrimaryButtonText = "Guardar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Primary,
                Content = panel
            };

            testButton.Click += async (_, __) =>
            {
                var candidate = BuildSettingsFromControls(hostBox, portBox, dbBox, userBox, passBox, autoConnectBox);
                if (candidate is null)
                {
                    feedbackText.Foreground = new SolidColorBrush(Colors.OrangeRed);
                    feedbackText.Text = "Puerto inválido.";
                    SetDbStatusIndicator(false, "Sin conexión");
                    return;
                }

                testButton.IsEnabled = false;
                SetDbStatusIndicator(false, "Conectando...");
                feedbackText.Foreground = new SolidColorBrush(Colors.DodgerBlue);
                feedbackText.Text = "Conectando...";
                var (connected, message) = await _dbConnectionService.TestConnectionAsync(candidate);
                feedbackText.Foreground = new SolidColorBrush(connected ? Colors.ForestGreen : Colors.OrangeRed);
                feedbackText.Text = connected ? "Conexión correcta." : $"Sin conexión: {message}";
                SetDbStatusIndicator(connected, connected ? "Conectado" : "Sin conexión");
                testButton.IsEnabled = true;
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;

            var settings = BuildSettingsFromControls(hostBox, portBox, dbBox, userBox, passBox, autoConnectBox);
            if (settings is null)
            {
                SetDbStatusIndicator(false, "Sin conexión");
                return;
            }

            _dbSettings = settings;
            _dbSettingsStore.Save(_dbSettings);

            if (_dbSettings.AutoConnectOnStartup)
            {
                await ConnectAndReflectStatusAsync(_dbSettings);
            }
            else
            {
                SetDbStatusIndicator(false, "Sin conexión");
            }

            if (NavView.SelectedItem == DbTablesTab)
                await LoadAndShowDbTablesAsync();
        }

        private async Task ConnectAndReflectStatusAsync(DbConnectionSettings settings)
        {
            var (connected, message) = await _dbConnectionService.TestConnectionAsync(settings);
            SetDbStatusIndicator(connected, connected ? "Conectado" : "Sin conexión");

            if (!connected)
            {
                _liveTables = Array.Empty<string>();
                _testnetTables = Array.Empty<string>();
                UpdateDbTablesBindings();
                ViewerStatusText.Text = $"Sin conexión DB: {message}";
                return;
            }

            await RefreshDbTablesAsync();
        }

        private DbConnectionSettings? BuildSettingsFromControls(
            TextBox hostBox,
            TextBox portBox,
            TextBox dbBox,
            TextBox userBox,
            PasswordBox passBox,
            CheckBox autoConnectBox)
        {
            if (!int.TryParse(portBox.Text?.Trim(), out var port))
                return null;

            return new DbConnectionSettings
            {
                Host = hostBox.Text?.Trim() ?? string.Empty,
                Port = port,
                Database = dbBox.Text?.Trim() ?? string.Empty,
                Username = userBox.Text?.Trim() ?? string.Empty,
                Password = passBox.Password ?? string.Empty,
                AutoConnectOnStartup = autoConnectBox.IsChecked == true
            };
        }

        private async Task RefreshDbTablesAsync()
        {
            var result = await _dbConnectionService.GetBotTablesAsync(_dbSettings);
            if (!result.Success)
            {
                _liveTables = Array.Empty<string>();
                _testnetTables = Array.Empty<string>();
                UpdateDbTablesBindings();
                ViewerStatusText.Text = $"No se pudieron cargar tablas DB: {result.Message}";
                return;
            }

            _liveTables = result.LiveTables;
            _testnetTables = result.TestnetTables;
            UpdateDbTablesBindings();
        }

        private async Task LoadAndShowDbTablesAsync()
        {
            DbTablesPanel.Visibility = Visibility.Visible;
            ContentFrame.Visibility = Visibility.Collapsed;
            UpdateDbModeText();

            await RefreshDbTablesAsync();

            var selectedMode = UseTestnetCheckBox.IsChecked == true ? "testnet" : "live";
            var count = UseTestnetCheckBox.IsChecked == true ? _testnetTables.Count : _liveTables.Count;
            ViewerStatusText.Text = $"DB Tables ({selectedMode}): {count} tablas.";
        }

        private void UpdateDbModeText()
        {
            if (UseTestnetCheckBox is null || DbTablesModeText is null)
                return;

            var usingTestnet = UseTestnetCheckBox.IsChecked == true;
            DbTablesModeText.Text = usingTestnet
                ? "Modo activo: Testnet (tablas con sufijo _testnet)."
                : "Modo activo: Live (tablas sin sufijo _testnet).";
        }

        private void UpdateDbTablesBindings()
        {
            if (LiveTablesListView is null || TestnetTablesListView is null)
                return;

            LiveTablesListView.ItemsSource = _liveTables;
            TestnetTablesListView.ItemsSource = _testnetTables;
        }

        private async void UseTestnetCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateDbModeText();

            if (NavView.SelectedItem == DbTablesTab)
                await LoadAndShowDbTablesAsync();
        }

        private void SetDbStatusIndicator(bool connected, string text)
        {
            if (DbStatusLamp is not null)
                DbStatusLamp.Fill = new SolidColorBrush(connected ? Colors.LimeGreen : Colors.Red);

            if (DbStatusText is not null)
                DbStatusText.Text = text;
        }
    }
}
