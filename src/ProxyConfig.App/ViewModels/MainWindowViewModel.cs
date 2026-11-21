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
    private enum EditorPage { StaticSite, ReverseProxy, Redirect, Explorer }

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
    private EditorPage _page;
    private string _upstreamUrl = "http://127.0.0.1:3000";
    private string _proxyUrlPath = "/";
    private bool _stripPrefix = true;
    private bool _preserveHost = true;
    private bool _webSocket;
    private bool _enableTls;
    private string _certificateFile = string.Empty;
    private string _privateKeyFile = string.Empty;
    private string _redirectDestination = "https://example.org";
    private int _redirectStatus = 301;
    private string _requestHeadersText = string.Empty;
    private string _responseHeadersText = string.Empty;
    private string _searchText = string.Empty;
    private bool _includeVariables;
    private DocumentedDirective? _selectedDirective;
    private string _searchResultCount = string.Empty;
    private string _preview = string.Empty;
    private string _validationMessage = string.Empty;
    private string _exportStatus = string.Empty;

    public MainWindowViewModel()
    {
        ShowStaticSiteCommand = new RelayCommand(() => SelectPage(EditorPage.StaticSite));
        ShowReverseProxyCommand = new RelayCommand(() => SelectPage(EditorPage.ReverseProxy));
        ShowRedirectCommand = new RelayCommand(() => SelectPage(EditorPage.Redirect));
        ShowExplorerCommand = new RelayCommand(() => SelectPage(EditorPage.Explorer));
        RefreshExplorer();
        Regenerate();
    }

    public IRelayCommand ShowStaticSiteCommand { get; }
    public IRelayCommand ShowReverseProxyCommand { get; }
    public IRelayCommand ShowRedirectCommand { get; }
    public IRelayCommand ShowExplorerCommand { get; }
    public ObservableCollection<DocumentedDirective> SearchResults { get; } = [];
    public IReadOnlyList<ServerEngine> ServerEngines { get; } = Enum.GetValues<ServerEngine>();
    public IReadOnlyList<TargetPlatform> TargetPlatforms { get; } = Enum.GetValues<TargetPlatform>();
    public IReadOnlyList<int> RedirectStatuses { get; } = [301, 302, 307, 308];

    public bool IsExplorer => _page == EditorPage.Explorer;
    public bool IsStaticSite => _page == EditorPage.StaticSite;
    public bool IsReverseProxy => _page == EditorPage.ReverseProxy;
    public bool IsRedirect => _page == EditorPage.Redirect;
    public bool IsHostPreset => IsReverseProxy || IsRedirect;
    public bool IsGenerator => !IsExplorer;

    public string SuggestedFileName =>
        $"{(SelectedEngine == ServerEngine.Nginx ? "nginx" : "apache")}-" +
        (_page == EditorPage.ReverseProxy ? "proxy" :
         _page == EditorPage.Redirect ? "redirect" : "site") + ".conf";

    public string UpstreamUrl
    {
        get => _upstreamUrl;
        set { if (SetProperty(ref _upstreamUrl, value)) Regenerate(); }
    }

    public string ProxyUrlPath
    {
        get => _proxyUrlPath;
        set { if (SetProperty(ref _proxyUrlPath, value)) Regenerate(); }
    }

    public bool StripPrefix
    {
        get => _stripPrefix;
        set { if (SetProperty(ref _stripPrefix, value)) Regenerate(); }
    }

    public bool PreserveHost
    {
        get => _preserveHost;
        set { if (SetProperty(ref _preserveHost, value)) Regenerate(); }
    }

    public bool WebSocket
    {
        get => _webSocket;
        set { if (SetProperty(ref _webSocket, value)) Regenerate(); }
    }

    public bool EnableTls
    {
        get => _enableTls;
        set { if (SetProperty(ref _enableTls, value)) Regenerate(); }
    }

    public string CertificateFile
    {
        get => _certificateFile;
        set { if (SetProperty(ref _certificateFile, value)) Regenerate(); }
    }

    public string PrivateKeyFile
    {
        get => _privateKeyFile;
        set { if (SetProperty(ref _privateKeyFile, value)) Regenerate(); }
    }

    public string RedirectDestination
    {
        get => _redirectDestination;
        set { if (SetProperty(ref _redirectDestination, value)) Regenerate(); }
    }

    public int RedirectStatus
    {
        get => _redirectStatus;
        set { if (SetProperty(ref _redirectStatus, value)) Regenerate(); }
    }

    public string RequestHeadersText
    {
        get => _requestHeadersText;
        set { if (SetProperty(ref _requestHeadersText, value)) Regenerate(); }
    }

    public string ResponseHeadersText
    {
        get => _responseHeadersText;
        set { if (SetProperty(ref _responseHeadersText, value)) Regenerate(); }
    }

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
                OnPropertyChanged(nameof(SuggestedFileName));
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

    private void SelectPage(EditorPage page)
    {
        if (_page == page)
            return;
        _page = page;
        OnPropertyChanged(nameof(IsExplorer));
        OnPropertyChanged(nameof(IsStaticSite));
        OnPropertyChanged(nameof(IsReverseProxy));
        OnPropertyChanged(nameof(IsRedirect));
        OnPropertyChanged(nameof(IsHostPreset));
        OnPropertyChanged(nameof(IsGenerator));
        OnPropertyChanged(nameof(SuggestedFileName));
        Regenerate();
    }

    private void Regenerate()
    {
        ExportStatus = string.Empty;
        if (IsHostPreset)
        {
            RegenerateHost();
            return;
        }

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

    private void RegenerateHost()
    {
        var requestHeaders = HttpHeaderParser.Parse(
            IsReverseProxy ? RequestHeadersText : string.Empty, out var requestErrors);
        var responseHeaders = HttpHeaderParser.Parse(ResponseHeadersText, out var responseErrors);
        var options = new HostConfigOptions
        {
            Engine = SelectedEngine,
            Mode = IsReverseProxy ? HostConfigMode.ReverseProxy : HostConfigMode.Redirect,
            TargetPlatform = SelectedTargetPlatform,
            Domain = (Domain ?? string.Empty).Trim(),
            ListenPort = ListenPort,
            EnableTls = EnableTls,
            CertificateFile = (CertificateFile ?? string.Empty).Trim(),
            PrivateKeyFile = (PrivateKeyFile ?? string.Empty).Trim(),
            UrlPath = (ProxyUrlPath ?? string.Empty).Trim(),
            UpstreamUrl = (UpstreamUrl ?? string.Empty).Trim(),
            StripPrefix = StripPrefix,
            PreserveHost = PreserveHost,
            WebSocket = WebSocket,
            RequestHeaders = requestHeaders,
            ResponseHeaders = responseHeaders,
            RedirectDestination = (RedirectDestination ?? string.Empty).Trim(),
            RedirectStatus = RedirectStatus
        };

        var errors = requestErrors.Concat(responseErrors).Concat(HostConfigValidator.Validate(options)).ToList();
        ValidationMessage = string.Join(Environment.NewLine, errors);
        Preview = errors.Count != 0
            ? "# Fix validation errors before generation."
            : SelectedEngine == ServerEngine.Nginx
                ? NginxHostGenerator.Generate(options)
                : ApacheHostGenerator.Generate(options);

        OnPropertyChanged(nameof(CanExport));
    }
}
