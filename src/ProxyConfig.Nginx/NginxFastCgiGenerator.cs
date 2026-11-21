using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Nginx;

public static class NginxFastCgiGenerator
{
    /// <summary>Emit an independent PHP location to add inside an imported NGINX server.</summary>
    public static string GenerateHandlerRoute(FastCgiOptions options)
    {
        var generated = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, Generate(options));
        var handler = generated.VirtualHosts.Single().Children.Single(n =>
            n.Kind == ConfigNodeKind.Block && n.Name == "location" &&
            generated.GetText(n).Contains("fastcgi_pass", StringComparison.Ordinal));
        var snippet = generated.GetText(handler).Trim();
        var opening = snippet.IndexOf('{');
        var root = options.TargetPlatform == TargetPlatform.Windows
            ? options.DocumentRoot.Replace('\\', '/') : options.DocumentRoot;
        return snippet.Insert(opening + 1, "\n    root " + Quote(root) + ";");
    }

    public static string Generate(FastCgiOptions options)
    {
        if (options.Engine != ServerEngine.Nginx)
            throw new ArgumentException("NGINX options required.");
        var errors = FastCgiValidator.Validate(options);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));

        SafeTcpEndpoint.TryNormalize(options.FpmEndpoint, out var endpoint);
        var root = options.TargetPlatform == TargetPlatform.Windows
            ? options.DocumentRoot.Replace('\\', '/') : options.DocumentRoot;
        var sb = new StringBuilder();
        sb.AppendLine("# Requires ngx_http_fastcgi_module and an installed fastcgi_params include.");
        sb.AppendLine("# Include this server inside nginx's http context.");
        sb.AppendLine("server {");
        sb.AppendLine($"    listen {options.ListenPort};");
        sb.AppendLine($"    server_name {options.Domain};");
        sb.AppendLine($"    root {Quote(root)};");
        sb.AppendLine($"    index {string.Join(' ', options.IndexFiles)};");
        sb.AppendLine("    location / {");
        sb.AppendLine("        try_files $uri $uri/ =404;");
        sb.AppendLine("    }");
        sb.AppendLine(@"    location ~ \.php$ {");
        sb.AppendLine("        try_files $uri =404;");
        sb.AppendLine("        include fastcgi_params;");
        sb.AppendLine("        fastcgi_param SCRIPT_FILENAME $document_root$fastcgi_script_name;");
        sb.AppendLine($"        fastcgi_pass {endpoint};");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Quote(string path) =>
        "\"" + path.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
