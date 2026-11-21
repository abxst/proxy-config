using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static partial class FastCgiValidator
{
    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex HostName();

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex IndexName();

    public static IReadOnlyList<string> Validate(FastCgiOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Domain) || !HostName().IsMatch(options.Domain))
            errors.Add("Domain must be a safe hostname.");
        if (options.ListenPort is < 1 or > 65535)
            errors.Add("Listen port must be 1..65535.");
        if (string.IsNullOrWhiteSpace(options.DocumentRoot) ||
            options.DocumentRoot.Any(c => char.IsControl(c) || c is ';' or '{' or '}' or '#') ||
            options.DocumentRoot.Contains("..", StringComparison.Ordinal))
            errors.Add("Document root must be a safe target-server filesystem path.");
        if (!SafeTcpEndpoint.TryNormalize(options.FpmEndpoint, out _))
            errors.Add("PHP-FPM endpoint must be an explicit hostname:port or [IPv6]:port.");
        if (options.IndexFiles.Count is < 1 or > 8 ||
            options.IndexFiles.Any(i => !IndexName().IsMatch(i)))
            errors.Add("Specify 1–8 safe index filenames (e.g. index.php index.html).");
        return errors;
    }
}
