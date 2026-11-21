using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Nginx;

public static class NginxStreamProxyGenerator
{
    public static string Generate(StreamProxyOptions options)
    {
        var errors = StreamProxyValidator.Validate(options);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        SafeTcpEndpoint.TryNormalize(options.TargetEndpoint, out var endpoint);
        var sb = new StringBuilder();
        sb.AppendLine("# Requires --with-stream / ngx_stream_proxy_module.");
        sb.AppendLine("# Standalone stream context. Do not include inside an http block.");
        sb.AppendLine("stream {");
        sb.AppendLine("    server {");
        sb.AppendLine($"        listen {options.ListenPort}{(options.Udp ? " udp" : "")};");
        sb.AppendLine($"        proxy_timeout {options.ProxyTimeoutSeconds}s;");
        sb.AppendLine($"        proxy_pass {endpoint};");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
