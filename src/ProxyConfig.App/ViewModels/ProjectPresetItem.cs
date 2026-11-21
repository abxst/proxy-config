using ProxyConfig.Core.Models;

namespace ProxyConfig.App.ViewModels;

/// <summary>A frozen generated preset queued for the current ZIP composer.</summary>
public sealed record ProjectPresetItem(string Name, ServerEngine Engine, string Text)
{
    public string Display => $"{Engine} / {Name} ({Text.Length:N0} chars)";
}
