using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Syntax;

/// <summary>
/// Detects exact repeated route selectors before a built-in preset is inserted.
/// Overlapping wildcard/regex routing and module-dependent precedence are not
/// inferred; this narrow check is not a full server routing model.
/// </summary>
public static class ConfigRouteConflictDetector
{
    public static string? FindExactDuplicate(
        ConfigSyntaxDocument document, ConfigSyntaxNode host, string generatedSnippet)
    {
        if (!ConfigBlockEditor.IsHttpVirtualHost(document, host))
            throw new ArgumentException("Select an HTTP VirtualHost/server block.", nameof(host));
        var preset = ConfigSyntaxDocument.Parse(document.Engine, generatedSnippet);
        if (preset.Diagnostics.Count > 0)
            throw new ArgumentException("Generated snippet is structurally invalid.", nameof(generatedSnippet));
        var existing = host.Children.Select(n => Signature(document, n))
            .Where(s => s is not null).ToHashSet(StringComparer.Ordinal);
        foreach (var node in preset.Nodes)
        {
            var signature = Signature(preset, node);
            if (signature is not null && existing.Contains(signature))
                return $"A route with the same selector already exists in this host ({signature}). Review it before inserting a duplicate.";
        }
        return null;
    }

    private static string? Signature(ConfigSyntaxDocument doc, ConfigSyntaxNode node)
    {
        if (node.Kind == ConfigNodeKind.Block)
        {
            var routeBlock = doc.Engine == ServerEngine.Nginx
                ? node.Name == "location"
                : node.Name.Equals("FilesMatch", StringComparison.OrdinalIgnoreCase) ||
                  node.Name.Equals("Location", StringComparison.OrdinalIgnoreCase);
            if (!routeBlock || node.OpenEnd < node.Start || node.OpenEnd > doc.Text.Length)
                return null;
            var header = doc.Text[node.Start..node.OpenEnd].Trim();
            return node.Name.ToLowerInvariant() + "|" +
                Regex.Replace(header, @"\s+", " ").TrimEnd('{', '>', ' ');
        }
        if (doc.Engine != ServerEngine.Apache || node.Kind != ConfigNodeKind.Directive ||
            !(node.Name.Equals("ProxyPass", StringComparison.OrdinalIgnoreCase) ||
              node.Name.Equals("Alias", StringComparison.OrdinalIgnoreCase)))
            return null;
        var source = doc.GetText(node).TrimStart();
        var args = source[node.Name.Length..].TrimStart();
        if (args.Length == 0)
            return null;
        string? first;
        if (args[0] == '"')
        {
            var end = args.IndexOf('"', 1);
            first = end > 1 ? args[1..end] : null;
        }
        else
        {
            first = args.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();
        }
        return first is null ? null : node.Name.ToLowerInvariant() + "|" + first;
    }
}
