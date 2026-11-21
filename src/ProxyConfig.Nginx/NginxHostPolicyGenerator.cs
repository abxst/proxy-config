using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Nginx;

public static class NginxHostPolicyGenerator
{
    public static string GenerateLogging(HostLoggingOptions options)
    {
        var errors = HostLoggingValidator.Validate(options);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        var sb = new StringBuilder();
        if (options.AccessLogEnabled)
            sb.AppendLine($"access_log {Quote(Path(options.AccessLogPath, options.TargetPlatform))} combined;");
        sb.AppendLine($"error_log {Quote(Path(options.ErrorLogPath, options.TargetPlatform))} " +
            $"{options.ErrorLogLevel.ToString().ToLowerInvariant()};");
        return sb.ToString();
    }

    public static string GenerateIpAccess(IpAccessOptions options)
    {
        var errors = IpAccessValidator.Validate(options);
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));
        var sb = new StringBuilder();
        foreach (var ip in options.Entries)
        {
            IpAccessValidator.TryNormalizeIpOrCidr(ip, out var normalized);
            sb.AppendLine($"{(options.Mode == IpAccessMode.AllowList ? "allow" : "deny")} {normalized};");
        }
        sb.AppendLine(options.Mode == IpAccessMode.AllowList ? "deny all;" : "allow all;");
        return sb.ToString();
    }

    private static string Path(string input, TargetPlatform target) =>
        target == TargetPlatform.Windows ? input.Replace('\\', '/') : input;

    private static string Quote(string input) => "\"" + input.Replace("\\", "\\\\") + "\"";
}
