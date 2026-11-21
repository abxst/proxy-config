using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Syntax;

public static class ApacheConfigParser
{
    public static ConfigSyntaxDocument Parse(string text)
    {
        var reader = new Reader(text);
        var nodes = reader.ReadRegion(expectedClose: null, out _);
        return new ConfigSyntaxDocument(ServerEngine.Apache, text, nodes, reader.Diagnostics);
    }

    private sealed class Reader(string source)
    {
        private int _position;
        public List<ConfigSyntaxDiagnostic> Diagnostics { get; } = [];

        public IReadOnlyList<ConfigSyntaxNode> ReadRegion(string? expectedClose, out int closeStart)
        {
            var nodes = new List<ConfigSyntaxNode>();
            closeStart = -1;

            while (_position < source.Length)
            {
                var start = _position;
                var line = ReadLogicalLine();
                var trimmed = line.Trim();

                if (trimmed.Length == 0)
                {
                    nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Trivia, string.Empty, start, _position));
                    continue;
                }

                if (trimmed.StartsWith('#'))
                {
                    nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Comment, string.Empty, start, _position));
                    continue;
                }

                if (TryContainerTag(trimmed, out var name, out var closing))
                {
                    if (closing)
                    {
                        if (name.Equals(expectedClose, StringComparison.OrdinalIgnoreCase))
                        {
                            closeStart = start;
                            return nodes;
                        }

                        Diagnostics.Add(new ConfigSyntaxDiagnostic(start,
                            $"Unmatched Apache closing container '</{name}>'."));
                        nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Raw, name, start, _position));
                        continue;
                    }

                    var openEnd = _position;
                    var children = ReadRegion(name, out var closingPosition);
                    if (closingPosition < 0)
                        Diagnostics.Add(new ConfigSyntaxDiagnostic(start, $"Unclosed Apache container '<{name}>'."));
                    nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Block, name, start, _position,
                        openEnd, closingPosition, children));
                    continue;
                }

                if (trimmed.StartsWith('<'))
                {
                    Diagnostics.Add(new ConfigSyntaxDiagnostic(start, "Unrecognized Apache container syntax."));
                    nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Raw, string.Empty, start, _position));
                    continue;
                }

                var nameEnd = 0;
                while (nameEnd < trimmed.Length && !char.IsWhiteSpace(trimmed[nameEnd]))
                    nameEnd++;
                nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Directive, trimmed[..nameEnd],
                    start, _position));
            }

            return nodes;
        }

        private string ReadLogicalLine()
        {
            var start = _position;
            while (_position < source.Length)
            {
                var lineStart = _position;
                while (_position < source.Length && source[_position] is not ('\r' or '\n'))
                    _position++;
                var contentEnd = _position;
                if (_position < source.Length && source[_position] == '\r')
                    _position++;
                if (_position < source.Length && source[_position] == '\n')
                    _position++;
                // Apache continues a line only when the backslash is the last
                // character immediately preceding the physical newline.
                if (contentEnd == lineStart || source[contentEnd - 1] != '\\' ||
                    _position == source.Length)
                    break;
            }
            return source[start.._position];
        }

        private static bool TryContainerTag(string line, out string name, out bool closing)
        {
            name = string.Empty;
            closing = false;
            if (line.Length < 3 || line[0] != '<')
                return false;
            closing = line[1] == '/';
            var offset = closing ? 2 : 1;
            var end = offset;
            while (end < line.Length && !char.IsWhiteSpace(line[end]) && line[end] != '>')
                end++;
            if (end == offset || line[^1] != '>' ||
                line.AsSpan(offset, end - offset).IndexOfAny('<', '/') >= 0)
                return false;
            name = line[offset..end];
            return true;
        }
    }
}
