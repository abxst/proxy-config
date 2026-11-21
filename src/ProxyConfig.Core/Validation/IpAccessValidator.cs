using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static partial class IpAccessValidator
{
    [GeneratedRegex("^/[A-Za-z0-9/_.~%+-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeLocation();

    [GeneratedRegex("^[0-9]{1,3}(?:\\.[0-9]{1,3}){3}$", RegexOptions.CultureInvariant)]
    private static partial Regex DottedIpv4();

    public static IReadOnlyList<string> ParseEntries(string? source) =>
        (source ?? string.Empty).Split(['\r', '\n', ',', ';'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static IReadOnlyList<string> Validate(IpAccessOptions options)
    {
        var errors = new List<string>();
        if (!Enum.IsDefined(options.Mode))
            errors.Add("Unsupported IP access policy mode.");
        if (options.Entries.Count is < 1 or > 64)
            errors.Add("Specify 1–64 IP addresses or CIDR networks.");
        if (string.IsNullOrWhiteSpace(options.UrlPath) ||
            !SafeLocation().IsMatch(options.UrlPath) ||
            options.UrlPath.StartsWith("//", StringComparison.Ordinal) ||
            options.UrlPath.Split('/').Any(part => part == ".."))
            errors.Add("Apache ACL URL path must start with / and contain safe characters.");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in options.Entries)
        {
            if (!TryNormalizeIpOrCidr(entry, out var normalized))
                errors.Add($"Invalid IP address or CIDR network: {entry}");
            else if (!seen.Add(normalized))
                errors.Add($"Duplicate IP/CIDR entry: {entry}");
        }
        return errors;
    }

    /// <summary>
    /// Accept only explicit dotted-decimal IPv4 or IPv6, optionally followed
    /// by an integer CIDR prefix. Reject alternative IPv4 numeral forms.
    /// </summary>
    public static bool TryNormalizeIpOrCidr(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input) || input.Length > 80 ||
            input.Any(char.IsWhiteSpace) || input.Contains('%') ||
            input.Count(c => c == '/') > 1)
            return false;

        var parts = input.Split('/');
        var sourceIp = parts[0];
        if (sourceIp.Contains(':'))
        {
            if (!IPAddress.TryParse(sourceIp, out var address) ||
                address.AddressFamily != AddressFamily.InterNetworkV6)
                return false;
            if (parts.Length == 2 && (!int.TryParse(parts[1], out var bits) ||
                bits is < 0 or > 128 || parts[1] != bits.ToString()))
                return false;
            normalized = address + (parts.Length == 2 ? "/" + parts[1] : "");
            return true;
        }

        if (!DottedIpv4().IsMatch(sourceIp))
            return false;
        var octets = sourceIp.Split('.');
        if (octets.Any(part => !byte.TryParse(part, out _) ||
            (part.Length > 1 && part[0] == '0')))
            return false;
        if (parts.Length == 2 && (!int.TryParse(parts[1], out var cidr) ||
            cidr is < 0 or > 32 || parts[1] != cidr.ToString()))
            return false;
        normalized = string.Join('.', octets.Select(x => byte.Parse(x).ToString())) +
            (parts.Length == 2 ? "/" + parts[1] : "");
        return true;
    }
}
