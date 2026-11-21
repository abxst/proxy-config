using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Registry;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;

namespace ProxyConfig.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private enum EditorPage { StaticSite, ReverseProxy, Redirect, Explorer, Import }

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
    private ConfigSyntaxDocument? _importedDocument;
    private string _importText = string.Empty;
    private string _importFileName = string.Empty;
    private string _importDiagnostics = string.Empty;
    private string _importSummary = string.Empty;
    private ConfigNodeListItem? _selectedImportNode;
    private string _nodeReplacementText = string.Empty;
    private string _lastGeneratedText = string.Empty;
    private ServerEngine _lastGeneratedEngine;
    private string _searchText = string.Empty;
    private bool _includeVariables;
    private DocumentedDirective? _selectedDirective;
    private string _searchResultCount = string.Empty;
    private string _directiveSearchText = string.Empty;
    private DocumentedDirective? _selectedSuggestion;
    private string _newDirectiveName = string.Empty;
    private string _newDirectiveArguments = string.Empty;
    private string _newBlockName = "location";
    private string _newBlockArguments = "/";
    private string _directiveHint = string.Empty;
    private string _editorScopeContext = "main";
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private string _preview = string.Empty;
    private string _validationMessage = string.Empty;
    private string _exportStatus = string.Empty;

    public MainWindowViewModel()
    {
        ShowStaticSiteCommand = new RelayCommand(() => SelectPage(EditorPage.StaticSite));
        ShowReverseProxyCommand = new RelayCommand(() => SelectPage(EditorPage.ReverseProxy));
        ShowRedirectCommand = new RelayCommand(() => SelectPage(EditorPage.Redirect));
        ShowExplorerCommand = new RelayCommand(() => SelectPage(EditorPage.Explorer));
        ShowImportCommand = new RelayCommand(() =>
        {
            SelectPage(EditorPage.Import);
            ParseImported();
        });
        ParseImportedCommand = new RelayCommand(ParseImported);
        ApplyNodeEditCommand = new RelayCommand(ApplyNodeEdit);
        AppendGeneratedHostCommand = new RelayCommand(AppendGeneratedHost);
        AddDirectiveCommand = new RelayCommand(AddDirectiveToSelection);
        AddBlockCommand = new RelayCommand(AddBlockToSelection);
        RemoveNodeCommand = new RelayCommand(RemoveSelectedNode);
        SelectRootScopeCommand = new RelayCommand(() => SelectedImportNode = null);
        DuplicateNodeCommand = new RelayCommand(DuplicateSelectedNode);
        MoveNodeUpCommand = new RelayCommand(() => MoveSelectedNode(-1));
        MoveNodeDownCommand = new RelayCommand(() => MoveSelectedNode(1));
        UndoEditCommand = new RelayCommand(UndoEdit);
        RedoEditCommand = new RelayCommand(RedoEdit);
        RefreshExplorer();
        RefreshDirectiveSuggestions();
        Regenerate();
    }

    public IRelayCommand ShowStaticSiteCommand { get; }
    public IRelayCommand ShowReverseProxyCommand { get; }
    public IRelayCommand ShowRedirectCommand { get; }
    public IRelayCommand ShowExplorerCommand { get; }
    public IRelayCommand ShowImportCommand { get; }
    public IRelayCommand ParseImportedCommand { get; }
    public IRelayCommand ApplyNodeEditCommand { get; }
    public IRelayCommand AppendGeneratedHostCommand { get; }
    public IRelayCommand AddDirectiveCommand { get; }
    public IRelayCommand AddBlockCommand { get; }
    public IRelayCommand RemoveNodeCommand { get; }
    public IRelayCommand SelectRootScopeCommand { get; }
    public IRelayCommand DuplicateNodeCommand { get; }
    public IRelayCommand MoveNodeUpCommand { get; }
    public IRelayCommand MoveNodeDownCommand { get; }
    public IRelayCommand UndoEditCommand { get; }
    public IRelayCommand RedoEditCommand { get; }
    public ObservableCollection<ConfigNodeListItem> ImportedNodes { get; } = [];
    public ObservableCollection<DocumentedDirective> SearchResults { get; } = [];
    public ObservableCollection<DocumentedDirective> DirectiveSuggestions { get; } = [];
    public IReadOnlyList<ServerEngine> ServerEngines { get; } = Enum.GetValues<ServerEngine>();
    public IReadOnlyList<TargetPlatform> TargetPlatforms { get; } = Enum.GetValues<TargetPlatform>();
    public IReadOnlyList<int> RedirectStatuses { get; } = [301, 302, 307, 308];

    public bool IsExplorer => _page == EditorPage.Explorer;
    public bool IsStaticSite => _page == EditorPage.StaticSite;
    public bool IsReverseProxy => _page == EditorPage.ReverseProxy;
    public bool IsRedirect => _page == EditorPage.Redirect;
    public bool IsHostPreset => IsReverseProxy || IsRedirect;
    public bool IsImport => _page == EditorPage.Import;
    public bool IsGenerator => !IsExplorer && !IsImport;

    public string SuggestedFileName => IsImport && ImportFileName.Length > 0
        ? ImportFileName
        : $"{(SelectedEngine == ServerEngine.Nginx ? "nginx" : "apache")}-" +
          (_page == EditorPage.ReverseProxy ? "proxy" :
           _page == EditorPage.Redirect ? "redirect" :
           _page == EditorPage.Import ? "imported" : "site") + ".conf";

    public string ImportText
    {
        get => _importText;
        set
        {
            if (SetProperty(ref _importText, value))
            {
                OnPropertyChanged(nameof(CanExport));
                if (_importedDocument is not null && _importedDocument.Text != value)
                    ImportDiagnostics = "Text changed. Press Parse to refresh the structure.";
            }
        }
    }

    public string ImportFileName
    {
        get => _importFileName;
        set
        {
            if (SetProperty(ref _importFileName, value))
                OnPropertyChanged(nameof(SuggestedFileName));
        }
    }

    public string ImportDiagnostics
    {
        get => _importDiagnostics;
        private set => SetProperty(ref _importDiagnostics, value);
    }

    public string ImportSummary
    {
        get => _importSummary;
        private set => SetProperty(ref _importSummary, value);
    }

    public ConfigNodeListItem? SelectedImportNode
    {
        get => _selectedImportNode;
        set
        {
            if (SetProperty(ref _selectedImportNode, value))
            {
                NodeReplacementText = value is not null && _importedDocument is not null
                    ? _importedDocument.GetText(value.Node) : string.Empty;
                EditorScopeContext = _importedDocument is not null
                    ? ConfigBlockEditor.GetContext(_importedDocument, value?.Node) : "main";
            }
        }
    }

    public string NodeReplacementText
    {
        get => _nodeReplacementText;
        set => SetProperty(ref _nodeReplacementText, value);
    }

    public string DirectiveSearchText
    {
        get => _directiveSearchText;
        set { if (SetProperty(ref _directiveSearchText, value)) RefreshDirectiveSuggestions(); }
    }

    public DocumentedDirective? SelectedSuggestion
    {
        get => _selectedSuggestion;
        set
        {
            if (!SetProperty(ref _selectedSuggestion, value) || value is null)
                return;
            NewDirectiveName = value.Name;
            DirectiveHint = $"Syntax: {value.Syntax}\nContext: {value.Context}\nModule: {value.Module}\n" +
                "Reference only: engine version, available module and context are not verified.";
        }
    }

    public string NewDirectiveName
    {
        get => _newDirectiveName;
        set => SetProperty(ref _newDirectiveName, value);
    }

    public string NewDirectiveArguments
    {
        get => _newDirectiveArguments;
        set => SetProperty(ref _newDirectiveArguments, value);
    }

    public string NewBlockName
    {
        get => _newBlockName;
        set => SetProperty(ref _newBlockName, value);
    }

    public string NewBlockArguments
    {
        get => _newBlockArguments;
        set => SetProperty(ref _newBlockArguments, value);
    }

    public string DirectiveHint
    {
        get => _directiveHint;
        private set => SetProperty(ref _directiveHint, value);
    }

    public string EditorScopeContext
    {
        get => _editorScopeContext;
        private set => SetProperty(ref _editorScopeContext, value);
    }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public bool CanAppendGeneratedHost =>
        _lastGeneratedText.Length > 0 && _lastGeneratedEngine == SelectedEngine &&
        string.IsNullOrEmpty(ValidationMessage);

    public string ExportText => IsImport ? ImportText : Preview;

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
                _undo.Clear();
                _redo.Clear();
                NotifyHistory();
                NewBlockName = value == ServerEngine.Nginx ? "location" : "VirtualHost";
                NewBlockArguments = value == ServerEngine.Nginx ? "/" : "*:80";
                Regenerate();
                RefreshExplorer();
                RefreshDirectiveSuggestions();
                if (IsImport) ParseImported();
                OnPropertyChanged(nameof(SuggestedFileName));
                OnPropertyChanged(nameof(CanAppendGeneratedHost));
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

    public bool CanExport => IsImport
        ? !string.IsNullOrWhiteSpace(ImportText)
        : string.IsNullOrEmpty(ValidationMessage) && !string.IsNullOrWhiteSpace(Preview);

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

    private void RefreshDirectiveSuggestions()
    {
        var server = SelectedEngine == ServerEngine.Nginx ? "nginx" : "apache";
        var results = _catalog.Search(server, DirectiveSearchText).Take(100);
        DirectiveSuggestions.Clear();
        foreach (var entry in results)
            DirectiveSuggestions.Add(entry);
        SelectedSuggestion = DirectiveSuggestions.FirstOrDefault();
        if (SelectedSuggestion is null)
            DirectiveHint = "No matching catalog entry. You can still enter a directive name manually.";
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
        OnPropertyChanged(nameof(IsImport));
        OnPropertyChanged(nameof(SuggestedFileName));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanAppendGeneratedHost));
        Regenerate();
    }

    private void Regenerate()
    {
        if (IsImport || IsExplorer)
            return;

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
            RememberGeneratedPreview();
        }

        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanAppendGeneratedHost));
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
        if (errors.Count == 0)
            RememberGeneratedPreview();

        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanAppendGeneratedHost));
    }

    private void RememberGeneratedPreview()
    {
        _lastGeneratedText = Preview;
        _lastGeneratedEngine = SelectedEngine;
        OnPropertyChanged(nameof(CanAppendGeneratedHost));
    }

    public void ParseImported() => ReparseImported(recordHistory: true);

    private void ReparseImported(bool recordHistory)
    {
        if (recordHistory && _importedDocument is not null && _importedDocument.Text != ImportText)
        {
            _undo.Push(_importedDocument.Text);
            _redo.Clear();
            NotifyHistory();
        }

        _importedDocument = ConfigSyntaxDocument.Parse(SelectedEngine, ImportText);
        SelectedImportNode = null;
        ImportedNodes.Clear();

        void AppendNodes(IEnumerable<ConfigSyntaxNode> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                if (node.Kind == ConfigNodeKind.Trivia)
                    continue;
                ImportedNodes.Add(new ConfigNodeListItem(node, depth));
                AppendNodes(node.Children, depth + 1);
            }
        }

        AppendNodes(_importedDocument.Nodes, 0);
        SelectedImportNode = ImportedNodes.FirstOrDefault();
        ImportSummary = $"{_importedDocument.VirtualHosts.Count} Virtual Hosts · " +
            $"{ImportedNodes.Count} syntax nodes · " +
            $"{_importedDocument.Diagnostics.Count} diagnostics";
        ImportDiagnostics = string.Join(Environment.NewLine,
            _importedDocument.Diagnostics.Take(8).Select(d => $"At {d.Offset}: {d.Message}"));
    }

    private void NotifyHistory()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private bool IsImportSnapshotCurrent()
    {
        if (_importedDocument is null)
            ParseImported();
        if (_importedDocument!.Text == ImportText)
            return true;
        ImportDiagnostics = "Press Parse before editing a selected node.";
        return false;
    }

    private void ApplyEditorOperation(Func<ConfigSyntaxDocument, ConfigSyntaxDocument> operation)
    {
        if (!IsImportSnapshotCurrent())
            return;
        try
        {
            ImportText = operation(_importedDocument!).Text;
            ParseImported();
        }
        catch (ArgumentException error)
        {
            ImportDiagnostics = error.Message;
        }
        catch (InvalidOperationException error)
        {
            ImportDiagnostics = error.Message;
        }
    }

    private ConfigSyntaxNode? InsertionScope(ConfigSyntaxDocument doc) =>
        ConfigBlockEditor.GetInsertionScope(doc, SelectedImportNode?.Node);

    private void AddDirectiveToSelection() =>
        ApplyEditorOperation(doc => ConfigBlockEditor.AddDirective(
            doc, InsertionScope(doc), NewDirectiveName, NewDirectiveArguments));

    private void AddBlockToSelection() =>
        ApplyEditorOperation(doc => ConfigBlockEditor.AddBlock(
            doc, InsertionScope(doc), NewBlockName, NewBlockArguments));

    private void RemoveSelectedNode()
    {
        if (SelectedImportNode?.Node is { } selected)
            ApplyEditorOperation(doc => ConfigBlockEditor.Remove(doc, selected));
    }

    private void DuplicateSelectedNode()
    {
        if (SelectedImportNode?.Node is { } selected)
            ApplyEditorOperation(doc => ConfigBlockEditor.Duplicate(doc, selected));
    }

    private void MoveSelectedNode(int direction)
    {
        if (SelectedImportNode?.Node is { } selected)
            ApplyEditorOperation(doc => ConfigBlockEditor.Move(doc, selected, direction));
    }

    private void UndoEdit()
    {
        if (!_undo.TryPop(out var previous))
            return;
        _redo.Push(ImportText);
        ImportText = previous;
        ReparseImported(recordHistory: false);
        NotifyHistory();
    }

    private void RedoEdit()
    {
        if (!_redo.TryPop(out var next))
            return;
        _undo.Push(ImportText);
        ImportText = next;
        ReparseImported(recordHistory: false);
        NotifyHistory();
    }

    private void ApplyNodeEdit()
    {
        if (_importedDocument is null || SelectedImportNode is null)
            return;
        if (_importedDocument.Text != ImportText)
        {
            ImportDiagnostics = "Press Parse before editing a selected node.";
            return;
        }

        var updated = _importedDocument.ReplaceNode(SelectedImportNode.Node, NodeReplacementText);
        ImportText = updated.Text;
        ParseImported();
    }

    private void AppendGeneratedHost()
    {
        if (!CanAppendGeneratedHost)
        {
            ImportDiagnostics = "Generate a valid preset for the selected server first.";
            return;
        }
        var current = ConfigSyntaxDocument.Parse(SelectedEngine, ImportText);
        ImportText = current.AppendVirtualHost(_lastGeneratedText).Text;
        ParseImported();
    }
}
