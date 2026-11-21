using System.Text;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Apache;

public static class ApacheStaticSiteGenerator
{
    public static string Generate(StaticSiteOptions options)
    {
        var sb = new StringBuilder();
        var path = NormalizePath(options.StaticDirectory, options.TargetPlatform);
        var urlPath = NormalizeUrlPath(options.UrlPath);

        sb.AppendLine($"<VirtualHost *:{options.ListenPort}>");
        sb.AppendLine($"    ServerName {options.Domain}");

        if (urlPath == "/")
        {
            sb.AppendLine($"    DocumentRoot \"{Escape(path)}\"");
            sb.AppendLine();
            AppendDirectory(sb, path, options, 4);
        }
        else
        {
            sb.AppendLine($"    Alias \"{urlPath}\" \"{Escape(EnsureTrailingSlash(path))}\"");
            sb.AppendLine();
            AppendDirectory(sb, path, options, 4);
        }

        if (options.Compression)
        {
            sb.AppendLine();
            sb.AppendLine("    # Requires mod_filter and mod_deflate.");
            sb.AppendLine("    AddOutputFilterByType DEFLATE text/html text/plain text/css application/javascript application/json image/svg+xml");
        }

        sb.AppendLine("</VirtualHost>");
        return sb.ToString();
    }

    private static void AppendDirectory(StringBuilder sb, string path, StaticSiteOptions options, int indent)
    {
        var pad = new string(' ', indent);
        sb.AppendLine($"{pad}<Directory \"{Escape(path)}\">");
        sb.AppendLine($"{pad}    Require all granted");
        sb.AppendLine($"{pad}    DirectoryIndex {string.Join(' ', options.IndexFiles)}");

        if (options.DirectoryListing)
            sb.AppendLine($"{pad}    Options +Indexes");

        if (options.SpaFallback)
            sb.AppendLine($"{pad}    FallbackResource /{options.IndexFiles[0]}");

        if (options.CacheStaticAssets)
        {
            sb.AppendLine($"{pad}    # Requires mod_expires.");
            sb.AppendLine($"{pad}    ExpiresActive On");
            sb.AppendLine($"{pad}    ExpiresDefault \"access plus 30 days\"");
        }

        sb.AppendLine($"{pad}</Directory>");
    }

    private static string NormalizePath(string path, TargetPlatform targetPlatform) =>
        targetPlatform == TargetPlatform.Windows ? path.Replace('\\', '/') : path;

    private static string NormalizeUrlPath(string path)
    {
        if (path == "/")
            return path;

        var normalized = path.StartsWith('/') ? path : "/" + path;
        return normalized.EndsWith('/') ? normalized : normalized + "/";
    }

    private static string EnsureTrailingSlash(string path) => path.EndsWith('/') ? path : path + "/";

    private static string Escape(string value) => value.Replace("\"", "\\\"");
}
