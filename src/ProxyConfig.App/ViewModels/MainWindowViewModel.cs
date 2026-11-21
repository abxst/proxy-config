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
    private enum EditorPage { StaticSite, ReverseProxy, Redirect, LoadBalancer, FastCgi, StreamProxy, Explorer, Import }
    private sealed record CrossFileTransfer(
        WorkspaceFileItem Source, WorkspaceFileItem Destination,
        string BeforeSource, string BeforeDestination,
        string AfterSource, string AfterDestination);

    private readonly DirectiveCatalog _catalog = DirectiveCatalog.LoadEmbedded();
    private WorkspaceFileItem? _selectedWorkspaceFile;
    private WorkspaceFileItem? _selectedRootWorkspaceFile;
    private string _includeSummary = string.Empty;
    private string _apacheServerRoot = string.Empty;
    private string _diffPreview = string.Empty;
    private string _nodeSearchText = string.Empty;
    private string _newWorkspacePath = "nginx.conf";
    private BlockTargetItem? _selectedMoveTarget;
    private WorkspaceFileItem? _targetWorkspaceFile;
    private ConfigSyntaxDocument? _targetWorkspaceDocument;
    private BlockTargetItem? _selectedCrossFileTarget;
    private CrossFileTransfer? _lastTransfer;
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
    private string _backendOriginsText = "http://127.0.0.1:3000\nhttp://127.0.0.1:3001";
    private string _clusterName = "backend_pool";
    private LoadBalancingMethod _balancingMethod;
    private string _fpmEndpoint = "127.0.0.1:9000";
    private string _phpIndexFiles = "index.php index.html";
    private int _streamListenPort = 9000;
    private string _streamTarget = "127.0.0.1:9001";
    private bool _streamUdp;
    private int _streamTimeoutSeconds = 60;
    private string _grpcUrlPath = "/grpc/";
    private string _grpcUpstreamEndpoint = "127.0.0.1:50051";
    private bool _grpcUpstreamTls;
    private int _grpcReadTimeoutSeconds = 120;
    private int _grpcSendTimeoutSeconds = 60;
    private bool _enableHostLogging;
    private bool _writeAccessLog = true;
    private string _accessLogPath = "logs/access.log";
    private string _errorLogPath = "logs/error.log";
    private ErrorLogLevel _errorLogLevel = ErrorLogLevel.Warn;
    private bool _enableIpPolicy;
    private IpAccessMode _ipAccessMode = IpAccessMode.AllowList;
    private string _ipAccessEntriesText = "192.168.1.0/24";
    private string _ipAccessUrlPath = "/";
    private string _proxyUrlPath = "/";
    private bool _stripPrefix = true;
    private bool _preserveHost = true;
    private bool _webSocket;
    private bool _enableTls;
    private TlsProtocolProfile _tlsProtocolProfile = TlsProtocolProfile.Tls12And13;
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
    private EditorPage _lastGeneratedPage;
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
        ShowLoadBalancerCommand = new RelayCommand(() => SelectPage(EditorPage.LoadBalancer));
        ShowFastCgiCommand = new RelayCommand(() => SelectPage(EditorPage.FastCgi));
        ShowStreamProxyCommand = new RelayCommand(() => SelectPage(EditorPage.StreamProxy));
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
        MoveNodeToTargetCommand = new RelayCommand(MoveNodeToTarget);
        AddStaticRouteCommand = new RelayCommand(AddStaticRouteToSelection);
        AddProxyRouteCommand = new RelayCommand(AddProxyRouteToSelection);
        AddPhpHandlerCommand = new RelayCommand(AddPhpHandlerToSelection);
        AddGrpcRouteCommand = new RelayCommand(AddGrpcRouteToSelection);
        AddHostLoggingCommand = new RelayCommand(AddHostLoggingToSelection);
        AddIpPolicyCommand = new RelayCommand(AddIpPolicyToSelection);
        MoveToWorkspaceFileCommand = new RelayCommand(MoveToWorkspaceFile);
        UndoWorkspaceTransferCommand = new RelayCommand(UndoWorkspaceTransfer);
        RefreshExplorer();
        RefreshDirectiveSuggestions();
        Regenerate();
    }

    public IRelayCommand ShowStaticSiteCommand { get; }
    public IRelayCommand ShowReverseProxyCommand { get; }
    public IRelayCommand ShowRedirectCommand { get; }
    public IRelayCommand ShowLoadBalancerCommand { get; }
    public IRelayCommand ShowFastCgiCommand { get; }
    public IRelayCommand ShowStreamProxyCommand { get; }
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
    public IRelayCommand MoveNodeToTargetCommand { get; }
    public IRelayCommand AddStaticRouteCommand { get; }
    public IRelayCommand AddProxyRouteCommand { get; }
    public IRelayCommand AddPhpHandlerCommand { get; }
    public IRelayCommand AddGrpcRouteCommand { get; }
    public IRelayCommand AddHostLoggingCommand { get; }
    public IRelayCommand AddIpPolicyCommand { get; }
    public IRelayCommand MoveToWorkspaceFileCommand { get; }
    public IRelayCommand UndoWorkspaceTransferCommand { get; }
    public ObservableCollection<ConfigNodeListItem> ImportedNodes { get; } = [];
    public ObservableCollection<DocumentedDirective> SearchResults { get; } = [];
    public ObservableCollection<DocumentedDirective> DirectiveSuggestions { get; } = [];
    public ObservableCollection<string> ArgumentChoices { get; } = [];
    public ObservableCollection<ConfigContextAnalyzer.Warning> ContextWarnings { get; } = [];
    public ObservableCollection<ConfigSecurityLinter.Finding> SecurityFindings { get; } = [];
    public ObservableCollection<WorkspaceFileItem> WorkspaceFiles { get; } = [];
    public ObservableCollection<BlockTargetItem> BlockTargets { get; } = [];
    public ObservableCollection<BlockTargetItem> CrossFileBlockTargets { get; } = [];
    public ObservableCollection<string> IncludeEntries { get; } = [];
    public IReadOnlyList<ServerEngine> ServerEngines { get; } = Enum.GetValues<ServerEngine>();
    public IReadOnlyList<TargetPlatform> TargetPlatforms { get; } = Enum.GetValues<TargetPlatform>();
    public IReadOnlyList<int> RedirectStatuses { get; } = [301, 302, 307, 308];
    public IReadOnlyList<TlsProtocolProfile> TlsProtocolProfiles { get; } =
        Enum.GetValues<TlsProtocolProfile>();
    public IReadOnlyList<LoadBalancingMethod> BalancingMethods { get; } =
        Enum.GetValues<LoadBalancingMethod>();
    public IReadOnlyList<ErrorLogLevel> ErrorLogLevels { get; } =
        Enum.GetValues<ErrorLogLevel>();
    public IReadOnlyList<IpAccessMode> IpAccessModes { get; } =
        Enum.GetValues<IpAccessMode>();

    public bool IsExplorer => _page == EditorPage.Explorer;
    public bool IsStaticSite => _page == EditorPage.StaticSite;
    public bool IsReverseProxy => _page == EditorPage.ReverseProxy;
    public bool IsRedirect => _page == EditorPage.Redirect;
    public bool IsLoadBalancer => _page == EditorPage.LoadBalancer;
    public bool IsFastCgi => _page == EditorPage.FastCgi;
    public bool IsStreamProxy => _page == EditorPage.StreamProxy;
    public bool IsHostPreset => IsReverseProxy || IsRedirect;
    public bool IsImport => _page == EditorPage.Import;
    public bool IsGenerator => !IsExplorer && !IsImport;
    public bool IsHttpGenerator => IsStaticSite || IsHostPreset || IsLoadBalancer || IsFastCgi;

    public string SuggestedFileName => IsImport && ImportFileName.Length > 0
        ? ImportFileName.Replace('\\', '/').Split('/').Last()
        : $"{(SelectedEngine == ServerEngine.Nginx ? "nginx" : "apache")}-" +
          (_page == EditorPage.ReverseProxy ? "proxy" :
           _page == EditorPage.Redirect ? "redirect" :
           _page == EditorPage.LoadBalancer ? "balancer" :
           _page == EditorPage.FastCgi ? "php" :
           _page == EditorPage.StreamProxy ? "stream" :
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
    public bool CanUndoWorkspaceTransfer => _lastTransfer is not null;

    public WorkspaceFileItem? TargetWorkspaceFile
    {
        get => _targetWorkspaceFile;
        set
        {
            if (SetProperty(ref _targetWorkspaceFile, value))
                RefreshCrossFileTargets();
        }
    }

    public BlockTargetItem? SelectedCrossFileTarget
    {
        get => _selectedCrossFileTarget;
        set => SetProperty(ref _selectedCrossFileTarget, value);
    }

    public BlockTargetItem? SelectedMoveTarget
    {
        get => _selectedMoveTarget;
        set => SetProperty(ref _selectedMoveTarget, value);
    }

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
                SecurityFindings.Clear();
                BlockTargets.Clear();
                SelectedMoveTarget = null;
                RefreshCrossFileTargets();
                NotifyHistory();
                return;
            }
            _undo = value.Undo;
            _redo = value.Redo;
            _importedDocument = null;
            ImportText = value.Text;
            ImportFileName = value.Path;
            ReparseImported(recordHistory: false);
            if (ReferenceEquals(TargetWorkspaceFile, value))
                TargetWorkspaceFile = WorkspaceFiles.FirstOrDefault(item =>
                    !ReferenceEquals(item, value));
            else
                RefreshCrossFileTargets();
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
        _lastGeneratedPage != EditorPage.StreamProxy &&
        string.IsNullOrEmpty(ValidationMessage);

    public string ExportText => IsImport ? ImportText : Preview;

    public string UpstreamUrl
    {
        get => _upstreamUrl;
        set { if (SetProperty(ref _upstreamUrl, value)) Regenerate(); }
    }

    public string BackendOriginsText
    {
        get => _backendOriginsText;
        set { if (SetProperty(ref _backendOriginsText, value)) Regenerate(); }
    }

    public string ClusterName
    {
        get => _clusterName;
        set { if (SetProperty(ref _clusterName, value)) Regenerate(); }
    }

    public LoadBalancingMethod BalancingMethod
    {
        get => _balancingMethod;
        set { if (SetProperty(ref _balancingMethod, value)) Regenerate(); }
    }

    public string FpmEndpoint
    {
        get => _fpmEndpoint;
        set { if (SetProperty(ref _fpmEndpoint, value)) Regenerate(); }
    }

    public string PhpIndexFiles
    {
        get => _phpIndexFiles;
        set { if (SetProperty(ref _phpIndexFiles, value)) Regenerate(); }
    }

    public int StreamListenPort
    {
        get => _streamListenPort;
        set { if (SetProperty(ref _streamListenPort, value)) Regenerate(); }
    }

    public string StreamTarget
    {
        get => _streamTarget;
        set { if (SetProperty(ref _streamTarget, value)) Regenerate(); }
    }

    public bool StreamUdp
    {
        get => _streamUdp;
        set { if (SetProperty(ref _streamUdp, value)) Regenerate(); }
    }

    public int StreamTimeoutSeconds
    {
        get => _streamTimeoutSeconds;
        set { if (SetProperty(ref _streamTimeoutSeconds, value)) Regenerate(); }
    }

    public string GrpcUrlPath
    {
        get => _grpcUrlPath;
        set => SetProperty(ref _grpcUrlPath, value);
    }

    public string GrpcUpstreamEndpoint
    {
        get => _grpcUpstreamEndpoint;
        set => SetProperty(ref _grpcUpstreamEndpoint, value);
    }

    public bool GrpcUpstreamTls
    {
        get => _grpcUpstreamTls;
        set => SetProperty(ref _grpcUpstreamTls, value);
    }

    public int GrpcReadTimeoutSeconds
    {
        get => _grpcReadTimeoutSeconds;
        set => SetProperty(ref _grpcReadTimeoutSeconds, value);
    }

    public int GrpcSendTimeoutSeconds
    {
        get => _grpcSendTimeoutSeconds;
        set => SetProperty(ref _grpcSendTimeoutSeconds, value);
    }

    public bool EnableHostLogging
    {
        get => _enableHostLogging;
        set { if (SetProperty(ref _enableHostLogging, value)) Regenerate(); }
    }

    public bool WriteAccessLog
    {
        get => _writeAccessLog;
        set { if (SetProperty(ref _writeAccessLog, value)) Regenerate(); }
    }

    public string AccessLogPath
    {
        get => _accessLogPath;
        set { if (SetProperty(ref _accessLogPath, value)) Regenerate(); }
    }

    public string ErrorLogPath
    {
        get => _errorLogPath;
        set { if (SetProperty(ref _errorLogPath, value)) Regenerate(); }
    }

    public ErrorLogLevel SelectedErrorLogLevel
    {
        get => _errorLogLevel;
        set { if (SetProperty(ref _errorLogLevel, value)) Regenerate(); }
    }

    public bool EnableIpPolicy
    {
        get => _enableIpPolicy;
        set { if (SetProperty(ref _enableIpPolicy, value)) Regenerate(); }
    }

    public IpAccessMode SelectedIpAccessMode
    {
        get => _ipAccessMode;
        set { if (SetProperty(ref _ipAccessMode, value)) Regenerate(); }
    }

    public string IpAccessEntriesText
    {
        get => _ipAccessEntriesText;
        set { if (SetProperty(ref _ipAccessEntriesText, value)) Regenerate(); }
    }

    public string IpAccessUrlPath
    {
        get => _ipAccessUrlPath;
        set { if (SetProperty(ref _ipAccessUrlPath, value)) Regenerate(); }
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

    public TlsProtocolProfile TlsProtocolProfile
    {
        get => _tlsProtocolProfile;
        set { if (SetProperty(ref _tlsProtocolProfile, value)) Regenerate(); }
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
                RefreshCrossFileTargets();
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
        OnPropertyChanged(nameof(IsLoadBalancer));
        OnPropertyChanged(nameof(IsFastCgi));
        OnPropertyChanged(nameof(IsStreamProxy));
        OnPropertyChanged(nameof(IsHostPreset));
        OnPropertyChanged(nameof(IsGenerator));
        OnPropertyChanged(nameof(IsHttpGenerator));
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
        if (IsFastCgi)
        {
            RegenerateFastCgi();
            return;
        }
        if (IsStreamProxy)
        {
            RegenerateStreamProxy();
            return;
        }
        if (IsLoadBalancer)
        {
            RegenerateLoadBalancer();
            return;
        }
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
            TlsProtocolProfile = TlsProtocolProfile,
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

    private void RegenerateLoadBalancer()
    {
        var options = new LoadBalancerOptions
        {
            Engine = SelectedEngine,
            Domain = (Domain ?? string.Empty).Trim(),
            ListenPort = ListenPort,
            ClusterName = (ClusterName ?? string.Empty).Trim(),
            UrlPath = (ProxyUrlPath ?? string.Empty).Trim(),
            BackendOrigins = LoadBalancerValidator.ParseBackends(BackendOriginsText),
            Method = BalancingMethod,
            StripPrefix = StripPrefix,
            PreserveHost = PreserveHost
        };
        var errors = LoadBalancerValidator.Validate(options);
        ValidationMessage = string.Join(Environment.NewLine, errors);
        Preview = errors.Count > 0 ? "# Fix validation errors before generation."
            : SelectedEngine == ServerEngine.Nginx
                ? NginxLoadBalancerGenerator.Generate(options)
                : ApacheLoadBalancerGenerator.Generate(options);
        if (errors.Count == 0)
            RememberGeneratedPreview();
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanAppendGeneratedHost));
    }

    private void RegenerateFastCgi()
    {
        var options = new FastCgiOptions
        {
            Engine = SelectedEngine,
            Domain = (Domain ?? string.Empty).Trim(),
            ListenPort = ListenPort,
            TargetPlatform = SelectedTargetPlatform,
            DocumentRoot = (StaticDirectory ?? string.Empty).Trim(),
            FpmEndpoint = (FpmEndpoint ?? string.Empty).Trim(),
            IndexFiles = (PhpIndexFiles ?? string.Empty).Split([' ', ',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };
        var errors = FastCgiValidator.Validate(options);
        ValidationMessage = string.Join(Environment.NewLine, errors);
        Preview = errors.Count != 0 ? "# Fix validation errors before generation."
            : SelectedEngine == ServerEngine.Nginx
                ? NginxFastCgiGenerator.Generate(options)
                : ApacheFastCgiGenerator.Generate(options);
        if (errors.Count == 0)
            RememberGeneratedPreview();
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanAppendGeneratedHost));
    }

    private void RegenerateStreamProxy()
    {
        var options = new StreamProxyOptions
        {
            ListenPort = StreamListenPort,
            TargetEndpoint = (StreamTarget ?? string.Empty).Trim(),
            Udp = StreamUdp,
            ProxyTimeoutSeconds = StreamTimeoutSeconds
        };
        var errors = StreamProxyValidator.Validate(options).ToList();
        if (SelectedEngine != ServerEngine.Nginx)
            errors.Add("Generic TCP/UDP stream proxying is supported only by NGINX in this app.");
        ValidationMessage = string.Join(Environment.NewLine, errors);
        Preview = errors.Count != 0 ? "# Fix validation errors before generation."
            : NginxStreamProxyGenerator.Generate(options);
        if (errors.Count == 0)
            RememberGeneratedPreview();
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanAppendGeneratedHost));
    }

    private void RememberGeneratedPreview()
    {
        if (IsHttpGenerator && (EnableHostLogging || EnableIpPolicy))
        {
            var errors = new List<string>();
            if (EnableHostLogging)
                errors.AddRange(HostLoggingValidator.Validate(BuildLoggingOptions()));
            if (EnableIpPolicy)
                errors.AddRange(IpAccessValidator.Validate(BuildIpOptions()));
            if (errors.Count > 0)
            {
                ValidationMessage = string.Join(Environment.NewLine, errors);
                Preview = "# Fix logging/access policy validation errors before generation.";
                _lastGeneratedText = string.Empty;
                OnPropertyChanged(nameof(CanAppendGeneratedHost));
                return;
            }

            var generated = ConfigSyntaxDocument.Parse(SelectedEngine, Preview);
            var host = generated.VirtualHosts.FirstOrDefault(n =>
                ConfigBlockEditor.IsHttpVirtualHost(generated, n));
            if (host is null || generated.Diagnostics.Count > 0)
            {
                ValidationMessage = "Generated HTTP host could not be identified for logging/access policies.";
                Preview = "# Fix the configuration before generation.";
                _lastGeneratedText = string.Empty;
                OnPropertyChanged(nameof(CanAppendGeneratedHost));
                return;
            }
            if (EnableHostLogging)
            {
                var logging = SelectedEngine == ServerEngine.Nginx
                    ? NginxHostPolicyGenerator.GenerateLogging(BuildLoggingOptions())
                    : ApacheHostPolicyGenerator.GenerateLogging(BuildLoggingOptions());
                generated = ConfigBlockEditor.InsertGeneratedSnippet(generated, host, logging);
                host = generated.VirtualHosts.Single(n =>
                    ConfigBlockEditor.IsHttpVirtualHost(generated, n));
            }
            if (EnableIpPolicy)
            {
                var access = SelectedEngine == ServerEngine.Nginx
                    ? NginxHostPolicyGenerator.GenerateIpAccess(BuildIpOptions())
                    : ApacheHostPolicyGenerator.GenerateIpAccess(BuildIpOptions());
                generated = ConfigBlockEditor.InsertGeneratedSnippet(generated, host, access);
            }
            Preview = generated.Text;
        }
        _lastGeneratedText = Preview;
        _lastGeneratedEngine = SelectedEngine;
        _lastGeneratedPage = _page;
        OnPropertyChanged(nameof(CanAppendGeneratedHost));
    }

    private HostLoggingOptions BuildLoggingOptions() => new()
    {
        AccessLogEnabled = WriteAccessLog,
        AccessLogPath = (AccessLogPath ?? string.Empty).Trim(),
        ErrorLogPath = (ErrorLogPath ?? string.Empty).Trim(),
        ErrorLogLevel = SelectedErrorLogLevel,
        TargetPlatform = SelectedTargetPlatform
    };

    private IpAccessOptions BuildIpOptions() => new()
    {
        Mode = SelectedIpAccessMode,
        UrlPath = (IpAccessUrlPath ?? string.Empty).Trim(),
        Entries = IpAccessValidator.ParseEntries(IpAccessEntriesText)
    };

    public void ImportFiles(IEnumerable<ConfigImportedFile> files)
    {
        var rejected = new List<string>();
        WorkspaceFileItem? firstAdded = null;
        foreach (var entry in files)
        {
            var path = entry.Path;
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
            var file = new WorkspaceFileItem(key, entry.Text, entry.Encoding);
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
        if (TargetWorkspaceFile is null)
            TargetWorkspaceFile = WorkspaceFiles.FirstOrDefault(item =>
                !ReferenceEquals(item, SelectedWorkspaceFile));
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
        ImportFiles([new ConfigImportedFile(path, string.Empty)]);
    }

    private void RemoveSelectedWorkspaceFile()
    {
        if (SelectedWorkspaceFile is not { } target)
            return;
        var next = WorkspaceFiles.FirstOrDefault(file => !ReferenceEquals(file, target));
        SelectedWorkspaceFile = next;
        WorkspaceFiles.Remove(target);
        if (ReferenceEquals(TargetWorkspaceFile, target))
            TargetWorkspaceFile = WorkspaceFiles.FirstOrDefault(item =>
                !ReferenceEquals(item, SelectedWorkspaceFile));
        if (_lastTransfer is { } transfer &&
            (ReferenceEquals(transfer.Source, target) || ReferenceEquals(transfer.Destination, target)))
        {
            _lastTransfer = null;
            OnPropertyChanged(nameof(CanUndoWorkspaceTransfer));
        }
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
                ? ImportText : file.Text, file.Encoding);
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

    private void RefreshCrossFileTargets()
    {
        CrossFileBlockTargets.Clear();
        _targetWorkspaceDocument = null;
        SelectedCrossFileTarget = null;
        if (TargetWorkspaceFile is not { } file ||
            ReferenceEquals(file, SelectedWorkspaceFile))
            return;
        _targetWorkspaceDocument = ConfigSyntaxDocument.Parse(SelectedEngine, file.Text);
        CrossFileBlockTargets.Add(new BlockTargetItem(null, "Root of destination file"));
        foreach (var block in _targetWorkspaceDocument.AllNodes
            .Where(n => n.Kind == ConfigNodeKind.Block && n.IsClosed))
            CrossFileBlockTargets.Add(new BlockTargetItem(block,
                $"{block.Name} @ offset {block.Start}"));
        SelectedCrossFileTarget = CrossFileBlockTargets.FirstOrDefault();
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
        BlockTargets.Clear();
        BlockTargets.Add(new BlockTargetItem(null, "Root / file scope"));
        foreach (var block in _importedDocument.AllNodes.Where(n => n.Kind == ConfigNodeKind.Block))
            BlockTargets.Add(new BlockTargetItem(block, $"{block.Name} @ offset {block.Start}"));
        SelectedMoveTarget = BlockTargets.FirstOrDefault();
        ContextWarnings.Clear();
        foreach (var warning in ConfigContextAnalyzer.Analyze(_importedDocument, _catalog, ImportFileName))
            ContextWarnings.Add(warning);
        SecurityFindings.Clear();
        foreach (var finding in ConfigSecurityLinter.Analyze(_importedDocument))
            SecurityFindings.Add(finding);
        ContextWarningSummary = $"{ContextWarnings.Count} context warning(s), " +
            $"{SecurityFindings.Count} security review hint(s). All are advisory.";
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
            $"{ContextWarnings.Count} context warnings · " +
            $"{SecurityFindings.Count} security hints";
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

    private void MoveNodeToTarget()
    {
        if (SelectedImportNode?.Node is not { } selected || SelectedMoveTarget is null)
            return;
        ApplyEditorOperation(doc => ConfigBlockEditor.MoveToParent(
            doc, selected, SelectedMoveTarget.Node));
    }

    private void MoveToWorkspaceFile()
    {
        if (!IsImportSnapshotCurrent() ||
            SelectedWorkspaceFile is not { } sourceFile ||
            TargetWorkspaceFile is not { } destinationFile ||
            SelectedImportNode?.Node is not { } selected ||
            SelectedCrossFileTarget is null)
            return;
        if (ReferenceEquals(sourceFile, destinationFile))
        {
            ImportDiagnostics = "Use Move to for moves within the same file.";
            return;
        }
        if (_targetWorkspaceDocument is null ||
            _targetWorkspaceDocument.Text != destinationFile.Text)
        {
            RefreshCrossFileTargets();
            ImportDiagnostics = "Destination changed; select the destination block again.";
            return;
        }
        try
        {
            var result = ConfigBlockEditor.MoveBetweenDocuments(
                _importedDocument!, selected, _targetWorkspaceDocument,
                SelectedCrossFileTarget.Node);
            var previousSource = ImportText;
            var previousDestination = destinationFile.Text;
            sourceFile.Undo.Clear();
            sourceFile.Redo.Clear();
            destinationFile.Undo.Clear();
            destinationFile.Redo.Clear();
            destinationFile.Text = result.Destination.Text;
            ImportText = result.Source.Text;
            _lastTransfer = new CrossFileTransfer(sourceFile, destinationFile,
                previousSource, previousDestination, result.Source.Text, result.Destination.Text);
            OnPropertyChanged(nameof(CanUndoWorkspaceTransfer));
            ReparseImported(recordHistory: false);
            RefreshCrossFileTargets();
            NotifyHistory();
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

    private void UndoWorkspaceTransfer()
    {
        if (_lastTransfer is not { } transfer)
            return;
        if (!WorkspaceFiles.Contains(transfer.Source) ||
            !WorkspaceFiles.Contains(transfer.Destination) ||
            transfer.Source.Text != transfer.AfterSource ||
            transfer.Destination.Text != transfer.AfterDestination)
        {
            ImportDiagnostics = "Files changed since transfer. Undo refused to avoid losing newer edits.";
            return;
        }
        transfer.Source.Text = transfer.BeforeSource;
        transfer.Destination.Text = transfer.BeforeDestination;
        transfer.Source.Undo.Clear();
        transfer.Source.Redo.Clear();
        transfer.Destination.Undo.Clear();
        transfer.Destination.Redo.Clear();
        if (ReferenceEquals(SelectedWorkspaceFile, transfer.Source))
            ImportText = transfer.BeforeSource;
        else if (ReferenceEquals(SelectedWorkspaceFile, transfer.Destination))
            ImportText = transfer.BeforeDestination;
        _lastTransfer = null;
        OnPropertyChanged(nameof(CanUndoWorkspaceTransfer));
        ReparseImported(recordHistory: false);
        RefreshCrossFileTargets();
        NotifyHistory();
    }

    private void AddStaticRouteToSelection()
    {
        if (!IsImportSnapshotCurrent())
            return;
        var parent = ConfigBlockEditor.GetInsertionScope(
            _importedDocument!, SelectedImportNode?.Node);
        if (!ConfigBlockEditor.IsHttpVirtualHost(_importedDocument!, parent))
        {
            ImportDiagnostics = "Select an HTTP server / VirtualHost (not an NGINX stream/mail server).";
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
        if (errors.Count > 0)
        {
            ImportDiagnostics = string.Join(Environment.NewLine, errors);
            return;
        }
        var generated = SelectedEngine == ServerEngine.Nginx
            ? NginxStaticSiteGenerator.Generate(options)
            : ApacheStaticSiteGenerator.Generate(options);
        var preset = ConfigSyntaxDocument.Parse(SelectedEngine, generated);
        if (preset.Diagnostics.Count > 0 || preset.VirtualHosts.Count != 1)
        {
            ImportDiagnostics = "Generated static route has a structural parsing issue.";
            return;
        }
        var excluded = SelectedEngine == ServerEngine.Nginx
            ? new[] { "listen", "server_name" } : new[] { "ServerName" };
        var snippets = preset.VirtualHosts[0].Children
            .Where(n => n.Kind is ConfigNodeKind.Directive or ConfigNodeKind.Block or ConfigNodeKind.Comment)
            .Where(n => !excluded.Contains(n.Name, StringComparer.OrdinalIgnoreCase))
            .Select(n => preset.GetText(n).Trim())
            .Where(text => text.Length > 0);
        var routeBody = string.Join(Environment.NewLine, snippets);
        var conflict = ConfigRouteConflictDetector.FindExactDuplicate(
            _importedDocument!, parent, routeBody);
        if (conflict is not null)
        {
            ImportDiagnostics = conflict;
            return;
        }
        ApplyEditorOperation(doc => ConfigBlockEditor.InsertGeneratedSnippet(doc, parent, routeBody));
    }

    private ConfigSyntaxNode? SelectedHttpRouteHost()
    {
        if (!IsImportSnapshotCurrent())
            return null;
        var host = ConfigBlockEditor.GetInsertionScope(
            _importedDocument!, SelectedImportNode?.Node);
        if (ConfigBlockEditor.IsHttpVirtualHost(_importedDocument!, host))
            return host;
        ImportDiagnostics = "Select an existing HTTP server / VirtualHost node first.";
        return null;
    }

    private void AddProxyRouteToSelection()
    {
        var host = SelectedHttpRouteHost();
        if (host is null)
            return;
        var requestHeaders = HttpHeaderParser.Parse(RequestHeadersText, out var requestErrors);
        var responseHeaders = HttpHeaderParser.Parse(ResponseHeadersText, out var responseErrors);
        var options = new HostConfigOptions
        {
            Engine = SelectedEngine,
            Mode = HostConfigMode.ReverseProxy,
            Domain = (Domain ?? string.Empty).Trim(),
            ListenPort = ListenPort,
            TargetPlatform = SelectedTargetPlatform,
            UrlPath = (ProxyUrlPath ?? string.Empty).Trim(),
            UpstreamUrl = (UpstreamUrl ?? string.Empty).Trim(),
            StripPrefix = StripPrefix,
            PreserveHost = PreserveHost,
            WebSocket = WebSocket,
            RequestHeaders = requestHeaders,
            ResponseHeaders = responseHeaders
        };
        var errors = requestErrors.Concat(responseErrors)
            .Concat(HostConfigValidator.Validate(options)).ToList();
        if (errors.Count > 0)
        {
            ImportDiagnostics = string.Join(Environment.NewLine, errors);
            return;
        }
        var snippet = SelectedEngine == ServerEngine.Nginx
            ? NginxHostGenerator.GenerateReverseProxyRoute(options)
            : ApacheHostGenerator.GenerateReverseProxyRoute(options);
        var conflict = ConfigRouteConflictDetector.FindExactDuplicate(
            _importedDocument!, host, snippet);
        if (conflict is not null)
        {
            ImportDiagnostics = conflict;
            return;
        }
        ApplyEditorOperation(doc => ConfigBlockEditor.InsertGeneratedSnippet(doc, host, snippet));
    }

    private void AddPhpHandlerToSelection()
    {
        var host = SelectedHttpRouteHost();
        if (host is null)
            return;
        var options = new FastCgiOptions
        {
            Engine = SelectedEngine,
            Domain = (Domain ?? string.Empty).Trim(),
            ListenPort = ListenPort,
            TargetPlatform = SelectedTargetPlatform,
            DocumentRoot = (StaticDirectory ?? string.Empty).Trim(),
            FpmEndpoint = (FpmEndpoint ?? string.Empty).Trim(),
            IndexFiles = (PhpIndexFiles ?? string.Empty).Split([' ', ',', ';'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        };
        var errors = FastCgiValidator.Validate(options);
        if (errors.Count > 0)
        {
            ImportDiagnostics = string.Join(Environment.NewLine, errors);
            return;
        }
        var snippet = SelectedEngine == ServerEngine.Nginx
            ? NginxFastCgiGenerator.GenerateHandlerRoute(options)
            : ApacheFastCgiGenerator.GenerateHandlerRoute(options);
        var conflict = ConfigRouteConflictDetector.FindExactDuplicate(
            _importedDocument!, host, snippet);
        if (conflict is not null)
        {
            ImportDiagnostics = conflict;
            return;
        }
        ApplyEditorOperation(doc => ConfigBlockEditor.InsertGeneratedSnippet(doc, host, snippet));
    }

    private void AddGrpcRouteToSelection()
    {
        if (SelectedEngine != ServerEngine.Nginx)
        {
            ImportDiagnostics = "This gRPC route generator supports NGINX only.";
            return;
        }
        var host = SelectedHttpRouteHost();
        if (host is null)
            return;
        var options = new GrpcRouteOptions
        {
            UrlPath = (GrpcUrlPath ?? string.Empty).Trim(),
            UpstreamEndpoint = (GrpcUpstreamEndpoint ?? string.Empty).Trim(),
            UpstreamTls = GrpcUpstreamTls,
            ReadTimeoutSeconds = GrpcReadTimeoutSeconds,
            SendTimeoutSeconds = GrpcSendTimeoutSeconds
        };
        var errors = GrpcRouteValidator.Validate(options);
        if (errors.Count > 0)
        {
            ImportDiagnostics = string.Join(Environment.NewLine, errors);
            return;
        }
        var snippet = NginxGrpcRouteGenerator.Generate(options);
        var conflict = ConfigRouteConflictDetector.FindExactDuplicate(
            _importedDocument!, host, snippet);
        if (conflict is not null)
        {
            ImportDiagnostics = conflict;
            return;
        }
        ApplyEditorOperation(doc => ConfigBlockEditor.InsertGeneratedSnippet(doc, host, snippet));
    }

    private void AddHostLoggingToSelection()
    {
        var host = SelectedHttpRouteHost();
        if (host is null)
            return;
        var options = BuildLoggingOptions();
        var errors = HostLoggingValidator.Validate(options);
        if (errors.Count > 0)
        {
            ImportDiagnostics = string.Join(Environment.NewLine, errors);
            return;
        }
        string[] existingNames = SelectedEngine == ServerEngine.Nginx
            ? ["access_log", "error_log"]
            : ["CustomLog", "ErrorLog", "LogLevel"];
        if (host.Children.Any(n => existingNames.Contains(n.Name, StringComparer.OrdinalIgnoreCase)))
        {
            ImportDiagnostics = "This host already has log directives. Edit or remove them before inserting a new logging preset.";
            return;
        }
        var snippet = SelectedEngine == ServerEngine.Nginx
            ? NginxHostPolicyGenerator.GenerateLogging(options)
            : ApacheHostPolicyGenerator.GenerateLogging(options);
        ApplyEditorOperation(doc => ConfigBlockEditor.InsertGeneratedSnippet(doc, host, snippet));
    }

    private void AddIpPolicyToSelection()
    {
        if (!IsImportSnapshotCurrent())
            return;
        var document = _importedDocument!;
        var scope = ConfigBlockEditor.GetInsertionScope(document, SelectedImportNode?.Node);
        var isHost = ConfigBlockEditor.IsHttpVirtualHost(document, scope);
        var isLocation = scope is { Kind: ConfigNodeKind.Block, IsClosed: true } &&
            scope.Name.Equals(SelectedEngine == ServerEngine.Nginx ? "location" : "Location",
                StringComparison.OrdinalIgnoreCase) &&
            document.VirtualHosts.Any(host =>
                ConfigBlockEditor.IsHttpVirtualHost(document, host) &&
                host.DescendantsAndSelf().Any(n => ReferenceEquals(n, scope)));
        if (!isHost && !isLocation)
        {
            ImportDiagnostics = "Select an HTTP server / VirtualHost or a Location block inside it.";
            return;
        }
        var options = BuildIpOptions();
        var errors = IpAccessValidator.Validate(options);
        if (errors.Count > 0)
        {
            ImportDiagnostics = string.Join(Environment.NewLine, errors);
            return;
        }
        var forbidden = SelectedEngine == ServerEngine.Nginx
            ? new[] { "allow", "deny" }
            : new[] { "Require", "RequireAny", "RequireAll", "RequireNone" };
        if (scope!.Children.Any(n => forbidden.Contains(n.Name, StringComparer.OrdinalIgnoreCase)))
        {
            ImportDiagnostics = "This scope already has authorization rules. Edit those rules instead of stacking conflicting policies.";
            return;
        }
        var snippet = SelectedEngine == ServerEngine.Nginx
            ? NginxHostPolicyGenerator.GenerateIpAccess(options)
            : ApacheHostPolicyGenerator.GenerateIpAccess(options, insideLocation: isLocation);
        if (SelectedEngine == ServerEngine.Apache && isHost)
        {
            var conflict = ConfigRouteConflictDetector.FindExactDuplicate(document, scope, snippet);
            if (conflict is not null)
            {
                ImportDiagnostics = "That Apache Location already exists. Select and edit its authorization rules directly.";
                return;
            }
        }
        ApplyEditorOperation(doc => ConfigBlockEditor.InsertGeneratedSnippet(doc, scope, snippet));
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
