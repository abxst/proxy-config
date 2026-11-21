using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Registry;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;

namespace ProxyConfig.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly DirectiveCatalog _catalog = DirectiveCatalog.LoadEmbedded();
    private ServerEngine _selectedEngine = ServerEngine.Nginx;
    private TargetPlatform _selectedTargetPlatform = TargetPlatform.Linux;
    private string _domain = "example.com";
    private string _staticDirectory = "/srv/www/example";
    private string _urlPath = "/";
    private int _listenPort = 80;
    private string _indexFiles = "index.html index.htm";
    private bool _spaFallback;
    private bool _directoryListing;
    private bool _cacheStaticAssets;
    private bool _compression;
    private bool _isExplorer;
    private string _searchText = string.Empty;
    private bool _includeVariables;
    private DocumentedDirective? _selectedDirective;
    private string _searchResultCount = string.Empty;
    private string _preview = string.Empty;
    private string _validationMessage = string.Empty;
    private string _exportStatus = string.Empty;

    public MainWindowViewModel()
    {
        ShowStaticSiteCommand = new RelayCommand(() => IsExplorer = false);
        ShowExplorerCommand = new RelayCommand(() => IsExplorer = true);
        RefreshExplorer();
        Regenerate();
    }

    public IRelayCommand ShowStaticSiteCommand { get; }
    public IRelayCommand ShowExplorerCommand { get; }
    public ObservableCollection<DocumentedDirective> SearchResults { get; } = [];
    public IReadOnlyList<ServerEngine> ServerEngines { get; } = Enum.GetValues<ServerEngine>();
    public IReadOnlyList<TargetPlatform> TargetPlatforms { get; } = Enum.GetValues<TargetPlatform>();

    public bool IsExplorer
    {
        get => _isExplorer;
        set
        {
            if (SetProperty(ref _isExplorer, value))
                OnPropertyChanged(nameof(IsStaticSite));
        }
    }

    public bool IsStaticSite => !IsExplorer;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value)) RefreshExplorer();
        }
    }

    public bool IncludeVariables
    {
        get => _includeVariables;
        set
        {
            if (SetProperty(ref _includeVariables, value)) RefreshExplorer();
        }
    }

    public string SearchResultCount
    {
        get => _searchResultCount;
        private set => SetProperty(ref _searchResultCount, value);
    }

    public DocumentedDirective? SelectedDirective
    {
        get => _selectedDirective;
        set => SetProperty(ref _selectedDirective, value);
    }

    public ServerEngine SelectedEngine
    {
        get => _selectedEngine;
        set
        {
            if (SetProperty(ref _selectedEngine, value))
            {
                Regenerate();
                RefreshExplorer();
            }
        }
    }

    public TargetPlatform SelectedTargetPlatform
    {
        get => _selectedTargetPlatform;
        set
        {
            if (SetProperty(ref _selectedTargetPlatform, value)) Regenerate();
        }
    }

    public string Domain
    {
        get => _domain;
        set
        {
            if (SetProperty(ref _domain, value)) Regenerate();
        }
    }

    public string StaticDirectory
    {
        get => _staticDirectory;
        set
        {
            if (SetProperty(ref _staticDirectory, value)) Regenerate();
        }
    }

    public string UrlPath
    {
        get => _urlPath;
        set
        {
            if (SetProperty(ref _urlPath, value)) Regenerate();
        }
    }

    public int ListenPort
    {
        get => _listenPort;
        set
        {
            if (SetProperty(ref _listenPort, value)) Regenerate();
        }
    }

    public string IndexFiles
    {
        get => _indexFiles;
        set
        {
            if (SetProperty(ref _indexFiles, value)) Regenerate();
        }
    }

    public bool SpaFallback
    {
        get => _spaFallback;
        set
        {
            if (SetProperty(ref _spaFallback, value)) Regenerate();
        }
    }

    public bool DirectoryListing
    {
        get => _directoryListing;
        set
        {
            if (SetProperty(ref _directoryListing, value)) Regenerate();
        }
    }

    public bool CacheStaticAssets
    {
        get => _cacheStaticAssets;
        set
        {
            if (SetProperty(ref _cacheStaticAssets, value)) Regenerate();
        }
    }

    public bool Compression
    {
        get => _compression;
        set
        {
            if (SetProperty(ref _compression, value)) Regenerate();
        }
    }

    public string Preview
    {
        get => _preview;
        private set => SetProperty(ref _preview, value);
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public bool CanExport => string.IsNullOrEmpty(ValidationMessage) && !string.IsNullOrWhiteSpace(Preview);

    public string ExportStatus
    {
        get => _exportStatus;
        set => SetProperty(ref _exportStatus, value);
    }

    private void RefreshExplorer()
    {
        var server = SelectedEngine == ServerEngine.Nginx ? "nginx" : "apache";
        var results = _catalog.Search(server, SearchText, IncludeVariables);
        SearchResults.Clear();
        foreach (var entry in results)
            SearchResults.Add(entry);
        SelectedDirective = SearchResults.FirstOrDefault();
        SearchResultCount = $"{results.Count:N0} matching entries · offline snapshot {_catalog.GeneratedAtUtc[..10]}";
    }

    private void Regenerate()
    {
        ExportStatus = string.Empty;
        var options = new StaticSiteOptions
        {
            Engine = SelectedEngine,
            TargetPlatform = SelectedTargetPlatform,
            Domain = (Domain ?? string.Empty).Trim(),
            StaticDirectory = (StaticDirectory ?? string.Empty).Trim(),
            UrlPath = (UrlPath ?? string.Empty).Trim(),
            ListenPort = ListenPort,
            IndexFiles = (IndexFiles ?? string.Empty).Split([' ', ',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            SpaFallback = SpaFallback,
            DirectoryListing = DirectoryListing,
            CacheStaticAssets = CacheStaticAssets,
            Compression = Compression
        };

        var errors = StaticSiteValidator.Validate(options);
        ValidationMessage = string.Join(Environment.NewLine, errors);

        if (errors.Count > 0)
        {
            Preview = "# Fix validation errors before generation.";
        }
        else
        {
            Preview = SelectedEngine switch
            {
                ServerEngine.Nginx => NginxStaticSiteGenerator.Generate(options),
                ServerEngine.Apache => ApacheStaticSiteGenerator.Generate(options),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        OnPropertyChanged(nameof(CanExport));
    }
}
