namespace ProxyConfig.Core.Syntax;

/// <summary>
/// Source-offset navigation only; it never reparses or changes imported text.
/// Offsets use UTF-16 indices, matching .NET string/Avalonia TextBox.
/// </summary>
public static class ConfigTextNavigator
{
    public static bool TryLineStart(string text, int oneBasedLine, out int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        offset = 0;
        if (oneBasedLine < 1)
            return false;
        if (oneBasedLine == 1)
            return true;
        var currentLine = 1;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
                continue;
            currentLine++;
            if (currentLine != oneBasedLine)
                continue;
            offset = index + 1;
            return true;
        }
        return false;
    }

    public static int LineAt(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (offset < 0 || offset > text.Length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        var line = 1;
        for (var index = 0; index < offset; index++)
            if (text[index] == '\n')
                line++;
        return line;
    }

    public static (int Start, int End)? FindNext(
        string text, string? term, int startAt = 0, bool ignoreCase = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(term) || term.Length > 256)
            return null;
        var comparer = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var start = Math.Clamp(startAt, 0, text.Length);
        var match = text.IndexOf(term, start, comparer);
        if (match < 0 && start != 0)
            match = text.IndexOf(term, 0, comparer);
        return match < 0 ? null : (match, match + term.Length);
    }
}
