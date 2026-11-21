using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static partial class HttpHeaderParser
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderNameRegex();

    /// <summary>Accepts one "Name: value" header per line, no continuations.</summary>
    public static IReadOnlyList<HttpHeader> Parse(string? lines, out IReadOnlyList<string> errors)
    {
        var parsed = new List<HttpHeader>();
        var problems = new List<string>();
        var normalized = (lines ?? string.Empty).Replace("\r\n", "\n");
        var inputLines = normalized.Split('\n');

        for (var index = 0; index < inputLines.Length; index++)
        {
            var line = inputLines[index];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var colon = line.IndexOf(':');
            var name = colon > 0 ? line[..colon].Trim() : string.Empty;
            var value = colon > 0 ? line[(colon + 1)..].Trim() : string.Empty;
            if (!HeaderNameRegex().IsMatch(name) || value.Any(char.IsControl))
            {
                problems.Add($"Header line {index + 1}: expected a valid Name: value pair.");
                continue;
            }

            parsed.Add(new HttpHeader(name, value));
        }

        errors = problems;
        return parsed;
    }
}
