namespace ProxyConfig.Core.Models;

/// <summary>
/// Explicit address ranges of reverse proxies allowed to supply client IP
/// identity via X-Forwarded-For. Never trust arbitrary unlisted senders.
/// </summary>
public sealed record TrustedProxyOptions
{
    public IReadOnlyList<string> TrustedProxyAddresses { get; init; } = [];
}
