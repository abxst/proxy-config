using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class TrustedProxyTests
{
    [Theory]
    [InlineData(ServerEngine.Nginx)]
    [InlineData(ServerEngine.Apache)]
    public void GeneratesTrustedClientIpOnlyForExplicitProxyNetworks(ServerEngine engine)
    {
        var options = new TrustedProxyOptions
        {
            TrustedProxyAddresses = ["10.0.0.0/8", "2001:db8::/32"]
        };
        var snippet = engine == ServerEngine.Nginx
            ? NginxHostPolicyGenerator.GenerateTrustedProxies(options)
            : ApacheHostPolicyGenerator.GenerateTrustedProxies(options);
        var source = engine == ServerEngine.Nginx
            ? "http { server { listen 80; server_name site.test; } }\n"
            : "<VirtualHost *:80>\nServerName site.test\n</VirtualHost>\n";
        var doc = ConfigSyntaxDocument.Parse(engine, source);
        var next = ConfigBlockEditor.InsertGeneratedSnippet(
            doc, Assert.Single(doc.VirtualHosts), snippet);

        Assert.Empty(next.Diagnostics);
        Assert.Contains("10.0.0.0/8", next.Text);
        Assert.Contains("2001:db8::/32", next.Text);
        Assert.Contains(engine == ServerEngine.Nginx
            ? "real_ip_header X-Forwarded-For;"
            : "RemoteIPHeader X-Forwarded-For", next.Text);
    }

    [Theory]
    [InlineData("0.0.0.0/0")]
    [InlineData("::/0")]
    [InlineData("127.1")]
    [InlineData("10.0.0.1;proxy_pass evil")]
    public void RejectsUnboundedOrAmbiguousTrustedProxies(string input)
    {
        Assert.NotEmpty(TrustedProxyValidator.Validate(
            new TrustedProxyOptions { TrustedProxyAddresses = [input] }));
    }

    [Fact]
    public void RequiresAtLeastOneExplicitProxyAndNoDuplicates()
    {
        Assert.NotEmpty(TrustedProxyValidator.Validate(new TrustedProxyOptions()));
        Assert.NotEmpty(TrustedProxyValidator.Validate(new TrustedProxyOptions
        {
            TrustedProxyAddresses = ["10.0.0.1", "10.0.0.1"]
        }));
    }

    [Theory]
    [InlineData(ServerEngine.Nginx, "http { set_real_ip_from 10.0.0.0/8; real_ip_header X-Forwarded-For; }",
        "nginx.conf", "ngx_http_realip_module")]
    [InlineData(ServerEngine.Apache, "RemoteIPHeader X-Forwarded-For\nRemoteIPTrustedProxy 10.0.0.0/8",
        "httpd.conf", "mod_remoteip")]
    public void CompatibilityReviewIdentifiesOptionalRealIpModule(
        ServerEngine engine, string source, string path, string module)
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate(path, source);
        var findings = ConfigTargetCompatibility.Analyze(workspace, engine, path,
            new ConfigTargetCompatibility.Profile { InstalledModules = [] });
        Assert.Contains(findings, f =>
            f.Code == "missing-module" && f.Message.Contains(module, StringComparison.Ordinal));
    }
}
