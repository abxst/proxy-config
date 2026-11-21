using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Syntax;

/// <summary>
/// Structural multi-block operations. Every mutation preserves the untouched
/// source text. Values are validated as a single directive/block before insert.
/// Context/module/version compatibility is separate from structural safety.
/// </summary>
public static partial class ConfigBlockEditor
{
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NameRegex();

    public static ConfigSyntaxDocument AddDirective(
        ConfigSyntaxDocument document, ConfigSyntaxNode? parent,
        string name, string? arguments)
    {
        CheckName(name);
        arguments ??= string.Empty;
        RejectControlCharacters(arguments);
        var fragment = name + (string.IsNullOrWhiteSpace(arguments) ? "" : " " + arguments.Trim()) +
            (document.Engine == ServerEngine.Nginx ? ";" : "");
        ValidateSingleFragment(document.Engine, fragment, ConfigNodeKind.Directive);
        return Insert(document, parent, fragment);
    }

    public static ConfigSyntaxDocument AddBlock(
        ConfigSyntaxDocument document, ConfigSyntaxNode? parent,
        string name, string? arguments)
    {
        CheckName(name);
        arguments ??= string.Empty;
        RejectControlCharacters(arguments);
        var header = name + (string.IsNullOrWhiteSpace(arguments) ? "" : " " + arguments.Trim());
        var fragment = document.Engine == ServerEngine.Nginx
            ? header + " {\n}"
            : "<" + header + ">\n</" + name + ">";
        ValidateSingleFragment(document.Engine, fragment, ConfigNodeKind.Block);
        return Insert(document, parent, fragment);
    }

    /// <summary>
    /// Insert a pre-generated, structurally valid collection of directives and
    /// blocks. Intended for built-in presets, not untrusted free-form snippets.
    /// </summary>
    public static ConfigSyntaxDocument InsertGeneratedSnippet(
        ConfigSyntaxDocument document, ConfigSyntaxNode? parent, string snippet)
    {
        var parsed = ConfigSyntaxDocument.Parse(document.Engine, snippet ?? string.Empty);
        if (parsed.Diagnostics.Count > 0 || parsed.Nodes.Any(n => n.Kind == ConfigNodeKind.Raw) ||
            !parsed.Nodes.Any(n => n.Kind is ConfigNodeKind.Block or ConfigNodeKind.Directive))
            throw new ArgumentException("Generated snippet is not structurally valid.", nameof(snippet));
        return Insert(document, parent, ReindentFragment(snippet));
    }

    public static ConfigSyntaxDocument Remove(
        ConfigSyntaxDocument document, ConfigSyntaxNode node)
    {
        EnsureBelongs(document, node);
        if (node.Kind is not (ConfigNodeKind.Block or ConfigNodeKind.Directive))
            throw new ArgumentException("Only a directive or block may be removed.", nameof(node));
        return document.ReplaceNode(node, string.Empty);
    }

    public static ConfigSyntaxDocument Duplicate(
        ConfigSyntaxDocument document, ConfigSyntaxNode node)
    {
        var parent = FindParent(document, node, out _);
        if (node.Kind is not (ConfigNodeKind.Block or ConfigNodeKind.Directive))
            throw new ArgumentException("Only a directive or block may be duplicated.", nameof(node));
        // Duplicates are appended inside the same parent. Keeping the original
        // source span in place also keeps adjacent comments where they belong.
        return Insert(document, parent, document.GetText(node).Trim());
    }

    /// <summary>
    /// Swap with the preceding/following directive or block sibling. The
    /// intervening trivia is retained; crossing comments is deliberately
    /// refused rather than silently reattaching them to another directive.
    /// </summary>
    public static ConfigSyntaxDocument Move(
        ConfigSyntaxDocument document, ConfigSyntaxNode node, int direction)
    {
        if (direction is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(direction));

        var parent = FindParent(document, node, out var siblings);
        var candidates = siblings.Where(n => n.Kind is ConfigNodeKind.Block or ConfigNodeKind.Directive).ToList();
        var index = candidates.FindIndex(current => ReferenceEquals(current, node));
        if (index < 0 || index + direction < 0 || index + direction >= candidates.Count)
            throw new InvalidOperationException("No neighboring directive/block in that direction.");

        var neighbor = candidates[index + direction];
        var left = node.Start < neighbor.Start ? node : neighbor;
        var right = node.Start < neighbor.Start ? neighbor : node;
        if (siblings.Any(n => n.Kind == ConfigNodeKind.Comment &&
            n.Start >= left.End && n.End <= right.Start) ||
            HasInlineComment(document.Text, left) || HasInlineComment(document.Text, right))
            throw new InvalidOperationException("Cannot move across attached comments safely.");

        // The parent variable deliberately establishes a same-parent operation.
        _ = parent;
        var originalLeft = document.GetText(left);
        var originalRight = document.GetText(right);
        var between = document.Text[left.End..right.Start];
        return document.ReplaceSpan(left.Start, right.End, originalRight + between + originalLeft);
    }

