using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static partial class GrpcRouteValidator
{
    [GeneratedRegex("^/[A-Za-z0-9/_.~%+-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeRoute();

    public static IReadOnlyList<string> Validate(GrpcRouteOptions options)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(options.UrlPath) ||
            !SafeRoute().IsMatch(options.UrlPath) ||
            options.UrlPath.StartsWith("//", StringComparison.Ordinal) ||
            options.UrlPath.Split('/').Any(s => s == ".."))
            errors.Add("gRPC URL path must start with / and contain safe path characters.");
        if (!SafeTcpEndpoint.TryNormalize(options.UpstreamEndpoint, out _))
            errors.Add("gRPC backend must be hostname:port or [IPv6]:port.");
        if (options.ReadTimeoutSeconds is < 1 or > 3600 ||
            options.SendTimeoutSeconds is < 1 or > 3600)
            errors.Add("gRPC read/send timeouts must each be 1..3600 seconds.");
        return errors;
    }
}
