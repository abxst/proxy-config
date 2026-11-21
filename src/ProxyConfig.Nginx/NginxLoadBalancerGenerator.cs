using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Nginx;

public static class NginxLoadBalancerGenerator
{
    public static string Generate(LoadBalancerOptions options)
    {
        if (options.Engine != ServerEngine.Nginx)
            throw new ArgumentException("NGINX generator requires NGINX options.");
        var errors = LoadBalancerValidator.Validate(options);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));

        var upstream = options.ClusterName;
        var mount = options.UrlPath == "/" ? "/" : options.UrlPath.TrimEnd('/') + "/";
        var sb = new StringBuilder();
        sb.AppendLine("# Include both blocks inside the nginx http context.");
        sb.AppendLine($"upstream {upstream} {{");
        if (options.Method == LoadBalancingMethod.LeastConnections)
            sb.AppendLine("    least_conn;");
        if (options.Method == LoadBalancingMethod.ClientIpHash)
            sb.AppendLine("    ip_hash;");
        foreach (var backend in options.BackendOrigins)
        {
            HostConfigValidator.TryGetOrigin(backend, out var origin);
            var uri = new Uri(origin);
            var host = uri.HostNameType == UriHostNameType.IPv6
                ? "[" + uri.Host + "]" : uri.Host;
            sb.AppendLine($"    server {host}:{uri.Port};");
        }
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("server {");
        sb.AppendLine($"    listen {options.ListenPort};");
        sb.AppendLine($"    server_name {options.Domain};");
        sb.AppendLine($"    location {mount} {{");
        var trailing = options.StripPrefix && mount != "/" ? "/" : "";
        sb.AppendLine($"        proxy_pass http://{upstream}{trailing};");
        if (options.PreserveHost)
            sb.AppendLine("        proxy_set_header Host $host;");
        sb.AppendLine("        proxy_set_header X-Real-IP $remote_addr;");
        sb.AppendLine("        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;");
        sb.AppendLine("        proxy_set_header X-Forwarded-Proto $scheme;");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
