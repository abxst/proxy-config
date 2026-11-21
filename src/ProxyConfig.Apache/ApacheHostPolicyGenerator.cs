using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Apache;

public static class ApacheHostPolicyGenerator
{
    private const string CombinedFormat =
        "%h %l %u %t \\\"%r\\\" %>s %b \\\"%{Referer}i\\\" \\\"%{User-Agent}i\\\"";

    public static string GenerateLogging(HostLoggingOptions options)
    {
        var errors = HostLoggingValidator.Validate(options);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        var sb = new StringBuilder();
        if (options.AccessLogEnabled)
            sb.AppendLine($"CustomLog {Quote(Path(options.AccessLogPath, options.TargetPlatform))} \"{CombinedFormat}\"");
        sb.AppendLine($"ErrorLog {Quote(Path(options.ErrorLogPath, options.TargetPlatform))}");
        sb.AppendLine($"LogLevel {options.ErrorLogLevel.ToString().ToLowerInvariant()}");
        return sb.ToString();
    }

    /// <summary>Apache Require authorization must appear in a directory/location context.</summary>
    public static string GenerateIpAccess(IpAccessOptions options, bool insideLocation = false)
    {
        var errors = IpAccessValidator.Validate(options);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        var sb = new StringBuilder();
        if (!insideLocation)
            sb.AppendLine($"<Location \"{options.UrlPath}\">");
        var indent = insideLocation ? "" : "    ";
        sb.AppendLine(indent + (options.Mode == IpAccessMode.AllowList ? "<RequireAny>" : "<RequireAll>"));
        if (options.Mode == IpAccessMode.DenyList)
            sb.AppendLine(indent + "    Require all granted");
        foreach (var ip in options.Entries)
        {
            IpAccessValidator.TryNormalizeIpOrCidr(ip, out var normalized);
            sb.AppendLine(indent + "    Require " +
                (options.Mode == IpAccessMode.AllowList ? "ip " : "not ip ") + normalized);
        }
        sb.AppendLine(indent + (options.Mode == IpAccessMode.AllowList ? "</RequireAny>" : "</RequireAll>"));
        if (!insideLocation)
            sb.AppendLine("</Location>");
        return sb.ToString();
    }

    /// <summary>Requires mod_remoteip; valid at server or VirtualHost scope.</summary>
    public static string GenerateTrustedProxies(TrustedProxyOptions options)
    {
        var errors = TrustedProxyValidator.Validate(options);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        var sb = new StringBuilder();
        sb.AppendLine("# Requires mod_remoteip.");
        sb.AppendLine("RemoteIPHeader X-Forwarded-For");
        foreach (var address in options.TrustedProxyAddresses)
        {
            IpAccessValidator.TryNormalizeIpOrCidr(address, out var normalized);
            sb.AppendLine($"RemoteIPTrustedProxy {normalized}");
        }
        return sb.ToString();
    }

    private static string Path(string input, TargetPlatform target) =>
        target == TargetPlatform.Windows ? input.Replace('\\', '/') : input;

    private static string Quote(string input) => "\"" + input.Replace("\\", "\\\\") + "\"";
}
