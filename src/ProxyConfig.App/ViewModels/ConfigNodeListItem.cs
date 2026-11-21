using ProxyConfig.Core.Syntax;

namespace ProxyConfig.App.ViewModels;

public sealed class ConfigNodeListItem(ConfigSyntaxNode node, int depth)
{
    public ConfigSyntaxNode Node { get; } = node;
    public int Depth { get; } = depth;
    public string Display =>
        $"{new string(' ', Math.Min(24, Depth * 2))}{Node.Kind}: " +
        (Node.Name.Length == 0 ? "(text)" : Node.Name);
}
