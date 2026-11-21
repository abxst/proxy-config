namespace ProxyConfig.Core.Models;

/// <summary>One NGINX HTTP-context route to a gRPC upstream.</summary>
public sealed record GrpcRouteOptions
{
    public string UrlPath { get; init; } = "/";
    public string UpstreamEndpoint { get; init; } = "127.0.0.1:50051";
    public bool UpstreamTls { get; init; }
    public int ReadTimeoutSeconds { get; init; } = 120;
    public int SendTimeoutSeconds { get; init; } = 60;
}
