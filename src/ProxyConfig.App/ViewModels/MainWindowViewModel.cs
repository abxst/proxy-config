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
    private WorkspaceFileItem? _selectedWorkspaceFile;
    private WorkspaceFileItem? _selectedRootWorkspaceFile;
    private string _includeSummary = string.Empty;
    private string _apacheServerRoot = string.Empty;
    private string _diffPreview = string.Empty;
    private string _nodeSearchText = string.Empty;
    private string _newWorkspacePath = "nginx.conf";
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
    private string _contextWarningSummary = string.Empty;
    private bool _hideIncompatibleDirectives;
    private DirectiveArgumentForm? _argumentForm;
    private string _typedArgumentHint = string.Empty;
    private string? _selectedArgumentChoice;
    private bool _useTypedValidation = true;
    private Stack<string> _undo = new();
    private Stack<string> _redo = new();
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
        RemoveWorkspaceFileCommand = new RelayCommand(RemoveSelectedWorkspaceFile);
        AddWorkspaceFileCommand = new RelayCommand(AddWorkspaceFile);
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
    public IRelayCommand RemoveWorkspaceFileCommand { get; }
    public IRelayCommand AddWorkspaceFileCommand { get; }
    public ObservableCollection<ConfigNodeListItem> ImportedNodes { get; } = [];
    public ObservableCollection<DocumentedDirective> SearchResults { get; } = [];
    public ObservableCollection<DocumentedDirective> DirectiveSuggestions { get; } = [];
    public ObservableCollection<string> ArgumentChoices { get; } = [];
    public ObservableCollection<ConfigContextAnalyzer.Warning> ContextWarnings { get; } = [];
    public ObservableCollection<WorkspaceFileItem> WorkspaceFiles { get; } = [];
    public ObservableCollection<string> IncludeEntries { get; } = [];
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
        ? ImportFileName.Replace('\\', '/').Split('/').Last()
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
                if (_selectedWorkspaceFile is not null)
                    _selectedWorkspaceFile.Text = value;
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

    public bool HasWorkspaceFiles => WorkspaceFiles.Count > 0;

    public WorkspaceFileItem? SelectedWorkspaceFile
    {
        get => _selectedWorkspaceFile;
        set
        {
            if (ReferenceEquals(_selectedWorkspaceFile, value))
                return;
            if (_selectedWorkspaceFile is not null)
            {
                _selectedWorkspaceFile.Text = ImportText;
                if (_importedDocument is not null && _importedDocument.Text != ImportText)
                {
                    _undo.Push(_importedDocument.Text);
                    _redo.Clear();
                }
            }
            _selectedWorkspaceFile = value;
            OnPropertyChanged();
            if (value is null)
            {
                _importedDocument = null;
                _undo = new();
                _redo = new();
                ImportText = string.Empty;
                ImportFileName = string.Empty;
                ImportedNodes.Clear();
                ContextWarnings.Clear();
                NotifyHistory();
                return;
            }
            _undo = value.Undo;
            _redo = value.Redo;
            _importedDocument = null;
            ImportText = value.Text;
            ImportFileName = value.Path;
            ReparseImported(recordHistory: false);
            NotifyHistory();
        }
    }

    public WorkspaceFileItem? SelectedRootWorkspaceFile
    {
        get => _selectedRootWorkspaceFile;
        set
        {
            if (SetProperty(ref _selectedRootWorkspaceFile, value))
                RebuildIncludeGraph();
        }
    }

    public string ApacheServerRoot
    {
        get => _apacheServerRoot;
        set { if (SetProperty(ref _apacheServerRoot, value)) RebuildIncludeGraph(); }
    }

    public string IncludeSummary
    {
        get => _includeSummary;
        private set => SetProperty(ref _includeSummary, value);
    }

    public string DiffPreview
    {
        get => _diffPreview;
        private set => SetProperty(ref _diffPreview, value);
    }

    public string NewWorkspacePath
    {
        get => _newWorkspacePath;
        set => SetProperty(ref _newWorkspacePath, value);
    }

    public string NodeSearchText
    {
        get => _nodeSearchText;
        set
        {
            if (SetProperty(ref _nodeSearchText, value) && _importedDocument is not null)
            {
                RebuildImportedNodes();
                UpdateImportSummary();
            }
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
                RefreshDirectiveSuggestions();
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

    public bool HideIncompatibleDirectives
    {
        get => _hideIncompatibleDirectives;
        set { if (SetProperty(ref _hideIncompatibleDirectives, value)) RefreshDirectiveSuggestions(); }
    }

    public DocumentedDirective? SelectedSuggestion
    {
        get => _selectedSuggestion;
        set
        {
            if (!SetProperty(ref _selectedSuggestion, value) || value is null)
                return;
            NewDirectiveName = value.Name;
            UpdateArgumentForm(value);
            UpdateDirectiveHint();
        }
    }

    public bool HasArgumentChoices => ArgumentChoices.Count > 0;

    public string? SelectedArgumentChoice
    {
        get => _selectedArgumentChoice;
        set
        {
            if (SetProperty(ref _selectedArgumentChoice, value) && value is not null)
                NewDirectiveArguments = value;
        }
    }

    public string TypedArgumentHint
    {
        get => _typedArgumentHint;
        private set => SetProperty(ref _typedArgumentHint, value);
    }

    public bool UseTypedValidation
    {
        get => _useTypedValidation;
        set => SetProperty(ref _useTypedValidation, value);
    }

    public string ContextWarningSummary
    {
        get => _contextWarningSummary;
        private set => SetProperty(ref _contextWarningSummary, value);
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
                NewWorkspacePath = value == ServerEngine.Nginx ? "nginx.conf" : "apache2.conf";
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
        var scope = _importedDocument is not null &&
            _importedDocument.Text == ImportText
            ? ConfigBlockEditor.GetInsertionScope(_importedDocument, SelectedImportNode?.Node)
            : null;
        var results = _catalog.Search(server, DirectiveSearchText)
            .Where(entry => !HideIncompatibleDirectives || _importedDocument is null ||
                ConfigContextAnalyzer.AssessInsertion(_importedDocument, scope, entry, ImportFileName)
                    != ConfigContextAnalyzer.Verdict.Incompatible)
            .Take(100);
        DirectiveSuggestions.Clear();
        foreach (var entry in results)
            DirectiveSuggestions.Add(entry);
        SelectedSuggestion = DirectiveSuggestions.FirstOrDefault();
        if (SelectedSuggestion is null)
            DirectiveHint = "No matching catalog entry. You can still enter a directive name manually.";
        else
            UpdateDirectiveHint();
    }

    private void UpdateDirectiveHint()
    {
        if (SelectedSuggestion is not { } entry)
            return;
        var verdict = _importedDocument is not null &&
            _importedDocument.Text == ImportText
            ? ConfigContextAnalyzer.AssessInsertion(
                _importedDocument,
                ConfigBlockEditor.GetInsertionScope(_importedDocument, SelectedImportNode?.Node),
                entry, ImportFileName)
            : ConfigContextAnalyzer.Verdict.Unknown;
        var advisory = verdict switch
        {
            ConfigContextAnalyzer.Verdict.Compatible => "Documented context matches this scope.",
            ConfigContextAnalyzer.Verdict.Incompatible => "WARNING: documented context does not match selected scope.",
            _ => "Context could not be verified for this scope."
        };
        DirectiveHint = $"{advisory}\nSyntax: {entry.Syntax}\nContext: {entry.Context}\n" +
            $"Module: {entry.Module}\nEngine version and installed modules are not verified.";
    }

    private void UpdateArgumentForm(DocumentedDirective entry)
    {
        _argumentForm = DirectiveArgumentForm.Infer(entry);
        TypedArgumentHint = _argumentForm.Hint;
        ArgumentChoices.Clear();
        foreach (var value in _argumentForm.Choices)
            ArgumentChoices.Add(value);
        OnPropertyChanged(nameof(HasArgumentChoices));
        SelectedArgumentChoice = ArgumentChoices.FirstOrDefault();
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

    public void ImportFiles(IEnumerable<(string Path, string Content)> files)
    {
        var rejected = new List<string>();
        WorkspaceFileItem? firstAdded = null;
        foreach (var (path, content) in files)
        {
            string key;
            try
            {
                key = ConfigWorkspace.NormalizeRelativePath(path);
            }
            catch (ArgumentException)
            {
                rejected.Add(path);
                continue;
            }
            if (WorkspaceFiles.Any(existing => existing.Path.Equals(key, StringComparison.Ordinal)))
            {
                rejected.Add(path + " (duplicate)");
                continue;
            }
            var file = new WorkspaceFileItem(key, content);
            WorkspaceFiles.Add(file);
            firstAdded ??= file;
        }
        if (firstAdded is null)
        {
            ImportDiagnostics = rejected.Count > 0
                ? "No files added: " + string.Join(", ", rejected.Take(5))
                : "No files selected.";
            return;
        }
        OnPropertyChanged(nameof(HasWorkspaceFiles));
        SelectPage(EditorPage.Import);
        SelectedRootWorkspaceFile ??= firstAdded;
        SelectedWorkspaceFile = firstAdded;
        if (rejected.Count > 0)
            ImportDiagnostics = "Skipped duplicate/invalid project paths: " +
                string.Join(", ", rejected.Take(5));
        RebuildIncludeGraph();
    }

    private void AddWorkspaceFile()
    {
        string path;
        try
        {
            path = ConfigWorkspace.NormalizeRelativePath(NewWorkspacePath);
        }
        catch (ArgumentException error)
        {
            ImportDiagnostics = error.Message;
            return;
        }
        if (WorkspaceFiles.Any(item => item.Path.Equals(path, StringComparison.Ordinal)))
        {
            ImportDiagnostics = $"Workspace already contains {path}.";
            return;
        }
        ImportFiles([(path, string.Empty)]);
    }

    private void RemoveSelectedWorkspaceFile()
    {
        if (SelectedWorkspaceFile is not { } target)
            return;
        var next = WorkspaceFiles.FirstOrDefault(file => !ReferenceEquals(file, target));
        SelectedWorkspaceFile = next;
        WorkspaceFiles.Remove(target);
        if (ReferenceEquals(SelectedRootWorkspaceFile, target))
            SelectedRootWorkspaceFile = next;
        OnPropertyChanged(nameof(HasWorkspaceFiles));
        RebuildIncludeGraph();
    }

    public ConfigWorkspace ExportWorkspaceSnapshot()
    {
        var snapshot = new ConfigWorkspace();
        foreach (var file in WorkspaceFiles)
            snapshot.AddOrUpdate(file.Path, ReferenceEquals(file, SelectedWorkspaceFile)
                ? ImportText : file.Text);
        return snapshot;
    }

    private void RebuildIncludeGraph()
    {
        IncludeEntries.Clear();
        if (SelectedRootWorkspaceFile is not { } root || WorkspaceFiles.Count == 0)
        {
            IncludeSummary = "Import multiple files to inspect include relationships.";
            return;
        }
        var graph = ExportWorkspaceSnapshot().AnalyzeIncludes(SelectedEngine,
            root.Path, string.IsNullOrWhiteSpace(ApacheServerRoot) ? null : ApacheServerRoot.Trim());
        IncludeSummary = $"{graph.VisitedFiles.Count} visited file(s), " +
            $"{graph.Edges.Count} include edge(s), {graph.Issues.Count} issue(s).";
        foreach (var edge in graph.Edges)
            IncludeEntries.Add($"{edge.From} → {edge.To}" + (edge.Optional ? " (optional)" : ""));
        foreach (var issue in graph.Issues)
            IncludeEntries.Add($"WARNING {issue.File}: {issue.Message}");
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
        RebuildImportedNodes();
        ContextWarnings.Clear();
        foreach (var warning in ConfigContextAnalyzer.Analyze(_importedDocument, _catalog, ImportFileName))
            ContextWarnings.Add(warning);
        ContextWarningSummary = $"{ContextWarnings.Count} advisory context warning(s). " +
            "Unknown directives are preserved; warnings do not block export.";
        UpdateImportSummary();
        ImportDiagnostics = string.Join(Environment.NewLine,
            _importedDocument.Diagnostics.Take(8).Select(d => $"At {d.Offset}: {d.Message}"));
        DiffPreview = SelectedWorkspaceFile is { } file
            ? ConfigTextDiff.Build(file.OriginalText, ImportText)
            : "Import one or more files to compare changes.";
        RebuildIncludeGraph();
    }

    private void RebuildImportedNodes()
    {
        if (_importedDocument is null)
            return;
        var previous = SelectedImportNode?.Node;
        SelectedImportNode = null;
        ImportedNodes.Clear();

        void AppendNodes(IEnumerable<ConfigSyntaxNode> nodes, int depth)
        {
            foreach (var node in nodes)
            {
                if (node.Kind == ConfigNodeKind.Trivia)
                    continue;
                if (string.IsNullOrWhiteSpace(NodeSearchText) ||
                    node.Name.Contains(NodeSearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    node.Kind.ToString().Contains(NodeSearchText.Trim(), StringComparison.OrdinalIgnoreCase))
                    ImportedNodes.Add(new ConfigNodeListItem(node, depth));
                AppendNodes(node.Children, depth + 1);
            }
        }

        AppendNodes(_importedDocument.Nodes, 0);
        SelectedImportNode = ImportedNodes.FirstOrDefault(item => ReferenceEquals(item.Node, previous))
            ?? ImportedNodes.FirstOrDefault();
    }

    private void UpdateImportSummary()
    {
        if (_importedDocument is null)
            return;
        var total = _importedDocument.AllNodes.Count(n => n.Kind != ConfigNodeKind.Trivia);
        ImportSummary = $"{_importedDocument.VirtualHosts.Count} Virtual Hosts · " +
            $"{ImportedNodes.Count}/{total} nodes shown · " +
            $"{_importedDocument.Diagnostics.Count} parser diagnostics · " +
            $"{ContextWarnings.Count} context warnings";
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

    private void AddDirectiveToSelection()
    {
        if (SelectedSuggestion is { } suggested &&
            suggested.Name.Equals(NewDirectiveName, SelectedEngine == ServerEngine.Nginx
                ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) &&
            _argumentForm is { } form)
        {
            var error = form.Validate(NewDirectiveArguments, ignoreForm: !UseTypedValidation);
            if (error is not null)
            {
                ImportDiagnostics = $"Argument form: {error} Disable typed validation for raw syntax.";
                return;
            }
        }
        ApplyEditorOperation(doc => ConfigBlockEditor.AddDirective(
            doc, InsertionScope(doc), NewDirectiveName, NewDirectiveArguments));
    }

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
