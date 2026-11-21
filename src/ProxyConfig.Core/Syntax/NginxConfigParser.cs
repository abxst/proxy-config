using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Syntax;

public static class NginxConfigParser
{
    public static ConfigSyntaxDocument Parse(string text)
    {
        var state = new Reader(text);
        var nodes = state.ReadRegion(isBlock: false, out _);
        return new ConfigSyntaxDocument(ServerEngine.Nginx, text, nodes, state.Diagnostics);
    }

    private sealed class Reader(string source)
    {
        private int _position;
        public List<ConfigSyntaxDiagnostic> Diagnostics { get; } = [];

        public IReadOnlyList<ConfigSyntaxNode> ReadRegion(bool isBlock, out int closeStart)
        {
            var nodes = new List<ConfigSyntaxNode>();
            closeStart = -1;

            while (_position < source.Length)
            {
                var start = _position;
                var current = source[_position];

                if (char.IsWhiteSpace(current))
                {
                    while (_position < source.Length && char.IsWhiteSpace(source[_position]))
                        _position++;
                    nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Trivia, string.Empty, start, _position));
                    continue;
                }

                if (current == '#')
                {
                    while (_position < source.Length && source[_position] is not ('\r' or '\n'))
                        _position++;
                    if (_position < source.Length && source[_position] == '\r')
                        _position++;
                    if (_position < source.Length && source[_position] == '\n')
                        _position++;
                    nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Comment, string.Empty, start, _position));
                    continue;
                }

                if (current == '}')
                {
                    _position++;
                    if (isBlock)
                    {
                        closeStart = start;
                        return nodes;
                    }
                    Diagnostics.Add(new ConfigSyntaxDiagnostic(start, "Unmatched nginx closing brace."));
                    nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Raw, string.Empty, start, _position));
                    continue;
                }

                var name = ReadName(source, start);
                char quote = '\0';
                var terminated = false;

                while (_position < source.Length)
                {
                    var c = source[_position];
                    if (c == '\\' && _position + 1 < source.Length)
                    {
                        _position += 2;
                        continue;
                    }

                    if (quote != '\0')
                    {
                        if (c == quote)
                            quote = '\0';
                        _position++;
                        continue;
                    }

                    if (c is '\'' or '"')
                    {
                        quote = c;
                        _position++;
                        continue;
                    }

                    if (c == ';')
                    {
                        _position++;
                        nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Directive, name, start, _position));
                        terminated = true;
                        break;
                    }

                    if (c == '{')
                    {
                        _position++;
                        var openEnd = _position;
                        var children = ReadRegion(isBlock: true, out var innerCloseStart);
                        if (innerCloseStart < 0)
                            Diagnostics.Add(new ConfigSyntaxDiagnostic(start, $"Unclosed nginx block '{name}'."));
                        nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Block, name, start, _position,
                            openEnd, innerCloseStart, children));
                        terminated = true;
                        break;
                    }

                    if (c is '}' or '#')
                    {
                        if (_position > start)
                        {
                            nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Raw, name, start, _position));
                            Diagnostics.Add(new ConfigSyntaxDiagnostic(start,
                                $"Unterminated nginx directive '{name}' before comment/brace."));
                        }
                        terminated = true;
                        break;
                    }

                    _position++;
                }

                if (terminated)
                    continue;

                Diagnostics.Add(new ConfigSyntaxDiagnostic(start,
                    quote != '\0' ? $"Unclosed nginx quotation in '{name}'." :
                        $"Unterminated nginx directive '{name}' at end of file."));
                nodes.Add(new ConfigSyntaxNode(ConfigNodeKind.Raw, name, start, _position));
            }

            return nodes;
        }

        private static string ReadName(string text, int start)
        {
            var end = start;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) &&
                   text[end] is not (';' or '{' or '}' or '#'))
                end++;
            return text[start..end];
        }
    }
}
