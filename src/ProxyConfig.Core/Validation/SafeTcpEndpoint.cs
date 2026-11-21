using System.Text.RegularExpressions;

namespace ProxyConfig.Core.Validation;

/// <summary>Strict host:port endpoint for built-in TCP/FastCGI presets.</summary>
public static partial class SafeTcpEndpoint
{
    [GeneratedRegex(@"^(?:\[[0-9A-Fa-f:.]+\]|[A-Za-z0-9.-]+):[0-9]{1,5}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex EndpointFormat();

    public static bool TryNormalize(string? input, out string endpoint)
    {
        endpoint = string.Empty;
        if (string.IsNullOrWhiteSpace(input) || !EndpointFormat().IsMatch(input) ||
            !Uri.TryCreate("http://" + input, UriKind.Absolute, out var uri) ||
            uri.Port is < 1 or > 65535 || uri.Host.Length == 0)
            return false;
        var host = uri.HostNameType == UriHostNameType.IPv6
            ? "[" + uri.Host.Trim('[', ']') + "]" : uri.Host;
        endpoint = host + ":" + uri.Port;
        return true;
    }
}
