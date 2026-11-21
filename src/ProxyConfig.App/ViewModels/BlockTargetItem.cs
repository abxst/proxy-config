using ProxyConfig.Core.Syntax;

namespace ProxyConfig.App.ViewModels;

public sealed class BlockTargetItem(ConfigSyntaxNode? node, string display)
{
    public ConfigSyntaxNode? Node { get; } = node;
    public string Display { get; } = display;
}
