namespace ProxyConfig.Core.Models;

public enum ServerEngine
{
    Nginx,
    Apache
}

public enum TargetPlatform
{
    Linux,
    Windows,
    FreeBsd,
    GenericUnix
}

public sealed record StaticSiteOptions
{
    public required ServerEngine Engine { get; init; }
    public TargetPlatform TargetPlatform { get; init; } = TargetPlatform.Linux;
    public required string Domain { get; init; }
    public required string StaticDirectory { get; init; }
    public string UrlPath { get; init; } = "/";
    public int ListenPort { get; init; } = 80;
    public IReadOnlyList<string> IndexFiles { get; init; } = ["index.html"];
    public bool SpaFallback { get; init; }
    public bool DirectoryListing { get; init; }
    public bool CacheStaticAssets { get; init; }
    public bool Compression { get; init; }
}