    /// <summary>
    /// Relocate a whole directive/block into another existing block or root.
    /// Reparse between removal and insertion so stale source spans are never
    /// reused. Comments outside the selected node stay at their original site.
    /// </summary>
    public static ConfigSyntaxDocument MoveToParent(
        ConfigSyntaxDocument document, ConfigSyntaxNode node, ConfigSyntaxNode? destination)
    {
        var sourceParent = FindParent(document, node, out var siblings);
        if (node.Kind is not (ConfigNodeKind.Block or ConfigNodeKind.Directive))
            throw new ArgumentException("Only a directive or block may be relocated.", nameof(node));
        if (destination is not null)
        {
            EnsureBelongs(document, destination);
            if (destination.Kind != ConfigNodeKind.Block || !destination.IsClosed)
                throw new ArgumentException("Destination must be a closed block.", nameof(destination));
            if (destination.Start >= node.Start && destination.End <= node.End)
                throw new InvalidOperationException("Cannot move a block into itself or its descendants.");
        }
        if (ReferenceEquals(sourceParent, destination))
            throw new InvalidOperationException("Node is already in that scope.");
        if (HasInlineComment(document.Text, node) ||
            HasAdjacentLeadingComment(siblings, node))
            throw new InvalidOperationException(
                "Cannot relocate a node with an adjacent or inline comment without review.");

        var fragment = ReindentFragment(document.GetText(node));
        var removed = document.ReplaceNode(node, string.Empty);
        if (destination is null)
            return Insert(removed, null, fragment);

        var destinationStart = destination.Start > node.End
            ? destination.Start - (node.End - node.Start)
            : destination.Start;
        var freshDestination = removed.AllNodes.FirstOrDefault(candidate =>
            candidate.Kind == ConfigNodeKind.Block && candidate.Start == destinationStart &&
            candidate.Name == destination.Name);
        if (freshDestination is null)
            throw new InvalidOperationException("Destination changed during relocation.");
        return Insert(removed, freshDestination, fragment);
    }

    /// <summary>
    /// Atomically computes two new immutable documents for a cross-file move.
    /// The caller must commit both documents as one workspace transaction.
    /// </summary>
    public static (ConfigSyntaxDocument Source, ConfigSyntaxDocument Destination) MoveBetweenDocuments(
        ConfigSyntaxDocument source, ConfigSyntaxNode node,
        ConfigSyntaxDocument destination, ConfigSyntaxNode? destinationParent)
    {
        if (ReferenceEquals(source, destination))
            throw new ArgumentException("Use MoveToParent for same-document moves.");
        if (source.Engine != destination.Engine)
            throw new ArgumentException("Source and target engines must match.");
        FindParent(source, node, out var siblings);
        if (node.Kind is not (ConfigNodeKind.Block or ConfigNodeKind.Directive))
            throw new ArgumentException("Only directives/blocks can move across files.");
        if (HasInlineComment(source.Text, node) || HasAdjacentLeadingComment(siblings, node))
            throw new InvalidOperationException("Source has potentially attached comments.");
        if (destinationParent is not null)
        {
            EnsureBelongs(destination, destinationParent);
            if (destinationParent.Kind != ConfigNodeKind.Block || !destinationParent.IsClosed)
                throw new ArgumentException("Destination must be a closed block.");
        }
        var snippet = ReindentFragment(source.GetText(node));
        var updatedDestination = InsertGeneratedSnippet(destination, destinationParent, snippet);
        var updatedSource = source.ReplaceNode(node, string.Empty);
        return (updatedSource, updatedDestination);
    }

    private static bool HasAdjacentLeadingComment(
        IReadOnlyList<ConfigSyntaxNode> siblings, ConfigSyntaxNode node)
    {
        var index = -1;
        for (var i = 0; i < siblings.Count; i++)
            if (ReferenceEquals(siblings[i], node)) { index = i; break; }
        if (index < 0) return false;
        for (var i = index - 1; i >= 0; i--)
        {
            if (siblings[i].Kind == ConfigNodeKind.Trivia)
                continue;
            return siblings[i].Kind == ConfigNodeKind.Comment;
        }
        return false;
    }

