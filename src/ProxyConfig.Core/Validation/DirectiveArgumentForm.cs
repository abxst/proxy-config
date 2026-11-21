using System.Globalization;
using System.Text.RegularExpressions;
using ProxyConfig.Core.Registry;

namespace ProxyConfig.Core.Validation;

/// <summary>
/// A conservative single-argument form derived from documented syntax.
/// Complex or ambiguous syntax always falls back to unrestricted raw text.
/// It is intentionally not a complete directive argument grammar.
/// </summary>
public sealed partial class DirectiveArgumentForm
{
    public enum InputKind { Raw, Choice, Integer, Size }

    [GeneratedRegex(@"^[A-Za-z0-9._+-]+(?:\s*\|\s*[A-Za-z0-9._+-]+)+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SimpleChoices();

    [GeneratedRegex(@"^[0-9]+[kKmMgG]?$", RegexOptions.CultureInvariant)]
    private static partial Regex NginxSize();

    private DirectiveArgumentForm(InputKind kind, IReadOnlyList<string> choices, string hint)
    {
        Kind = kind;
        Choices = choices;
        Hint = hint;
    }

    public InputKind Kind { get; }
    public IReadOnlyList<string> Choices { get; }
    public string Hint { get; }
    public bool HasChoices => Kind == InputKind.Choice;

    public static DirectiveArgumentForm Infer(DocumentedDirective entry)
    {
        var syntax = entry.Syntax.Trim();
        if (!syntax.StartsWith(entry.Name + " ", StringComparison.OrdinalIgnoreCase))
            return Raw();
        var tail = syntax[(entry.Name.Length + 1)..].TrimEnd(';').Trim();
        if (tail.Length == 0 || tail.Contains(';') || tail.Contains('\n') ||
            tail.Contains('{') || tail.Contains('}') || tail.Contains('[') ||
            tail.Contains(']') || tail.Contains('(') || tail.Contains(')'))
            return Raw();

        if (SimpleChoices().IsMatch(tail))
        {
            var choices = tail.Split('|', StringSplitOptions.TrimEntries);
            if (choices.All(choice => !new[] { "number", "size", "path", "URL", "time",
                "timeout", "value", "string", "variable" }.Contains(choice, StringComparer.OrdinalIgnoreCase)))
                return new(InputKind.Choice, choices,
                    $"Choose one value: {string.Join(", ", choices)}.");
        }

        if (tail.Equals("number", StringComparison.OrdinalIgnoreCase))
            return new(InputKind.Integer, [], "Non-negative integer (digits only).");

        if (entry.Server == "nginx" && tail.Equals("size", StringComparison.OrdinalIgnoreCase))
            return new(InputKind.Size, [], "Size in bytes; optional k/m/g suffix.");

        return Raw();
    }

    public string? Validate(string? arguments, bool ignoreForm = false)
    {
        if (ignoreForm || Kind == InputKind.Raw)
            return null;
        var text = arguments?.Trim() ?? "";
        return Kind switch
        {
            InputKind.Choice when !Choices.Contains(text, StringComparer.OrdinalIgnoreCase) =>
                $"Expected one of: {string.Join(", ", Choices)}.",
            InputKind.Integer when !ulong.TryParse(text, NumberStyles.None,
                CultureInfo.InvariantCulture, out _) =>
                "Expected a non-negative integer.",
            InputKind.Size when !NginxSize().IsMatch(text) =>
                "Expected a non-negative byte size such as 0, 128k or 2m.",
            _ => null
        };
    }

    private static DirectiveArgumentForm Raw() =>
        new(InputKind.Raw, [], "Complex syntax: enter the documented arguments manually.");
}
