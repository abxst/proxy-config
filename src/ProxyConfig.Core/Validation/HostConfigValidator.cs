using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static partial class HostConfigValidator
{
    [GeneratedRegex("^(\\*\\.)?[a-zA-Z0-9][a-zA-Z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex DomainRegex();

    [GeneratedRegex("^/[a-zA-Z0-9/_.~%+-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex UrlPathRegex();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeaderNameRegex();

    public static IReadOnlyList<string> Validate(HostConfigOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Domain) || !DomainRegex().IsMatch(options.Domain))
            errors.Add("Domain must be a hostname or a leading *. wildcard.");

        if (options.ListenPort is < 1 or > 65535)
            errors.Add("Listen port must be between 1 and 65535.");

        if (options.EnableTls)
        {
            if (!SafePath(options.CertificateFile))
                errors.Add("TLS certificate file path is required and must not contain control characters or quotes.");
            if (!SafePath(options.PrivateKeyFile))
                errors.Add("TLS private key path is required and must not contain control characters or quotes.");
        }

        ValidateHeaders(options.ResponseHeaders, "Response", errors);

        if (options.Mode == HostConfigMode.ReverseProxy)
        {
            if (!UrlPathRegex().IsMatch(options.UrlPath) ||
                options.UrlPath.Split('/').Any(segment => segment == "..") ||
                options.UrlPath.StartsWith("//", StringComparison.Ordinal))
                errors.Add("Proxy URL path must begin with / and contain only safe path characters.");

            if (!TryGetOrigin(options.UpstreamUrl, out _))
                errors.Add("Upstream must be an absolute http:// or https:// origin without path, query or credentials.");

            ValidateHeaders(options.RequestHeaders, "Request", errors);
        }
        else if (options.Mode == HostConfigMode.Redirect)
        {
            if (!TryGetOrigin(options.RedirectDestination, out _))
                errors.Add("Redirect destination must be an absolute http:// or https:// origin without path, query or credentials.");

            if (options.RedirectStatus is not (301 or 302 or 307 or 308))
                errors.Add("Redirect status must be 301, 302, 307 or 308.");
        }
        else
        {
            errors.Add("Unsupported host configuration mode.");
        }

        return errors;
    }

    /// <summary>Return a safe canonical origin, without a trailing slash.</summary>
    public static bool TryGetOrigin(string? value, out string origin)
    {
        origin = string.Empty;
        if (string.IsNullOrWhiteSpace(value) ||
            value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c is '\"' or '\'' or ';' or '{' or '}') ||
            !Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(parsed.Host) || parsed.UserInfo.Length != 0 ||
            parsed.AbsolutePath != "/" || parsed.Query.Length != 0 || parsed.Fragment.Length != 0)
            return false;

        origin = parsed.GetLeftPart(UriPartial.Authority);
        return true;
    }

    private static void ValidateHeaders(IReadOnlyList<HttpHeader> headers, string category, List<string> errors)
    {
        foreach (var header in headers)
        {
            if (!HeaderNameRegex().IsMatch(header.Name) ||
                header.Value.Any(char.IsControl))
                errors.Add($"{category} header {header.Name} contains invalid characters.");
        }
    }

    private static bool SafePath(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Any(c => char.IsControl(c) || c is '\"' or '\0' or '\n' or '\r');
}
