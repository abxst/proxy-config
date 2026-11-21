using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static class StaticSiteValidator
{
    public static IReadOnlyList<string> Validate(StaticSiteOptions options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Domain))
            errors.Add("Domain is required.");
        else if (ContainsControlCharacters(options.Domain) || options.Domain.Any(char.IsWhiteSpace))
            errors.Add("Domain must not contain whitespace or control characters.");

        if (string.IsNullOrWhiteSpace(options.StaticDirectory))
            errors.Add("Static directory is required.");
        else if (ContainsControlCharacters(options.StaticDirectory))
            errors.Add("Static directory must not contain control characters.");

        if (options.ListenPort is < 1 or > 65535)
            errors.Add("Listen port must be between 1 and 65535.");

        if (string.IsNullOrWhiteSpace(options.UrlPath) || !options.UrlPath.StartsWith('/'))
            errors.Add("URL path must start with '/'.");

        if (ContainsControlCharacters(options.UrlPath))
            errors.Add("URL path must not contain control characters.");

        if (options.IndexFiles.Count == 0 || options.IndexFiles.Any(string.IsNullOrWhiteSpace))
            errors.Add("At least one index file is required.");

        if (options.IndexFiles.Any(ContainsControlCharacters))
            errors.Add("Index file names must not contain control characters.");

        if (options.SpaFallback && options.UrlPath != "/")
            errors.Add("SPA fallback currently requires URL path '/'.");

        return errors;
    }

    private static bool ContainsControlCharacters(string value) =>
        value.Any(c => c is '\r' or '\n' or '\0');
}
