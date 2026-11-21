using ProxyConfig.Core.Models;
using ProxyConfig.Core.Registry;
using ProxyConfig.Core.Syntax;

namespace ProxyConfig.Core.Validation;

/// <summary>
/// Conservative, documentation-backed context advisory. The embedded catalog
/// is not a version/module manifest, so "compatible" is never a guarantee of
/// runtime support. Unknown constructs and ambiguous scopes are not rejected.
/// </summary>
public static class ConfigContextAnalyzer
{
    public enum Verdict { Compatible, Incompatible, Unknown }

    public sealed record Warning(int Offset, int Line, string Name, string Scope, string Expected)
    {
        public string Display => $"Line {Line}: {Name} is documented for [{Expected}], not [{Scope}].";
    }

    public static IReadOnlyList<Warning> Analyze(
        ConfigSyntaxDocument document, DirectiveCatalog catalog, string? fileName = null)
    {
        var server = document.Engine == ServerEngine.Nginx ? "nginx" : "apache";
        var lookup = catalog.Entries.Where(entry => entry.Server == server)
            .ToLookup(entry => document.Engine == ServerEngine.Nginx
                ? entry.Name : entry.Name.ToUpperInvariant(), StringComparer.Ordinal);
        var warnings = new List<Warning>();
        var parents = new List<ConfigSyntaxNode>();

        void Visit(IEnumerable<ConfigSyntaxNode> nodes)
        {
            foreach (var node in nodes)
            {
                if (node.Kind is ConfigNodeKind.Block or ConfigNodeKind.Directive)
                {
                    var key = document.Engine == ServerEngine.Nginx
                        ? node.Name : node.Name.ToUpperInvariant();
                    var candidates = ForNodeKind(lookup[key], document.Engine, node.Kind).ToList();
                    if (candidates.Count > 0)
                    {
                        var scope = ResolveScope(document.Engine, parents, fileName);
                        var verdict = Evaluate(scope, candidates);
                        if (verdict == Verdict.Incompatible)
                        {
                            var expected = string.Join(", ",
                                candidates.SelectMany(e => e.Context.Split(',', StringSplitOptions.TrimEntries))
                                    .Where(c => c.Length > 0)
                                    .Distinct(StringComparer.OrdinalIgnoreCase));
            var line = 1 + document.Text[..node.Start].Count(c => c == '\n');
                            warnings.Add(new Warning(node.Start, line, node.Name, scope.Name, expected));
                        }
                    }
                }
                if (node.Kind == ConfigNodeKind.Block)
                {
                    parents.Add(node);
                    Visit(node.Children);
                    parents.RemoveAt(parents.Count - 1);
                }
            }
        }

        Visit(document.Nodes);
        return warnings;
    }

    public static Verdict AssessInsertion(
        ConfigSyntaxDocument document, ConfigSyntaxNode? parent,
        DocumentedDirective directive, string? fileName = null)
    {
        if (directive.Server != (document.Engine == ServerEngine.Nginx ? "nginx" : "apache"))
            return Verdict.Unknown;

        var path = FindPath(document.Nodes, parent);
        if (parent is not null && path is null)
            return Verdict.Unknown;

        return Evaluate(ResolveScope(document.Engine, path ?? [], fileName), [directive]);
    }

    private static IReadOnlyList<ConfigSyntaxNode>? FindPath(
        IEnumerable<ConfigSyntaxNode> nodes, ConfigSyntaxNode? target)
    {
        if (target is null)
            return [];
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node, target))
                return [node];
            var childPath = FindPath(node.Children, target);
            if (childPath is not null)
                return [node, .. childPath];
        }
        return null;
    }

    private static IEnumerable<DocumentedDirective> ForNodeKind(
        IEnumerable<DocumentedDirective> entries, ServerEngine engine, ConfigNodeKind kind)
    {
        if (engine != ServerEngine.Nginx)
            return kind == ConfigNodeKind.Directive ? entries : [];
        // The catalog includes homonymous block/directive entries (e.g. server).
        return entries.Where(e => kind == ConfigNodeKind.Block
            ? e.Syntax.Contains("{", StringComparison.Ordinal)
            : !e.Syntax.Contains("{", StringComparison.Ordinal) ||
              e.Syntax.Contains(";", StringComparison.Ordinal));
    }

    private sealed record Scope(string Name, string? Family = null);

    private static Scope ResolveScope(
        ServerEngine engine, IReadOnlyList<ConfigSyntaxNode> ancestors, string? fileName)
    {
        if (engine == ServerEngine.Apache)
        {
            var context = fileName is not null &&
                Path.GetFileName(fileName).Equals(".htaccess", StringComparison.OrdinalIgnoreCase)
                    ? ".htaccess" : "server config";
            foreach (var parent in ancestors.Where(p => p.Kind == ConfigNodeKind.Block))
            {
                if (parent.Name.Equals("VirtualHost", StringComparison.OrdinalIgnoreCase))
                    context = "virtual host";
                else if (new[] { "Directory", "DirectoryMatch", "Location", "LocationMatch",
                        "Files", "FilesMatch", "Proxy", "ProxyMatch", "If", "Else", "ElseIf",
                        "Limit", "LimitExcept" }.Contains(parent.Name, StringComparer.OrdinalIgnoreCase))
                    context = "directory";
                else if (!new[] { "IfModule", "IfDefine", "IfVersion" }
                             .Contains(parent.Name, StringComparer.OrdinalIgnoreCase))
                    return new Scope("unknown");
            }
            return new Scope(context);
        }

        var family = ancestors.FirstOrDefault(p => p.Kind == ConfigNodeKind.Block &&
            new[] { "http", "stream", "mail" }.Contains(p.Name, StringComparer.Ordinal))?.Name;
        var scope = "main";
        foreach (var parent in ancestors.Where(p => p.Kind == ConfigNodeKind.Block))
        {
            var name = parent.Name;
            if (name is "http" or "events" or "stream" or "mail" or "server" or
                "location" or "upstream" or "limit_except")
                scope = name;
            else if (name == "if")
                scope = ancestors.Any(p => p.Name == "location") ? "if in location" : "if in server";
            else
                scope = "unknown";
        }
        return new Scope(scope, family);
    }

    private static Verdict Evaluate(Scope scope, IReadOnlyList<DocumentedDirective> entries)
    {
        if (scope.Name == "unknown" || entries.Count == 0)
            return Verdict.Unknown;

        var relevant = entries;
        if (scope.Family is not null)
        {
            var familyPrefix = "ngx_" + scope.Family + "_";
            relevant = entries.Where(e => e.Module.StartsWith(familyPrefix, StringComparison.Ordinal) ||
                e.Module == "ngx_core_module").ToList();
            if (relevant.Count == 0)
                return Verdict.Unknown; // module family cannot be inferred from this catalog
        }

        var contexts = relevant.SelectMany(e => e.Context.Split(',', StringSplitOptions.TrimEntries))
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (contexts.Count == 0)
            return Verdict.Unknown;
        return contexts.Contains(scope.Name) ? Verdict.Compatible : Verdict.Incompatible;
    }
}
