using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Nginx;

public static class NginxHostGenerator
{
    public static string Generate(HostConfigOptions options)
    {
        var errors = HostConfigValidator.Validate(options);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));

        var sb = new StringBuilder();
        sb.AppendLine("# Include this snippet inside nginx's http context.");
        sb.AppendLine("server {");
        sb.AppendLine($"    listen {options.ListenPort}{(options.EnableTls ? " ssl" : "")};");
        sb.AppendLine($"    server_name {options.Domain};");

        if (options.EnableTls)
        {
            sb.AppendLine($"    ssl_certificate {QuotePath(options.CertificateFile, options.TargetPlatform)};");
            sb.AppendLine($"    ssl_certificate_key {QuotePath(options.PrivateKeyFile, options.TargetPlatform)};");
        }

        sb.AppendLine();
        if (options.Mode == HostConfigMode.Redirect)
        {
            HostConfigValidator.TryGetOrigin(options.RedirectDestination, out var destination);
            sb.AppendLine($"    return {options.RedirectStatus} {destination}$request_uri;");
        }
        else
        {
            HostConfigValidator.TryGetOrigin(options.UpstreamUrl, out var upstream);
            var mountPath = NormalizeMountPath(options.UrlPath);
            var proxyPass = options.StripPrefix && mountPath != "/"
                ? upstream + "/"
                : upstream;

            sb.AppendLine($"    location {mountPath} {{");
            sb.AppendLine($"        proxy_pass {proxyPass};");
            if (options.PreserveHost)
                sb.AppendLine("        proxy_set_header Host $host;");
            sb.AppendLine("        proxy_set_header X-Real-IP $remote_addr;");
            sb.AppendLine("        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;");
            sb.AppendLine("        proxy_set_header X-Forwarded-Proto $scheme;");

            if (options.WebSocket)
            {
                sb.AppendLine("        proxy_http_version 1.1;");
                sb.AppendLine("        proxy_set_header Upgrade $http_upgrade;");
                sb.AppendLine("        proxy_set_header Connection \"upgrade\";");
                sb.AppendLine("        proxy_read_timeout 3600s;");
            }

            foreach (var header in options.RequestHeaders)
                sb.AppendLine($"        proxy_set_header {header.Name} {Quote(header.Value)};");

            sb.AppendLine("    }");
        }

        foreach (var header in options.ResponseHeaders)
            sb.AppendLine($"    add_header {header.Name} {Quote(header.Value)} always;");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string NormalizeMountPath(string path) =>
        path == "/" ? "/" : path.TrimEnd('/') + "/";

    private static string QuotePath(string path, TargetPlatform target) =>
        Quote(target == TargetPlatform.Windows ? path.Replace('\\', '/') : path);

    private static string Quote(string input) =>
        "\"" + input.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
