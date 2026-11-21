namespace ProxyConfig.Core.Models;

public enum IpAccessMode
{
    AllowList,
    DenyList
}

/// <summary>IP-only ACL. NGINX applies in selected server/location; Apache in a Location.</summary>
public sealed record IpAccessOptions
{
    public IpAccessMode Mode { get; init; } = IpAccessMode.AllowList;
    public string UrlPath { get; init; } = "/";
    public IReadOnlyList<string> Entries { get; init; } = ["192.168.1.0/24"];
}
