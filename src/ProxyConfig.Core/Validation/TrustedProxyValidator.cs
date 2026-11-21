using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Validation;

public static class TrustedProxyValidator
{
    public static IReadOnlyList<string> Validate(TrustedProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var errors = new List<string>();
        if (options.TrustedProxyAddresses is null ||
            options.TrustedProxyAddresses.Count is < 1 or > 64)
        {
            errors.Add("Supply 1–64 trusted proxy IP addresses or CIDR networks.");
            return errors;
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in options.TrustedProxyAddresses)
        {
            if (!IpAccessValidator.TryNormalizeIpOrCidr(candidate, out var normalized))
            {
                errors.Add($"Invalid trusted proxy address: {candidate}");
                continue;
            }
            if (normalized is "0.0.0.0/0" or "::/0")
                errors.Add("Unrestricted /0 networks cannot be trusted as reverse proxies.");
            if (!seen.Add(normalized))
                errors.Add($"Duplicate trusted proxy address: {normalized}");
        }
        return errors;
    }
}
