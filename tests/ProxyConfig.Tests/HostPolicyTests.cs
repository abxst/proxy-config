using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class HostPolicyTests
{
    [Theory]
    [InlineData(ServerEngine.Nginx)]
    [InlineData(ServerEngine.Apache)]
    public void LoggingCanBeAddedToSelectedHost(ServerEngine engine)
    {
        var options = new HostLoggingOptions
        {
            AccessLogPath = "logs/client-access.log",
            ErrorLogPath = "logs/client-error.log",
            ErrorLogLevel = ErrorLogLevel.Warn
        };
        var snippet = engine == ServerEngine.Nginx
            ? NginxHostPolicyGenerator.GenerateLogging(options)
            : ApacheHostPolicyGenerator.GenerateLogging(options);
        var source = engine == ServerEngine.Nginx
            ? "http { server { listen 80; server_name example.org; } }\n"
            : "<VirtualHost *:80>\n ServerName example.org\n</VirtualHost>\n";
        var doc = ConfigSyntaxDocument.Parse(engine, source);
        var updated = ConfigBlockEditor.InsertGeneratedSnippet(
            doc, Assert.Single(doc.VirtualHosts), snippet);
        Assert.Empty(updated.Diagnostics);
        Assert.Single(updated.VirtualHosts);
        Assert.Contains("logs/client-access.log", updated.Text);
        Assert.Contains("logs/client-error.log", updated.Text);
        Assert.Contains(engine == ServerEngine.Nginx ? "access_log" : "CustomLog", updated.Text);
        Assert.Contains(engine == ServerEngine.Nginx ? "error_log" : "ErrorLog", updated.Text);
        Assert.Contains(engine == ServerEngine.Nginx ? "warn;" : "LogLevel warn", updated.Text);
    }

    [Theory]
    [InlineData(ServerEngine.Nginx)]
    [InlineData(ServerEngine.Apache)]
    public void AccessDisabledDoesNotGenerateAccessLog(ServerEngine engine)
    {
        var options = new HostLoggingOptions { AccessLogEnabled = false };
        var text = engine == ServerEngine.Nginx
            ? NginxHostPolicyGenerator.GenerateLogging(options)
            : ApacheHostPolicyGenerator.GenerateLogging(options);
        Assert.DoesNotContain(engine == ServerEngine.Nginx ? "access_log" : "CustomLog", text);
    }

    [Fact]
    public void NginxAllowAndDenyListsUseCorrectFirstMatchOrdering()
    {
        var options = new IpAccessOptions
        {
            Mode = IpAccessMode.AllowList,
            Entries = ["10.0.0.0/8", "2001:db8::/32"]
        };
        var allow = NginxHostPolicyGenerator.GenerateIpAccess(options);
        Assert.True(allow.IndexOf("allow 10.0.0.0/8;", StringComparison.Ordinal) <
            allow.IndexOf("deny all;", StringComparison.Ordinal));
        Assert.Contains("allow 2001:db8::/32;", allow);
        var deny = NginxHostPolicyGenerator.GenerateIpAccess(
            options with { Mode = IpAccessMode.DenyList });
        Assert.StartsWith("deny 10.0.0.0/8;", deny);
        Assert.EndsWith("allow all;" + Environment.NewLine, deny);
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx,
            "http { server { listen 80; location /api/ { proxy_pass http://backend; } } }");
        var updated = ConfigBlockEditor.InsertGeneratedSnippet(
            doc, Assert.Single(doc.VirtualHosts), allow);
        Assert.Empty(updated.Diagnostics);
        Assert.Contains("location /api/", updated.Text);
        Assert.Contains("deny all;", updated.Text);
    }

    [Theory]
    [InlineData(IpAccessMode.AllowList, "<RequireAny>", "Require ip 192.168.1.0/24")]
    [InlineData(IpAccessMode.DenyList, "<RequireAll>", "Require not ip 192.168.1.0/24")]
    public void ApacheAclUsesAuthorizationContainers(
        IpAccessMode mode, string container, string expectedRule)
    {
        var options = new IpAccessOptions
        {
            Mode = mode,
            UrlPath = "/private/",
            Entries = ["192.168.1.0/24"]
        };
        var snippet = ApacheHostPolicyGenerator.GenerateIpAccess(options);
        Assert.Contains("<Location \"/private/\">", snippet);
        Assert.Contains(container, snippet);
        Assert.Contains(expectedRule, snippet);
        Assert.Equal(mode == IpAccessMode.DenyList,
            snippet.Contains("Require all granted", StringComparison.Ordinal));
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache,
            "<VirtualHost *:80>\n ServerName example.org\n</VirtualHost>\n");
        var updated = ConfigBlockEditor.InsertGeneratedSnippet(
            doc, Assert.Single(doc.VirtualHosts), snippet);
        Assert.Empty(updated.Diagnostics);
    }

    [Theory]
    [InlineData("127.1")]
    [InlineData("010.0.0.1")]
    [InlineData("10.0.0.0/33")]
    [InlineData("::1/129")]
    [InlineData("::1%eth0")]
    [InlineData("10.0.0.1;deny all")]
    [InlineData("2130706433")]
    public void UnsafeOrAmbiguousNetworkNotationIsRejected(string value)
    {
        Assert.False(IpAccessValidator.TryNormalizeIpOrCidr(value, out _));
    }

    [Theory]
    [InlineData("192.168.1.1", "192.168.1.1")]
    [InlineData("0.0.0.0/0", "0.0.0.0/0")]
    [InlineData("2001:db8::/32", "2001:db8::/32")]
    public void ExplicitIpNetworksAreNormalized(string value, string expected)
    {
        Assert.True(IpAccessValidator.TryNormalizeIpOrCidr(value, out var actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void InjectedLogPathAndDuplicateIpEntriesAreRejected()
    {
        Assert.NotEmpty(HostLoggingValidator.Validate(new HostLoggingOptions
            { ErrorLogPath = "|/bin/sh" }));
        Assert.NotEmpty(HostLoggingValidator.Validate(new HostLoggingOptions
            { AccessLogPath = "logs/a.log;include /etc" }));
        Assert.NotEmpty(IpAccessValidator.Validate(new IpAccessOptions
            { Entries = ["127.0.0.1", "127.0.0.1"] }));
    }
}
