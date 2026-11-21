using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Apache;

public static class ApacheLoadBalancerGenerator
{
    public static string Generate(LoadBalancerOptions options)
    {
        if (options.Engine != ServerEngine.Apache)
            throw new ArgumentException("Apache generator requires Apache options.");
        var errors = LoadBalancerValidator.Validate(options);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));

        var mount = options.UrlPath == "/" ? "/" : options.UrlPath.TrimEnd('/') + "/";
        var targetPath = options.StripPrefix ? "/" : mount;
        var cluster = "balancer://" + options.ClusterName;
        var sb = new StringBuilder();
        sb.AppendLine("# Requires mod_proxy, mod_proxy_http, mod_proxy_balancer and mod_lbmethod_byrequests.");
        sb.AppendLine($"<VirtualHost *:{options.ListenPort}>");
        sb.AppendLine($"    ServerName {options.Domain}");
        sb.AppendLine($"    ProxyPreserveHost {(options.PreserveHost ? "On" : "Off")}");
        sb.AppendLine($"    <Proxy \"{cluster}\">");
        foreach (var backend in options.BackendOrigins)
        {
            HostConfigValidator.TryGetOrigin(backend, out var origin);
            sb.AppendLine($"        BalancerMember \"{origin}\"");
        }
        sb.AppendLine("        ProxySet lbmethod=byrequests");
        sb.AppendLine("    </Proxy>");
        sb.AppendLine($"    ProxyPass \"{mount}\" \"{cluster}{targetPath}\"");
        sb.AppendLine($"    ProxyPassReverse \"{mount}\" \"{cluster}{targetPath}\"");
        sb.AppendLine("</VirtualHost>");
        return sb.ToString();
    }
}
