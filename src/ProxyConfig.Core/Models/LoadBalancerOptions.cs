namespace ProxyConfig.Core.Models;

public enum LoadBalancingMethod
{
    RoundRobin,
    LeastConnections,
    ClientIpHash
}

public sealed record LoadBalancerOptions
{
    public ServerEngine Engine { get; init; } = ServerEngine.Nginx;
    public string Domain { get; init; } = "example.com";
    public int ListenPort { get; init; } = 80;
    public string ClusterName { get; init; } = "backend_pool";
    public string UrlPath { get; init; } = "/";
    public IReadOnlyList<string> BackendOrigins { get; init; } = [];
    public LoadBalancingMethod Method { get; init; }
    public bool StripPrefix { get; init; } = true;
    public bool PreserveHost { get; init; } = true;
}
