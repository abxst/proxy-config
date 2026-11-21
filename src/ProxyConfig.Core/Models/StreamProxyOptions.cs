namespace ProxyConfig.Core.Models;

public sealed record StreamProxyOptions
{
    public int ListenPort { get; init; } = 9000;
    public string TargetEndpoint { get; init; } = "127.0.0.1:9001";
    public bool Udp { get; init; }
    public int ProxyTimeoutSeconds { get; init; } = 60;
}
