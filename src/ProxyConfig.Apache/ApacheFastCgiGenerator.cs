using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Apache;

public static class ApacheFastCgiGenerator
{
    /// <summary>The imported VirtualHost keeps its existing DocumentRoot.</summary>
    public static string GenerateHandlerRoute(FastCgiOptions options)
    {
        var generated = ConfigSyntaxDocument.Parse(ServerEngine.Apache, Generate(options));
        var handler = generated.VirtualHosts.Single().Children.Single(n =>
            n.Kind == ConfigNodeKind.Block && n.Name.Equals("FilesMatch", StringComparison.OrdinalIgnoreCase));
        return generated.GetText(handler).Trim();
    }

    public static string Generate(FastCgiOptions options)
    {
        if (options.Engine != ServerEngine.Apache)
            throw new ArgumentException("Apache options required.");
        var errors = FastCgiValidator.Validate(options);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));

        SafeTcpEndpoint.TryNormalize(options.FpmEndpoint, out var endpoint);
        var root = options.TargetPlatform == TargetPlatform.Windows
            ? options.DocumentRoot.Replace('\\', '/') : options.DocumentRoot;
        var escaped = root.Replace("\"", "\\\"");
        var sb = new StringBuilder();
        sb.AppendLine("# Requires mod_proxy and mod_proxy_fcgi, Apache 2.4.10+.");
        sb.AppendLine($"<VirtualHost *:{options.ListenPort}>");
        sb.AppendLine($"    ServerName {options.Domain}");
        sb.AppendLine($"    DocumentRoot \"{escaped}\"");
        sb.AppendLine($"    <Directory \"{escaped}\">");
        sb.AppendLine("        Require all granted");
        sb.AppendLine($"        DirectoryIndex {string.Join(' ', options.IndexFiles)}");
        sb.AppendLine("    </Directory>");
        sb.AppendLine(@"    <FilesMatch ""\.php$"">");
        sb.AppendLine($"        SetHandler \"proxy:fcgi://{endpoint}\"");
        sb.AppendLine("    </FilesMatch>");
        sb.AppendLine("</VirtualHost>");
        return sb.ToString();
    }
}
