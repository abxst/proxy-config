using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class LoadBalancerTests
{
    private static LoadBalancerOptions Valid(ServerEngine engine) => new()
    {
        Engine = engine,
        Domain = "lb.example.org",
        ListenPort = 8080,
        ClusterName = "app_pool",
        UrlPath = "/api/",
        BackendOrigins = ["http://127.0.0.1:3000", "http://127.0.0.1:3001"],
        StripPrefix = true,
        Method = LoadBalancingMethod.RoundRobin
    };

    [Fact]
    public void NginxGeneratesValidUpstreamAndServerBlocks()
    {
        var options = Valid(ServerEngine.Nginx) with
        {
            Method = LoadBalancingMethod.LeastConnections
        };
        var text = NginxLoadBalancerGenerator.Generate(options);
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, text);

        Assert.Empty(doc.Diagnostics);
        Assert.Single(doc.VirtualHosts);
        Assert.Single(doc.Nodes, n => n.Name == "upstream");
        Assert.Contains("least_conn;", text);
        Assert.Contains("server 127.0.0.1:3000;", text);
        Assert.Contains("proxy_pass http://app_pool/;", text);
    }

    [Fact]
    public void NginxIpHashIsExplicitlyGenerated()
    {
        var text = NginxLoadBalancerGenerator.Generate(Valid(ServerEngine.Nginx) with
        {
            Method = LoadBalancingMethod.ClientIpHash
        });
        Assert.Contains("ip_hash;", text);
        Assert.DoesNotContain("least_conn;", text);
    }

    [Fact]
    public void ApacheGeneratesBalancerContainerAndVirtualHost()
    {
        var text = ApacheLoadBalancerGenerator.Generate(Valid(ServerEngine.Apache));
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, text);

        Assert.Empty(doc.Diagnostics);
        Assert.Single(doc.VirtualHosts);
        Assert.Contains("<Proxy \"balancer://app_pool\">", text);
        Assert.Equal(2, doc.AllNodes.Count(n => n.Name == "BalancerMember"));
        Assert.Contains("ProxyPass \"/api/\" \"balancer://app_pool/\"", text);
    }

    [Theory]
    [InlineData("http://127.0.0.1:3000; include bad.conf")]
    [InlineData("https://example.com:443")]
    [InlineData("http://user@localhost:9000")]
    [InlineData("http://localhost:9000/unexpected")]
    public void UnsafeOrUnsupportedBackendsAreRejected(string origin)
    {
        var options = Valid(ServerEngine.Nginx) with
        {
            BackendOrigins = [origin, "http://127.0.0.1:3001"]
        };
        Assert.NotEmpty(LoadBalancerValidator.Validate(options));
    }

    [Fact]
    public void ApacheDoesNotClaimNginxOnlyAlgorithms()
    {
        var options = Valid(ServerEngine.Apache) with
        {
            Method = LoadBalancingMethod.ClientIpHash
        };
        Assert.NotEmpty(LoadBalancerValidator.Validate(options));
        Assert.Throws<ArgumentException>(() => ApacheLoadBalancerGenerator.Generate(options));
    }

    [Fact]
    public void DuplicateBackendAndInvalidClusterAreRejected()
    {
        var options = Valid(ServerEngine.Nginx) with
        {
            ClusterName = "bad;include",
            BackendOrigins = ["http://localhost:80", "http://localhost:80"]
        };
        Assert.True(LoadBalancerValidator.Validate(options).Count >= 2);
    }
}
