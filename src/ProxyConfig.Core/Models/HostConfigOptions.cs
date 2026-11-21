namespace ProxyConfig.Core.Models;

public enum HostConfigMode
{
    ReverseProxy,
    Redirect
}

public sealed record HttpHeader(string Name, string Value);

/// <summary>
/// A single server/virtual-host preset. Config is a snippet to include in
/// nginx's http context or Apache's server config, not a full nginx.conf.
/// </summary>
public sealed record HostConfigOptions
{
    public required ServerEngine Engine { get; init; }
    public HostConfigMode Mode { get; init; } = HostConfigMode.ReverseProxy;
    public TargetPlatform TargetPlatform { get; init; } = TargetPlatform.Linux;
    public required string Domain { get; init; }
    public int ListenPort { get; init; } = 80;
    public bool EnableTls { get; init; }
    public string CertificateFile { get; init; } = string.Empty;
    public string PrivateKeyFile { get; init; } = string.Empty;

    public string UrlPath { get; init; } = "/";
    public string UpstreamUrl { get; init; } = "http://127.0.0.1:3000";
    public bool StripPrefix { get; init; } = true;
    public bool PreserveHost { get; init; } = true;
    public bool WebSocket { get; init; }
    public IReadOnlyList<HttpHeader> RequestHeaders { get; init; } = [];
    public IReadOnlyList<HttpHeader> ResponseHeaders { get; init; } = [];

    public string RedirectDestination { get; init; } = "https://example.org";
    public int RedirectStatus { get; init; } = 301;
}
