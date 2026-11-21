using ProxyConfig.Core.Syntax;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using ProxyConfig.App.ViewModels;

namespace ProxyConfig.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void ImportConfig_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import NGINX / Apache configuration",
                AllowMultiple = true
            });
            if (files.Count == 0)
                return;

            var documents = new List<ConfigImportedFile>();
            foreach (var file in files)
            {
                await using var stream = await file.OpenReadAsync();
                if (stream.CanSeek && stream.Length > 8 * 1024 * 1024)
                    throw new IOException($"Config file exceeds 8 MiB: {file.Name}");
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                if (buffer.Length > 8 * 1024 * 1024)
                    throw new IOException($"Config file exceeds 8 MiB: {file.Name}");
                var decoded = ConfigFileCodec.Decode(buffer.ToArray());
                documents.Add(new ConfigImportedFile(file.Name, decoded.Text, decoded.Encoding));
            }
            viewModel.ImportFiles(documents);
            viewModel.ExportStatus = string.Empty;
        }
        catch (Exception ex)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.ExportStatus = $"Import failed: {ex.Message}";
        }
    }

    private async void ImportFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Import configuration folder",
                AllowMultiple = false
            });
            var rootPath = folders.FirstOrDefault()?.TryGetLocalPath();
            if (rootPath is null)
                return;

            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            };
            var candidates = Directory.EnumerateFiles(rootPath, "*", options)
                .Where(path => IsConfigCandidate(Path.GetFileName(path)))
                .Take(1001)
                .ToArray();
            if (candidates.Length > 1000)
                throw new IOException("Folder has more than 1,000 configuration files. Select a smaller folder.");

            var documents = new List<ConfigImportedFile>();
            long cumulativeBytes = 0;
            foreach (var file in candidates)
            {
                var length = new FileInfo(file).Length;
                if (length > 8 * 1024 * 1024 || (cumulativeBytes += length) > 40 * 1024 * 1024)
                    throw new IOException("Imported config files exceed the 8 MiB/file or 40 MiB/folder limit.");
                var relativeName = Path.GetRelativePath(rootPath, file).Replace('\\', '/');
                var decoded = ConfigFileCodec.Decode(await File.ReadAllBytesAsync(file));
                documents.Add(new ConfigImportedFile(
                    ConfigWorkspace.NormalizeRelativePath(relativeName), decoded.Text, decoded.Encoding));
            }
            viewModel.ImportFiles(documents);
            viewModel.ExportStatus = string.Empty;
        }
        catch (Exception ex)
        {
            if (DataContext is MainWindowViewModel vm)
                vm.ExportStatus = $"Folder import failed: {ex.Message}";
        }
    }

    private static bool IsConfigCandidate(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return fileName.Equals(".htaccess", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("mime.types", StringComparison.OrdinalIgnoreCase) ||
            new[] { ".conf", ".inc", ".include", ".load", ".vhost" }
                .Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private async void SaveWorkspaceZip_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.HasWorkspaceFiles)
            return;
        try
        {
            var target = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export all imported configuration files",
                SuggestedFileName = "proxy-config-workspace.zip",
                DefaultExtension = "zip",
                FileTypeChoices =
                [
                    new FilePickerFileType("Configuration archive") { Patterns = ["*.zip"] }
                ]
            });
            if (target is null)
                return;
            await using var output = await target.OpenWriteAsync();
            if (output.CanSeek)
            {
                output.SetLength(0);
                output.Position = 0;
            }
            viewModel.ExportWorkspaceSnapshot().WriteZip(output);
            await output.FlushAsync();
            viewModel.ExportStatus = $"Saved project ZIP: {target.Name}";
        }
        catch (Exception ex)
        {
            viewModel.ExportStatus = $"Workspace export failed: {ex.Message}";
        }
    }

    private async void SaveConfig_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel || !viewModel.CanExport)
            return;

        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export server configuration",
                SuggestedFileName = viewModel.SuggestedFileName,
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
            {
                stream.SetLength(0);
                stream.Position = 0;
            }
            var encoding = viewModel.IsImport && viewModel.SelectedWorkspaceFile is { } selected
                ? selected.Encoding : ConfigEncodingKind.Utf8;
            await stream.WriteAsync(ConfigFileCodec.Encode(viewModel.ExportText, encoding));
            await stream.FlushAsync();
            viewModel.ExportStatus = $"Saved {file.Name}";
        }
        catch (Exception ex)
        {
            viewModel.ExportStatus = $"Export failed: {ex.Message}";
        }
    }
}
