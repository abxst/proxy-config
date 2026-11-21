using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ProxyTuningTests
{
    private static HostConfigOptions Proxy(ServerEngine engine) => new()
    {
        Engine = engine,
        Domain = "site.test",
        ListenPort = 80,
        UpstreamUrl = "http://127.0.0.1:3000",
        UrlPath = "/api/",
        UseProxyTuning = true,
        ProxyConnectTimeoutSeconds = 5,
        ProxyReadTimeoutSeconds = 120,
        ProxySendTimeoutSeconds = 30,
        ProxyBuffering = false
    };

    [Fact]
    public void NginxEmitsIndependentConnectionReadSendTimeoutsAndBuffering()
    {
        var options = Proxy(ServerEngine.Nginx);
        var text = NginxHostGenerator.Generate(options);
        Assert.Contains("proxy_connect_timeout 5s;", text);
        Assert.Contains("proxy_read_timeout 120s;", text);
        Assert.Contains("proxy_send_timeout 30s;", text);
        Assert.Contains("proxy_buffering off;", text);

        var route = NginxHostGenerator.GenerateReverseProxyRoute(options);
        Assert.Contains("proxy_buffering off;", route);
        Assert.DoesNotContain("server_name", route);
        Assert.Empty(ConfigSyntaxDocument.Parse(ServerEngine.Nginx, route).Diagnostics);
    }

    [Fact]
    public void NginxWebSocketUsesOneCustomReadTimeoutWithoutDuplicate()
    {
        var tuned = NginxHostGenerator.Generate(
            Proxy(ServerEngine.Nginx) with { WebSocket = true });
        Assert.Equal(1, tuned.Split("proxy_read_timeout ").Length - 1);
        Assert.Contains("proxy_read_timeout 120s;", tuned);

        var defaults = NginxHostGenerator.Generate(
            Proxy(ServerEngine.Nginx) with { WebSocket = true, UseProxyTuning = false });
        Assert.Contains("proxy_read_timeout 3600s;", defaults);
    }

    [Fact]
    public void ApacheUsesDocumentedProxyPassWorkerParameters()
    {
        var options = Proxy(ServerEngine.Apache);
        var text = ApacheHostGenerator.Generate(options);
        Assert.Contains("connectiontimeout=5 timeout=120", text);
        Assert.DoesNotContain("proxy_buffering", text);
        var route = ApacheHostGenerator.GenerateReverseProxyRoute(options);
        Assert.Contains("connectiontimeout=5 timeout=120", route);
        Assert.Empty(ConfigSyntaxDocument.Parse(ServerEngine.Apache, route).Diagnostics);
    }

    [Theory]
    [InlineData(0, 120, 30)]
    [InlineData(76, 120, 30)]
    [InlineData(5, 0, 30)]
    [InlineData(5, 120, 3601)]
    public void InvalidTuningRangesAreRejected(int connect, int read, int send)
    {
        var options = Proxy(ServerEngine.Nginx) with
        {
            ProxyConnectTimeoutSeconds = connect,
            ProxyReadTimeoutSeconds = read,
            ProxySendTimeoutSeconds = send
        };
        Assert.NotEmpty(HostConfigValidator.Validate(options));
    }
}