    private static string ReindentFragment(string fragment)
    {
        var lines = fragment.Trim().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length <= 1)
            return fragment.Trim();
        var baseline = lines.Skip(1)
            .Where(line => line.Trim().Length > 0)
            .Select(line => line.Length - line.TrimStart(' ', '\t').Length)
            .DefaultIfEmpty(0).Min();
        return string.Join('\n', lines.Select((line, index) =>
            index == 0 || line.Length == 0 ? line :
            line[Math.Min(baseline, line.Length)..]));
    }

    public static string GetContext(ConfigSyntaxDocument document, ConfigSyntaxNode? selected)
    {
        if (selected is null)
            return document.Engine == ServerEngine.Nginx ? "main" : "server config";
        var scope = GetInsertionScope(document, selected);
        if (scope is null)
            return document.Engine == ServerEngine.Nginx ? "main" : "server config";
        if (document.Engine == ServerEngine.Apache &&
            scope.Name.Equals("VirtualHost", StringComparison.OrdinalIgnoreCase))
            return "virtual host";
        return scope.Name;
    }

    /// <summary>Insert into a selected block, or into a directive's parent.</summary>
    public static ConfigSyntaxNode? GetInsertionScope(
        ConfigSyntaxDocument document, ConfigSyntaxNode? selected)
    {
        if (selected is null)
            return null;
        EnsureBelongs(document, selected);
        return selected.Kind == ConfigNodeKind.Block
            ? selected : FindParent(document, selected, out _);
    }

    private static ConfigSyntaxDocument Insert(
        ConfigSyntaxDocument document, ConfigSyntaxNode? parent, string fragment)
    {
        var eol = document.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var offset = document.Text.Length;
        var indentation = "";

        if (parent is not null)
        {
            EnsureBelongs(document, parent);
            if (parent.Kind != ConfigNodeKind.Block || !parent.IsClosed)
                throw new ArgumentException("Selected parent must be a closed block.", nameof(parent));
            offset = parent.CloseStart;
            indentation = LineIndent(document.Text, parent.Start) + "    ";
            // NGINX closes at the brace itself; insert before its indentation,
            // otherwise the old indentation becomes a stray whitespace line.
            if (document.Engine == ServerEngine.Nginx)
            {
                var lineStart = document.Text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1;
                if (document.Text[lineStart..offset].All(c => c is ' ' or '\t'))
                    offset = lineStart;
            }
        }

        var lines = fragment.Trim('\r', '\n').Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var prepared = string.Join(eol, lines.Select(line =>
            line.Length == 0 ? "" : indentation + line));
        var prefix = offset > 0 && document.Text[offset - 1] != '\n' ? eol : string.Empty;
        return document.ReplaceSpan(offset, offset, prefix + prepared + eol);
    }

    private static string LineIndent(string text, int position)
    {
        var lineStart = position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;
        var end = lineStart;
        while (end < text.Length && text[end] is ' ' or '\t')
            end++;
        return text[lineStart..end];
    }

    private static bool HasInlineComment(string text, ConfigSyntaxNode node)
    {
        var endOfLine = text.IndexOfAny(['\r', '\n'], node.End);
        if (endOfLine < 0) endOfLine = text.Length;
        return text[node.End..endOfLine].Contains('#');
    }

    private static ConfigSyntaxNode? FindParent(
        ConfigSyntaxDocument document, ConfigSyntaxNode selected,
        out IReadOnlyList<ConfigSyntaxNode> siblings)
    {
        EnsureBelongs(document, selected);
        ConfigSyntaxNode? found = null;
        IReadOnlyList<ConfigSyntaxNode> foundSiblings = document.Nodes;

        bool Search(IReadOnlyList<ConfigSyntaxNode> nodes, ConfigSyntaxNode? parent)
        {
            foreach (var node in nodes)
            {
                if (ReferenceEquals(node, selected))
                {
                    found = parent;
                    foundSiblings = nodes;
                    return true;
                }
                if (Search(node.Children, node))
                    return true;
            }
            return false;
        }

        Search(document.Nodes, null);
        siblings = foundSiblings;
        return found;
    }

    private static void EnsureBelongs(ConfigSyntaxDocument document, ConfigSyntaxNode node)
    {
        if (!document.AllNodes.Any(n => ReferenceEquals(n, node)))
            throw new ArgumentException("Selected node is not part of this document.", nameof(node));
    }

    private static void CheckName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !NameRegex().IsMatch(name))
            throw new ArgumentException("Directive/block name must contain only letters, digits, '_' or '-'.");
    }

    private static void RejectControlCharacters(string arguments)
    {
        if (arguments.Any(c => c is '\r' or '\n' or '\0'))
            throw new ArgumentException("Directive arguments must stay on one logical line.");
    }

    private static void ValidateSingleFragment(
        ServerEngine engine, string fragment, ConfigNodeKind expectedKind)
    {
        var parsed = ConfigSyntaxDocument.Parse(engine, fragment);
        if (parsed.Diagnostics.Count != 0 ||
            parsed.Nodes.Count(n => n.Kind == expectedKind) != 1 ||
            parsed.Nodes.Any(n => n.Kind is ConfigNodeKind.Comment or ConfigNodeKind.Raw ||
                (n.Kind != expectedKind && n.Kind != ConfigNodeKind.Trivia)))
            throw new ArgumentException("Arguments would create invalid or additional syntax nodes.");
    }
}
