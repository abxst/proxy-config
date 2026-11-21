using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;

namespace ProxyConfig.Apache;

public static class ApacheHostGenerator
{
    /// <summary>Extract a validated proxy route and related directives, without a second VirtualHost.</summary>
    public static string GenerateReverseProxyRoute(HostConfigOptions options)
    {
        if (options.Engine != ServerEngine.Apache || options.Mode != HostConfigMode.ReverseProxy)
            throw new ArgumentException("An Apache reverse-proxy preset is required.", nameof(options));
        var generated = ConfigSyntaxDocument.Parse(ServerEngine.Apache,
            Generate(options with { EnableTls = false }));
        string[] routeNames = ["ProxyRequests", "ProxyPreserveHost", "ProxyPass",
            "ProxyPassReverse", "RequestHeader", "Header"];
        return string.Join(Environment.NewLine, generated.VirtualHosts.Single().Children
            .Where(n => n.Kind == ConfigNodeKind.Directive &&
                routeNames.Contains(n.Name, StringComparer.OrdinalIgnoreCase))
            .Select(generated.GetText)
            .Select(x => x.Trim()));
    }

    public static string Generate(HostConfigOptions options)
    {
        var errors = HostConfigValidator.Validate(options);
        if (errors.Count != 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(options));

        var sb = new StringBuilder();
        sb.AppendLine("# VirtualHost snippet. Required modules depend on enabled options.");
        if (options.Mode == HostConfigMode.ReverseProxy)
            sb.AppendLine("# Requires mod_proxy and mod_proxy_http.");
        if (options.EnableTls)
            sb.AppendLine("# Requires mod_ssl.");
        if (options.RequestHeaders.Count != 0 || options.ResponseHeaders.Count != 0)
            sb.AppendLine("# Custom headers require mod_headers.");

        sb.AppendLine($"<VirtualHost *:{options.ListenPort}>");
        sb.AppendLine($"    ServerName {options.Domain}");

        if (options.EnableTls)
        {
            sb.AppendLine("    SSLEngine On");
            sb.AppendLine($"    SSLCertificateFile {QuotePath(options.CertificateFile, options.TargetPlatform)}");
            sb.AppendLine($"    SSLCertificateKeyFile {QuotePath(options.PrivateKeyFile, options.TargetPlatform)}");
        }

        sb.AppendLine();
        if (options.Mode == HostConfigMode.Redirect)
        {
            HostConfigValidator.TryGetOrigin(options.RedirectDestination, out var destination);
            sb.AppendLine($"    Redirect {options.RedirectStatus} \"/\" \"{destination}/\"");
        }
        else
        {
            HostConfigValidator.TryGetOrigin(options.UpstreamUrl, out var upstream);
            var mountPath = NormalizeMountPath(options.UrlPath);
            var destinationPath = options.StripPrefix ? "/" : mountPath;
            var target = upstream + destinationPath;

            sb.AppendLine("    ProxyRequests Off");
            sb.AppendLine($"    ProxyPreserveHost {(options.PreserveHost ? "On" : "Off")}");
            if (options.WebSocket)
                sb.AppendLine("    # WebSocket Upgrade requires Apache HTTP Server 2.4.47 or newer.");
            var upgrade = options.WebSocket ? " upgrade=websocket" : string.Empty;
            sb.AppendLine($"    ProxyPass \"{mountPath}\" \"{target}\"{upgrade}");
            sb.AppendLine($"    ProxyPassReverse \"{mountPath}\" \"{target}\"");

            foreach (var header in options.RequestHeaders)
                sb.AppendLine($"    RequestHeader set {header.Name} {Quote(header.Value)}");
        }

        foreach (var header in options.ResponseHeaders)
            sb.AppendLine($"    Header always set {header.Name} {Quote(header.Value)}");

        sb.AppendLine("</VirtualHost>");
        return sb.ToString();
    }

    private static string NormalizeMountPath(string path) =>
        path == "/" ? "/" : path.TrimEnd('/') + "/";

    private static string QuotePath(string path, TargetPlatform target) =>
        Quote(target == TargetPlatform.Windows ? path.Replace('\\', '/') : path);

    private static string Quote(string input) =>
        "\"" + input.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
