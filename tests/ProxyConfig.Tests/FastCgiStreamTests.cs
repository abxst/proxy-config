using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class FastCgiStreamTests
{
    [Fact]
    public void NginxFastCgiGeneratesPhpHandlerWithExistingFileGuard()
    {
        var options = new FastCgiOptions();
        var text = NginxFastCgiGenerator.Generate(options);
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, text);
        Assert.Empty(doc.Diagnostics);
        Assert.Contains("fastcgi_pass 127.0.0.1:9000;", text);
        Assert.Contains("try_files $uri =404;", text);
        Assert.Contains("SCRIPT_FILENAME $document_root$fastcgi_script_name;", text);
        Assert.Single(doc.VirtualHosts);
    }

    [Fact]
    public void ApacheFastCgiGeneratesFilesMatchAndDocumentRoot()
    {
        var options = new FastCgiOptions { Engine = ServerEngine.Apache };
        var text = ApacheFastCgiGenerator.Generate(options);
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, text);
        Assert.Empty(doc.Diagnostics);
        Assert.Single(doc.VirtualHosts);
        Assert.Single(doc.AllNodes, n => n.Name == "FilesMatch");
        Assert.Contains("proxy:fcgi://127.0.0.1:9000", text);
    }

    [Theory]
    [InlineData("localhost:9000", "localhost:9000")]
    [InlineData("[::1]:9000", "[::1]:9000")]
    public void TcpEndpointNormalizationHandlesHostnameAndIpv6(string input, string expected)
    {
        Assert.True(SafeTcpEndpoint.TryNormalize(input, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("host")]
    [InlineData("host:0")]
    [InlineData("host:65536")]
    [InlineData("host:9000;include")]
    [InlineData("user@host:9000")]
    [InlineData("host:9000/path")]
    public void InvalidFastCgiEndpointsAreRejected(string input)
    {
        Assert.False(SafeTcpEndpoint.TryNormalize(input, out _));
    }

    [Fact]
    public void NginxStreamGeneratesTcpAndUdpModes()
    {
        var tcp = NginxStreamProxyGenerator.Generate(new StreamProxyOptions());
        var udp = NginxStreamProxyGenerator.Generate(new StreamProxyOptions
        {
            ListenPort = 5353,
            TargetEndpoint = "127.0.0.1:53",
            Udp = true,
            ProxyTimeoutSeconds = 20
        });
        Assert.Empty(ConfigSyntaxDocument.Parse(ServerEngine.Nginx, tcp).Diagnostics);
        Assert.Empty(ConfigSyntaxDocument.Parse(ServerEngine.Nginx, udp).Diagnostics);
        Assert.Contains("listen 9000;", tcp);
        Assert.Contains("listen 5353 udp;", udp);
        Assert.Contains("proxy_pass 127.0.0.1:53;", udp);
    }

    [Fact]
    public void FastCgiRejectsUnsafePathsAndIndexNames()
    {
        var options = new FastCgiOptions
        {
            DocumentRoot = "/srv;include /etc",
            IndexFiles = ["../../bad.php"]
        };
        Assert.NotEmpty(FastCgiValidator.Validate(options));
    }
}
