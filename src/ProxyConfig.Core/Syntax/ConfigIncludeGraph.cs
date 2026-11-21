using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Syntax;

public sealed record ConfigIncludeEdge(string From, string To, bool Optional);
public sealed record ConfigIncludeIssue(string File, string Message);

/// <summary>
/// Resolves an explicit set of imported files, without reading arbitrary
/// filesystem paths or fetching anything from the target server.
/// </summary>
public sealed class ConfigIncludeGraph
{
    private ConfigIncludeGraph(
        IReadOnlyList<ConfigIncludeEdge> edges,
        IReadOnlyList<ConfigIncludeIssue> issues,
        IReadOnlyList<string> visitedFiles)
    {
        Edges = edges;
        Issues = issues;
        VisitedFiles = visitedFiles;
    }

    public IReadOnlyList<ConfigIncludeEdge> Edges { get; }
    public IReadOnlyList<ConfigIncludeIssue> Issues { get; }
    public IReadOnlyList<string> VisitedFiles { get; }

    public static ConfigIncludeGraph Build(
        ServerEngine engine,
        string rootFile,
        IReadOnlyDictionary<string, string> suppliedFiles,
        string? apacheServerRoot = null,
        int maxDepth = 64)
    {
        if (maxDepth < 1)
            throw new ArgumentOutOfRangeException(nameof(maxDepth));

        var sources = suppliedFiles.ToDictionary(
            item => NormalizePath(item.Key), item => item.Value, StringComparer.Ordinal);
        var root = NormalizePath(rootFile);
        var issues = new List<ConfigIncludeIssue>();
        var edges = new List<ConfigIncludeEdge>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);

        void Visit(string path, int depth)
        {
            if (depth > maxDepth)
            {
                issues.Add(new ConfigIncludeIssue(path, "Include depth limit exceeded."));
                return;
            }
            if (!sources.TryGetValue(path, out var content))
            {
                issues.Add(new ConfigIncludeIssue(path, "Imported file content is unavailable."));
                return;
            }
            if (!active.Add(path))
            {
                issues.Add(new ConfigIncludeIssue(path, "Circular include detected."));
                return;
            }
            if (!visited.Add(path))
            {
                active.Remove(path);
                return;
            }

            var document = ConfigSyntaxDocument.Parse(engine, content);
            foreach (var node in document.AllNodes.Where(node => node.Kind == ConfigNodeKind.Directive))
            {
                var optional = node.Name.Equals("IncludeOptional", StringComparison.OrdinalIgnoreCase);
                if (!optional && !node.Name.Equals("Include", StringComparison.OrdinalIgnoreCase))
                    continue;

                var target = ReadIncludeTarget(document.GetText(node), node.Name, engine);
                if (target.Length == 0)
                {
                    issues.Add(new ConfigIncludeIssue(path, "Empty include path."));
                    continue;
                }
                var basePath = engine == ServerEngine.Apache && apacheServerRoot is not null
                    ? NormalizePath(apacheServerRoot)
                    : ParentDirectory(path);
                var pattern = NormalizePath(target.StartsWith('/') || HasDrivePrefix(target)
                    ? target : basePath + "/" + target);
                var matches = sources.Keys
                    .Where(candidate => MatchPattern(pattern, candidate))
                    .OrderBy(candidate => candidate, StringComparer.Ordinal)
                    .ToArray();
                if (matches.Length == 0 && !optional)
                    issues.Add(new ConfigIncludeIssue(path, $"Missing required include: {target}"));

                foreach (var match in matches)
                {
                    edges.Add(new ConfigIncludeEdge(path, match, optional));
                    Visit(match, depth + 1);
                }
            }

            active.Remove(path);
        }

        Visit(root, 0);
        return new ConfigIncludeGraph(edges, issues, visited.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static string ReadIncludeTarget(string text, string name, ServerEngine engine)
    {
        var value = text.Trim()[name.Length..].Trim();
        if (engine == ServerEngine.Nginx)
            value = value.TrimEnd(';').Trim();
        if (value.Length > 1 && (value[0] == '\'' || value[0] == '"') && value[^1] == value[0])
            value = value[1..^1];
        return value;
    }

    private static bool MatchPattern(string pattern, string candidate)
    {
        if (!pattern.Contains('*') && !pattern.Contains('?'))
            return pattern == candidate;
        var expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(candidate, expression, RegexOptions.CultureInvariant);
    }

    private static string ParentDirectory(string path)
    {
        var last = path.LastIndexOf('/');
        return last <= 0 ? (path.StartsWith('/') ? "/" : ".") : path[..last];
    }

    private static bool HasDrivePrefix(string path) =>
        path.Length > 2 && char.IsLetter(path[0]) && path[1] == ':' && path[2] == '/';

    private static string NormalizePath(string path)
    {
        path = path.Replace('\\', '/');
        var prefix = path.StartsWith('/') ? "/" : string.Empty;
        var parts = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == ".." && parts.Count > 0 && parts[^1] != "..")
            {
                parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(segment);
        }
        return prefix + string.Join('/', parts);
    }
}
