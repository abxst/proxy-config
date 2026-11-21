using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;

namespace ProxyConfig.Core.Validation;

/// <summary>
/// Narrow security review hints grounded in explicit directive values.
/// A finding is not proof of a vulnerability and never blocks export.
/// </summary>
public static class ConfigSecurityLinter
{
    public sealed record Finding(int Line, string Name, string Message)
    {
        public string Display => $"Line {Line}: {Name} — {Message}";
    }

    public static IReadOnlyList<Finding> Analyze(ConfigSyntaxDocument document)
    {
        var found = new List<Finding>();
        foreach (var node in document.AllNodes.Where(n => n.Kind == ConfigNodeKind.Directive))
        {
            var source = document.GetText(node).Trim();
            var args = source.Length > node.Name.Length
                ? source[node.Name.Length..].Trim().TrimEnd(';').Trim() : "";
            var name = node.Name;
            string? message = null;
            if (document.Engine == ServerEngine.Nginx)
            {
                if (name == "autoindex" && args == "on")
                    message = "Directory listing enabled. Confirm that listed files are intended to be public.";
                else if (name == "server_tokens" && args == "on")
                    message = "Server version may be exposed in headers/error pages.";
            }
            else
            {
                if (name.Equals("Options", StringComparison.OrdinalIgnoreCase) &&
                    args.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Any(value => value.Equals("Indexes", StringComparison.OrdinalIgnoreCase) ||
                                      value.Equals("+Indexes", StringComparison.OrdinalIgnoreCase)))
                    message = "Directory listing enabled. Confirm intended public access.";
                else if (name.Equals("ServerTokens", StringComparison.OrdinalIgnoreCase) &&
                    args.Equals("Full", StringComparison.OrdinalIgnoreCase))
                    message = "Detailed Apache version/module tokens may be exposed.";
                else if (name.Equals("AllowOverride", StringComparison.OrdinalIgnoreCase) &&
                    args.Equals("All", StringComparison.OrdinalIgnoreCase))
                    message = "Broad .htaccess override authority. Review who can edit served directories.";
            }
            if (message is not null)
                found.Add(new Finding(1 + document.Text[..node.Start].Count(c => c == '\n'),
                    name, message));
        }
        return found;
    }
}
