using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class GrpcRouteTests
{
    [Theory]
    [InlineData(false, "grpc://localhost:50051")]
    [InlineData(true, "grpcs://localhost:50051")]
    public void GrpcCanBeInsertedWithoutReplacingOtherHttpRoutes(bool upstreamTls, string target)
    {
        var options = new GrpcRouteOptions
        {
            UrlPath = "/grpc/",
            UpstreamEndpoint = "localhost:50051",
            UpstreamTls = upstreamTls
        };
        var snippet = NginxGrpcRouteGenerator.Generate(options);
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx,
            "http {\nserver {\nlocation /static/ { root /srv/static; }\n}\n}\n");
        var host = Assert.Single(doc.VirtualHosts);
        Assert.Null(ConfigRouteConflictDetector.FindExactDuplicate(doc, host, snippet));
        var updated = ConfigBlockEditor.InsertGeneratedSnippet(doc, host, snippet);
        Assert.Empty(updated.Diagnostics);
        Assert.Contains(target, updated.Text);
        Assert.Contains("grpc_read_timeout 120s;", updated.Text);
        Assert.Contains("location /static/", updated.Text);
        Assert.Contains("location /grpc/", updated.Text);
    }

    [Theory]
    [InlineData("bad;inject")]
    [InlineData("localhost")]
    [InlineData("localhost:0")]
    [InlineData("localhost:65536")]
    public void UnsafeGrpcBackendsAreRejected(string endpoint)
    {
        Assert.NotEmpty(GrpcRouteValidator.Validate(new GrpcRouteOptions
            { UpstreamEndpoint = endpoint }));
    }

    [Fact]
    public void UnsafeGrpcPathAndTimeoutAreRejected()
    {
        Assert.NotEmpty(GrpcRouteValidator.Validate(new GrpcRouteOptions
        {
            UrlPath = "/../unsafe",
            ReadTimeoutSeconds = 0,
            SendTimeoutSeconds = 4000
        }));
    }
}
