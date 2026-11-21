using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;

namespace ProxyConfig.Core.Validation;

/// <summary>
/// Conservative, offline, cross-file project diagnostics. Only exact HTTP
/// listener/name collisions are reported; wildcard matching, regex routing,
/// DNS resolution and installed module behavior require the target server.
/// </summary>
public static class ConfigProjectAnalyzer
{
    public sealed record Finding(
        string File, int Line, string Code, string Message,
        string? RelatedFile = null, int RelatedLine = 0)
    {
        public string Display => $"{File}:{(Line == 0 ? "?" : Line.ToString())}: " +
            $"{Message}" + (RelatedFile is null ? string.Empty :
                $" (also {RelatedFile}:{RelatedLine})");
    }

    private sealed record Binding(string Endpoint, string Name, int Start);
    private sealed record Origin(string File, int Line, int Offset);

    public static IReadOnlyList<Finding> Analyze(
        ConfigWorkspace workspace, ServerEngine engine, string rootFile, string? apacheServerRoot = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        var graph = workspace.AnalyzeIncludes(engine, rootFile, apacheServerRoot);
        var issues = new List<Finding>();
        foreach (var issue in graph.Issues)
            issues.Add(new Finding(issue.File, 0, "include", issue.Message));

        var seen = new Dictionary<string, Origin>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in graph.VisitedFiles)
        {
            if (!workspace.TryRead(file, out var source))
                continue;
            var doc = ConfigSyntaxDocument.Parse(engine, source);
            foreach (var diagnostic in doc.Diagnostics)
                issues.Add(new Finding(file, LineAt(source, diagnostic.Offset),
                    "syntax", diagnostic.Message));
            foreach (var binding in engine == ServerEngine.Nginx
                         ? NginxHttpBindings(doc, file) : ApacheBindings(doc))
            {
                // Exact binding collisions only; different local IP addresses
                // on one port may serve different hosts, so keep endpoint.
                var key = binding.Endpoint + "\0" + binding.Name;
                var line = LineAt(source, binding.Start);
                if (seen.TryGetValue(key, out var previous))
                {
                    if (previous.File == file && previous.Offset == binding.Start)
                        continue;
                    issues.Add(new Finding(file, line, "duplicate-host",
                        $"HTTP hostname '{binding.Name}' appears more than once on {binding.Endpoint}.",
                        previous.File, previous.Line));
                }
                else
                {
                    seen.Add(key, new Origin(file, line, binding.Start));
                }
            }
        }
        return issues.Take(256).ToArray();
    }

    private static IEnumerable<Binding> NginxHttpBindings(ConfigSyntaxDocument doc, string file)
    {
        // A root-level server snippet is assumed to be an HTTP include.
        // Stream-only generated include files are explicitly excluded.
        if (file.Replace('\\', '/').StartsWith("stream.d/", StringComparison.OrdinalIgnoreCase))
            yield break;

        IEnumerable<ConfigSyntaxNode> Servers(IEnumerable<ConfigSyntaxNode> nodes, bool isStream)
        {
            foreach (var node in nodes)
            {
                if (node.Kind != ConfigNodeKind.Block)
                    continue;
                var nestedStream = isStream || node.Name is "stream" or "mail";
                if (node.Name == "server" && !nestedStream)
                    yield return node;
                foreach (var child in Servers(node.Children, nestedStream))
                    yield return child;
            }
        }

        foreach (var host in Servers(doc.Nodes, isStream: false))
        {
            var listeners = host.Children
                .Where(node => node.Kind == ConfigNodeKind.Directive && node.Name == "listen")
                .Select(node => FirstToken(Arguments(doc, node)))
                .Select(NormalizeListener)
                .Where(value => value is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (listeners.Length == 0)
                listeners = ["*:80"];
            var names = host.Children
                .Where(node => node.Kind == ConfigNodeKind.Directive && node.Name == "server_name")
                .SelectMany(node => SplitTokens(Arguments(doc, node)))
                .Where(name => !name.StartsWith('~') &&
                    !name.Contains('$') && !name.Contains('*'))
                .Select(name => name.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
                foreach (var listener in listeners)
                    yield return new Binding(listener, name, host.Start);
        }
    }

    private static IEnumerable<Binding> ApacheBindings(ConfigSyntaxDocument doc)
    {
        foreach (var host in doc.AllNodes.Where(node =>
                     node.Kind == ConfigNodeKind.Block &&
                     node.Name.Equals("VirtualHost", StringComparison.OrdinalIgnoreCase)))
        {
            var opening = doc.Text[host.Start..host.OpenEnd].Trim();
            if (!opening.EndsWith('>') ||
                !opening.StartsWith("<VirtualHost", StringComparison.OrdinalIgnoreCase))
                continue;
            var listeners = SplitTokens(opening["<VirtualHost".Length..^1])
                .Select(NormalizeListener)
                .Where(value => value is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var names = host.Children.Where(node => node.Kind == ConfigNodeKind.Directive &&
                    (node.Name.Equals("ServerName", StringComparison.OrdinalIgnoreCase) ||
                     node.Name.Equals("ServerAlias", StringComparison.OrdinalIgnoreCase)))
                .SelectMany(node => SplitTokens(Arguments(doc, node)))
                .Where(name => !name.Contains('*') && !name.Contains('$'))
                .Select(name => name.ToLowerInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
                foreach (var listener in listeners)
                    yield return new Binding(listener, name, host.Start);
        }
    }

    private static string Arguments(ConfigSyntaxDocument document, ConfigSyntaxNode directive)
    {
        var source = document.GetText(directive).Trim();
        var args = source[directive.Name.Length..].Trim().TrimEnd(';').Trim();
        return args;
    }

    private static string FirstToken(string args) => SplitTokens(args).FirstOrDefault() ?? string.Empty;

    private static string[] SplitTokens(string args) =>
        args.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static string? NormalizeListener(string token)
    {
        token = token.Trim('\'', '"');
        if (token.Length == 0 || token.StartsWith("unix:", StringComparison.OrdinalIgnoreCase))
            return null;
        if (int.TryParse(token, out var portOnly) && portOnly is >= 1 and <= 65535)
            return $"*:{portOnly}";
        var colon = token.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(token[(colon + 1)..], out var port) ||
            port is < 1 or > 65535)
            return null;
        var address = token[..colon];
        return $"{address.ToLowerInvariant()}:{port}";
    }

    private static int LineAt(string source, int offset)
    {
        var line = 1;
        foreach (var ch in source.AsSpan(0, Math.Clamp(offset, 0, source.Length)))
            if (ch == '\n')
                line++;
        return line;
    }
}
