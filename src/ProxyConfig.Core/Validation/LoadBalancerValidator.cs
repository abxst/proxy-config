using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static partial class LoadBalancerValidator
{
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ClusterName();

    [GeneratedRegex("^/[a-zA-Z0-9/_.~%+-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeMountPath();

    public static IReadOnlyList<string> Validate(LoadBalancerOptions options)
    {
        var errors = new List<string>();
        if (!HostConfigValidator.TryGetOrigin("http://" + options.Domain, out _))
            errors.Add("Domain must be a valid hostname without path or credentials.");
        if (options.Domain.Any(c => c is ':' or '/' or '\\' or ';' or '*' or '#'))
            errors.Add("Balancer domain must be a plain hostname.");
        if (options.ListenPort is < 1 or > 65535)
            errors.Add("Listen port must be between 1 and 65535.");
        if (!ClusterName().IsMatch(options.ClusterName))
            errors.Add("Cluster name must begin with a letter and contain letters/digits/_/-.");
        if (!SafeMountPath().IsMatch(options.UrlPath) ||
            options.UrlPath.StartsWith("//", StringComparison.Ordinal) ||
            options.UrlPath.Split('/').Any(segment => segment == ".."))
            errors.Add("Mount path must be a safe absolute URL path.");
        if (options.BackendOrigins.Count is < 2 or > 32)
            errors.Add("Balancer requires 2 to 32 HTTP backends.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var backend in options.BackendOrigins)
        {
            if (!HostConfigValidator.TryGetOrigin(backend, out var origin) ||
                !origin.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                errors.Add($"Invalid HTTP backend origin: {backend}");
            else if (!seen.Add(origin))
                errors.Add($"Duplicate backend origin: {origin}");
        }
        if (options.Engine == ServerEngine.Apache &&
            options.Method != LoadBalancingMethod.RoundRobin)
            errors.Add("This Apache preset supports only request-based round-robin; choose NGINX for least_conn/ip_hash.");
        return errors;
    }

    public static IReadOnlyList<string> ParseBackends(string? lines) =>
        (lines ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length != 0)
            .ToArray();
}
