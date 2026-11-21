namespace ProxyConfig.App.ViewModels;

/// <summary>One explicitly imported source file with independent edit history.</summary>
public sealed class WorkspaceFileItem(string path, string text)
{
    public string Path { get; } = path;
    public string OriginalText { get; } = text;
    public string Text { get; set; } = text;
    public Stack<string> Undo { get; } = new();
    public Stack<string> Redo { get; } = new();
    public string Display => path;
}
