namespace ProxyConfig.Core.Syntax;

public enum ConfigNodeKind
{
    Trivia,
    Comment,
    Directive,
    Block,
    Raw
}

/// <summary>
/// Spans refer to the original document text. No syntax or comments are
/// normalized, so unknown directives survive a parse/edit/export round-trip.
/// </summary>
public sealed class ConfigSyntaxNode
{
    internal ConfigSyntaxNode(
        ConfigNodeKind kind, string name, int start, int end,
        int openEnd = -1, int closeStart = -1,
        IReadOnlyList<ConfigSyntaxNode>? children = null)
    {
        Kind = kind;
        Name = name;
        Start = start;
        End = end;
        OpenEnd = openEnd;
        CloseStart = closeStart;
        Children = children ?? [];
    }

    public ConfigNodeKind Kind { get; }
    public string Name { get; }
    public int Start { get; }
    public int End { get; }
    public int OpenEnd { get; }
    public int CloseStart { get; }
    public IReadOnlyList<ConfigSyntaxNode> Children { get; }
    public bool IsClosed => Kind != ConfigNodeKind.Block || CloseStart >= 0;

    public IEnumerable<ConfigSyntaxNode> DescendantsAndSelf()
    {
        yield return this;
        foreach (var child in Children)
            foreach (var descendant in child.DescendantsAndSelf())
                yield return descendant;
    }
}

public sealed record ConfigSyntaxDiagnostic(int Offset, string Message);
