using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;

namespace ProxyConfig.Core.Validation;

/// <summary>
/// Opt-in compatibility advisories for a small, verified set of version- and
/// module-dependent features. A blank module inventory means "unknown", not
/// "no modules"; undocumented/third-party directives are never rejected.
/// </summary>
public static class ConfigTargetCompatibility
{
    public sealed record Profile
    {
        public string Version { get; init; } = string.Empty;
        public IReadOnlyCollection<string>? InstalledModules { get; init; }
    }

    public sealed record Finding(string File, int Line, string Code,
        string Message, string DocumentationUrl)
    {
        public string Display => $"{File}:{Line}: {Message}";
    }

    private sealed record Rule(
        string Feature, string Module, Version? MinimumVersion, string DocumentationUrl);

    private static readonly Rule NginxStream = new(
        "NGINX Stream", "ngx_stream_core_module", new Version(1, 9, 0),
        "https://nginx.org/en/docs/stream/ngx_stream_core_module.html");
    private static readonly Rule NginxGrpc = new(
        "NGINX gRPC", "ngx_http_grpc_module", new Version(1, 13, 10),
        "https://nginx.org/en/docs/http/ngx_http_grpc_module.html");
    private static readonly Rule NginxFastCgi = new(
        "NGINX FastCGI", "ngx_http_fastcgi_module", null,
        "https://nginx.org/en/docs/http/ngx_http_fastcgi_module.html");
    private static readonly Rule NginxRealIp = new(
        "NGINX trusted client IP", "ngx_http_realip_module", null,
        "https://nginx.org/en/docs/http/ngx_http_realip_module.html");
    private static readonly Rule ApacheFastCgi = new(
        "Apache FastCGI proxy", "mod_proxy_fcgi", new Version(2, 4, 10),
        "https://httpd.apache.org/docs/2.4/mod/mod_proxy_fcgi.html");
    private static readonly Rule ApacheHttp2 = new(
        "Apache HTTP/2", "mod_http2", new Version(2, 4, 17),
        "https://httpd.apache.org/docs/2.4/mod/mod_http2.html");
    private static readonly Rule ApacheProxy = new(
        "Apache reverse proxy", "mod_proxy", null,
        "https://httpd.apache.org/docs/2.4/mod/mod_proxy.html");
    private static readonly Rule ApacheSsl = new(
        "Apache TLS", "mod_ssl", null,
        "https://httpd.apache.org/docs/2.4/mod/mod_ssl.html");
    private static readonly Rule ApacheRemoteIp = new(
        "Apache trusted client IP", "mod_remoteip", null,
        "https://httpd.apache.org/docs/2.4/mod/mod_remoteip.html");

    public static IReadOnlyList<Finding> Analyze(
        ConfigWorkspace workspace, ServerEngine engine, string rootFile, Profile profile,
        string? apacheServerRoot = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Version.Length > 0 &&
            (!Version.TryParse(profile.Version.Trim(), out _) ||
             profile.Version.Trim().Split('.').Length is < 2 or > 4))
            return [new Finding(rootFile, 1, "invalid-target-version",
                "Target version must be numeric (for example 1.26.2 or 2.4.62).", string.Empty)];

        var version = profile.Version.Length == 0 ? null : Version.Parse(profile.Version.Trim());
        var modules = profile.InstalledModules is null
            ? null : profile.InstalledModules.Select(m => m.Trim())
                .Where(m => m.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var findings = new List<Finding>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        var graph = workspace.AnalyzeIncludes(engine, rootFile, apacheServerRoot);

        foreach (var file in graph.VisitedFiles)
        {
            if (!workspace.TryRead(file, out var source))
                continue;
            var document = ConfigSyntaxDocument.Parse(engine, source);
            foreach (var node in document.AllNodes)
            {
                var rule = SelectRule(document, node);
                if (rule is null)
                    continue;
                var line = 1;
                foreach (var ch in source.AsSpan(0, node.Start))
                    if (ch == '\n')
                        line++;

                void Warn(string code, string message)
                {
                    // Show one summary per feature/file, not the same warning
                    // for each matching route in a large generated project.
                    if (reported.Add(file + "\0" + code + "\0" + rule.Feature))
                        findings.Add(new Finding(file, line, code, message, rule.DocumentationUrl));
                }

                if (modules is not null && !HasModule(modules, rule.Module))
                    Warn("missing-module",
                        $"{rule.Feature} references {rule.Module}, absent from the supplied target module inventory.");
                if (version is not null && rule.MinimumVersion is not null &&
                    version.CompareTo(rule.MinimumVersion) < 0)
                    Warn("min-version",
                        $"{rule.Feature} requires at least {rule.MinimumVersion}, but the supplied target version is {version}.");
            }
        }
        return findings.Take(128).ToArray();
    }

    private static bool HasModule(HashSet<string> modules, string required) =>
        modules.Contains(required) ||
        required.StartsWith("mod_", StringComparison.Ordinal) &&
            modules.Contains(required[4..] + "_module") ||
        required == "ngx_stream_core_module" &&
            (modules.Contains("ngx_stream_module") || modules.Contains("--with-stream")) ||
        required == "ngx_http_realip_module" && modules.Contains("--with-http_realip_module");

    private static Rule? SelectRule(ConfigSyntaxDocument doc, ConfigSyntaxNode node)
    {
        if (doc.Engine == ServerEngine.Nginx)
        {
            if (node.Kind == ConfigNodeKind.Block && node.Name == "stream")
                return NginxStream;
            if (node.Kind == ConfigNodeKind.Directive)
                return node.Name switch
                {
                    "grpc_pass" => NginxGrpc,
                    "fastcgi_pass" => NginxFastCgi,
                    "set_real_ip_from" or "real_ip_header" => NginxRealIp,
                    _ => null
                };
            return null;
        }

        if (node.Kind != ConfigNodeKind.Directive)
            return null;
        if (node.Name.Equals("ProxyPass", StringComparison.OrdinalIgnoreCase) ||
            node.Name.Equals("ProxyPassReverse", StringComparison.OrdinalIgnoreCase))
            return ApacheProxy;
        if (node.Name.Equals("SSLEngine", StringComparison.OrdinalIgnoreCase))
            return ApacheSsl;
        if (node.Name.Equals("RemoteIPHeader", StringComparison.OrdinalIgnoreCase) ||
            node.Name.Equals("RemoteIPTrustedProxy", StringComparison.OrdinalIgnoreCase))
            return ApacheRemoteIp;
        if (node.Name.Equals("SetHandler", StringComparison.OrdinalIgnoreCase) &&
            doc.GetText(node).Contains("proxy:fcgi", StringComparison.OrdinalIgnoreCase))
            return ApacheFastCgi;
        if (node.Name.Equals("Protocols", StringComparison.OrdinalIgnoreCase) &&
            doc.GetText(node).Contains("h2", StringComparison.OrdinalIgnoreCase))
            return ApacheHttp2;
        return null;
    }
}
