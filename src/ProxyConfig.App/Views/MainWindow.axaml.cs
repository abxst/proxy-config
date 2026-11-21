using System.Text;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ProxyConfig.App.ViewModels;
using ProxyConfig.Core.Models;

namespace ProxyConfig.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void SaveConfig_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.CanExport)
            return;

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export server configuration",
                SuggestedFileName = viewModel.SelectedEngine == ServerEngine.Nginx
                    ? "nginx-site.conf" : "apache-vhost.conf",
                DefaultExtension = "conf",
                FileTypeChoices =
                [
                    new FilePickerFileType("Server configuration") { Patterns = ["*.conf"] }
                ]
            });

            if (file is null)
                return;

            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek)
                stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(viewModel.Preview);
            await writer.FlushAsync();
            viewModel.ExportStatus = $"Saved {file.Name}";
        }
        catch (Exception ex)
        {
            viewModel.ExportStatus = $"Export failed: {ex.Message}";
        }
    }
}
