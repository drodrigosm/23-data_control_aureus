using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AureusControl.Core.Services;
using AureusControl.Core.Models;
using AureusControl.Views;

namespace AureusControl
{
    public sealed partial class MainWindow : Window
    {
        private readonly BotFileLocatorService _locator = new();
        private readonly LargeFileViewerPage _viewerPage = new();
        private BotExecutionFiles _loadedFiles = new();

        public MainWindow()
        {
            this.InitializeComponent();
            ContentFrame.Content = _viewerPage;
            NavView.SelectedItem = DataTab;
        }

        private async void LoadBot_Click(object sender, RoutedEventArgs e)
        {
            var botId = BotIdTextBox.Text?.Trim();

            if (string.IsNullOrWhiteSpace(botId))
                return;

            _loadedFiles = _locator.Locate(botId);

            MachineText.Text = $"Machine: {_loadedFiles.MachineName ?? "Not found"}";
            await OpenSelectedTabFileAsync();
        }

        private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            await OpenSelectedTabFileAsync();
        }

        private async System.Threading.Tasks.Task OpenSelectedTabFileAsync()
        {
            if (NavView.SelectedItem is not NavigationViewItem selectedItem)
                return;

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
                ViewerStatusText.Text = $"Mostrando: {System.IO.Path.GetFileName(path)}";
                await _viewerPage.OpenAsync(path);
            }
            catch (System.Exception ex)
            {
                ViewerStatusText.Text = $"Error al abrir: {ex.Message}";
                _viewerPage.Clear();
            }
        }
    }
}
