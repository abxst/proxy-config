using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Syntax;

/// <summary>
/// An immutable text-preserving syntax snapshot. Editing a span re-parses it;
/// all bytes outside the edited span remain textually identical.
/// </summary>
public sealed class ConfigSyntaxDocument
{
    internal ConfigSyntaxDocument(
        ServerEngine engine, string text, IReadOnlyList<ConfigSyntaxNode> nodes,
        IReadOnlyList<ConfigSyntaxDiagnostic> diagnostics)
    {
        Engine = engine;
        Text = text;
        Nodes = nodes;
        Diagnostics = diagnostics;
    }

    public ServerEngine Engine { get; }
    public string Text { get; }
    public IReadOnlyList<ConfigSyntaxNode> Nodes { get; }
    public IReadOnlyList<ConfigSyntaxDiagnostic> Diagnostics { get; }

    public IEnumerable<ConfigSyntaxNode> AllNodes =>
        Nodes.SelectMany(node => node.DescendantsAndSelf());

    public IReadOnlyList<ConfigSyntaxNode> VirtualHosts =>
        AllNodes.Where(node => node.Kind == ConfigNodeKind.Block &&
            node.Name.Equals(Engine == ServerEngine.Nginx ? "server" : "VirtualHost",
                StringComparison.OrdinalIgnoreCase)).ToArray();

    public static ConfigSyntaxDocument Parse(ServerEngine engine, string? text) =>
        engine switch
        {
            ServerEngine.Nginx => NginxConfigParser.Parse(text ?? string.Empty),
            ServerEngine.Apache => ApacheConfigParser.Parse(text ?? string.Empty),
            _ => throw new ArgumentOutOfRangeException(nameof(engine))
        };

    public string GetText(ConfigSyntaxNode node)
    {
        EnsureValidSpan(node.Start, node.End);
        return Text[node.Start..node.End];
    }

    public ConfigSyntaxDocument ReplaceNode(ConfigSyntaxNode node, string replacement)
    {
        if (!AllNodes.Any(current => ReferenceEquals(current, node)))
            throw new ArgumentException("The node does not belong to this document.", nameof(node));
        return ReplaceSpan(node.Start, node.End, replacement);
    }

    public ConfigSyntaxDocument ReplaceSpan(int start, int end, string replacement)
    {
        EnsureValidSpan(start, end);
        var updated = string.Concat(Text.AsSpan(0, start), replacement.AsSpan(), Text.AsSpan(end));
        return Parse(Engine, updated);
    }

    /// <summary>
    /// Adds a generated server/VirtualHost as another sibling. If a full nginx
    /// document has an http block, the server is inserted inside that block.
    /// Otherwise the snippet is appended for use in an http-context include.
    /// </summary>
    public ConfigSyntaxDocument AppendVirtualHost(string generatedSnippet)
    {
        if (string.IsNullOrWhiteSpace(generatedSnippet))
            throw new ArgumentException("Generated host snippet must not be empty.", nameof(generatedSnippet));

        var eol = Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var snippet = generatedSnippet.TrimEnd('\r', '\n')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", eol, StringComparison.Ordinal);

        if (Engine == ServerEngine.Nginx)
        {
            var http = Nodes.FirstOrDefault(node => node.Kind == ConfigNodeKind.Block &&
                node.Name.Equals("http", StringComparison.OrdinalIgnoreCase) && node.IsClosed);
            if (http is not null)
            {
                var indent = "    ";
                var indented = string.Join(eol, snippet.Split(eol).Select(line =>
                    line.Length == 0 ? string.Empty : indent + line));
                var before = Text[..http.CloseStart];
                var leadingNewline = before.Length > 0 && before[^1] != '\n' ? eol : string.Empty;
                return ReplaceSpan(http.CloseStart, http.CloseStart,
                    leadingNewline + indented + eol);
            }
        }

        var separator = Text.Length == 0 || Text.EndsWith('\n') ? string.Empty : eol;
        return ReplaceSpan(Text.Length, Text.Length, separator + snippet + eol);
    }

    private void EnsureValidSpan(int start, int end)
    {
        if (start < 0 || end < start || end > Text.Length)
            throw new ArgumentOutOfRangeException(nameof(start), "Span is outside document bounds.");
    }
}
