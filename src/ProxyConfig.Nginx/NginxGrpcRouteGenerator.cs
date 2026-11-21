using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Nginx;

/// <summary>
/// NGINX gRPC reverse proxy location. The parent server must already enable
/// HTTP/2 on its frontend listener; this helper does not edit TLS/listen.
/// </summary>
public static class NginxGrpcRouteGenerator
{
    public static string Generate(GrpcRouteOptions options)
    {
        var errors = GrpcRouteValidator.Validate(options);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        SafeTcpEndpoint.TryNormalize(options.UpstreamEndpoint, out var endpoint);
        var sb = new StringBuilder();
        sb.AppendLine($"location {options.UrlPath} {{");
        sb.AppendLine($"    grpc_pass {(options.UpstreamTls ? "grpcs" : "grpc")}://{endpoint};");
        sb.AppendLine($"    grpc_read_timeout {options.ReadTimeoutSeconds}s;");
        sb.AppendLine($"    grpc_send_timeout {options.SendTimeoutSeconds}s;");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
