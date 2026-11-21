using System.Text;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Nginx;

public static class NginxStaticSiteGenerator
{
    public static string Generate(StaticSiteOptions options)
    {
        var sb = new StringBuilder();
        var path = QuoteIfNeeded(options.StaticDirectory);
        var urlPath = NormalizeUrlPath(options.UrlPath);

        sb.AppendLine("server {");
        sb.AppendLine($"    listen {options.ListenPort};");
        sb.AppendLine($"    server_name {options.Domain};");
        sb.AppendLine();

        if (urlPath == "/")
        {
            sb.AppendLine($"    root {path};");
            sb.AppendLine($"    index {string.Join(' ', options.IndexFiles)};");
            sb.AppendLine();

            sb.AppendLine("    location / {");
            if (options.SpaFallback)
                sb.AppendLine($"        try_files $uri $uri/ /{options.IndexFiles[0]};");
            else
                sb.AppendLine("        try_files $uri $uri/ =404;");

            if (options.DirectoryListing)
                sb.AppendLine("        autoindex on;");

            sb.AppendLine("    }");
        }
        else
        {
            sb.AppendLine($"    location {urlPath} {{");
            sb.AppendLine($"        alias {EnsureTrailingSlash(path)};");
            sb.AppendLine($"        index {string.Join(' ', options.IndexFiles)};");
            if (options.DirectoryListing)
                sb.AppendLine("        autoindex on;");
            if (options.CacheStaticAssets)
            {
                sb.AppendLine("        expires 30d;");
                sb.AppendLine("        add_header Cache-Control \"public, immutable\";");
            }
            sb.AppendLine("    }");
        }

        if (options.CacheStaticAssets && urlPath == "/")
        {
            sb.AppendLine();
            sb.AppendLine(@"    location ~* \.(?:css|js|mjs|json|jpg|jpeg|png|gif|svg|webp|ico|woff|woff2|ttf|otf)$ {");
            sb.AppendLine("        expires 30d;");
            sb.AppendLine("        add_header Cache-Control \"public, immutable\";");
            sb.AppendLine("        try_files $uri =404;");
            sb.AppendLine("    }");
        }

        if (options.Compression)
        {
            sb.AppendLine();
            sb.AppendLine("    gzip on;");
            sb.AppendLine("    gzip_types text/plain text/css application/javascript application/json image/svg+xml;");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string NormalizeUrlPath(string path)
    {
        if (path == "/")
            return path;

        var normalized = path.StartsWith('/') ? path : "/" + path;
        return normalized.EndsWith('/') ? normalized : normalized + "/";
    }

    private static string EnsureTrailingSlash(string path)
    {
        if (path.EndsWith('/'))
            return path;

        if (path.EndsWith('"'))
            return path[..^1] + "/\"";

        return path + "/";
    }

    private static string QuoteIfNeeded(string value)
    {
        if (!value.Any(c => char.IsWhiteSpace(c) || c is ';' or '#' or '{' or '}' or '"'))
            return value;

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
