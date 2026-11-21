using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class GeneratedRouteTests
{
    [Fact]
    public void StreamAndMailServerBlocksCannotReceiveHttpRoutes()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, """
            stream { server { listen 9000; proxy_pass 127.0.0.1:9001; } }
            mail { server { listen 25; protocol smtp; } }
            http { server { listen 80; } }
            """);
        Assert.Empty(doc.Diagnostics);
        Assert.Equal(3, doc.VirtualHosts.Count);
        Assert.False(ConfigBlockEditor.IsHttpVirtualHost(doc, doc.VirtualHosts[0]));
        Assert.False(ConfigBlockEditor.IsHttpVirtualHost(doc, doc.VirtualHosts[1]));
        Assert.True(ConfigBlockEditor.IsHttpVirtualHost(doc, doc.VirtualHosts[2]));
    }

    [Theory]
    [InlineData(ServerEngine.Nginx)]
    [InlineData(ServerEngine.Apache)]
    public void ReverseProxyRouteAddsToExistingHostWithoutReplacingOtherLocations(ServerEngine engine)
    {
        var initial = engine == ServerEngine.Nginx
            ? "http {\nserver {\n    server_name example.org;\n    location /assets/ { root /srv/assets; }\n}\n}\n"
            : "<VirtualHost *:80>\n ServerName example.org\n Alias \"/assets/\" \"/srv/assets/\"\n</VirtualHost>\n";
        var doc = ConfigSyntaxDocument.Parse(engine, initial);
        var options = new HostConfigOptions
        {
            Engine = engine,
            Domain = "example.org",
            UrlPath = "/api/",
            UpstreamUrl = "http://127.0.0.1:3100",
            ResponseHeaders = [new HttpHeader("X-Generator", "route")]
        };
        var snippet = engine == ServerEngine.Nginx
            ? NginxHostGenerator.GenerateReverseProxyRoute(options)
            : ApacheHostGenerator.GenerateReverseProxyRoute(options);
        var updated = ConfigBlockEditor.InsertGeneratedSnippet(doc,
            doc.VirtualHosts.Single(), snippet);
        Assert.Empty(updated.Diagnostics);
        Assert.Single(updated.VirtualHosts);
        Assert.Contains("/assets/", updated.Text);
        Assert.Contains("/api/", updated.Text);
        Assert.Contains("X-Generator", updated.Text);
        Assert.DoesNotContain("server_name example.org;", snippet);
        Assert.DoesNotContain("ServerName example.org", snippet);
    }

    [Theory]
    [InlineData(ServerEngine.Nginx)]
    [InlineData(ServerEngine.Apache)]
    public void PhpHandlerCanBeInsertedIntoExistingHost(ServerEngine engine)
    {
        var initial = engine == ServerEngine.Nginx
            ? "http {\nserver { listen 80; server_name example.org; }\n}\n"
            : "<VirtualHost *:80>\n ServerName example.org\n DocumentRoot \"/srv/www/site\"\n</VirtualHost>\n";
        var doc = ConfigSyntaxDocument.Parse(engine, initial);
        var options = new FastCgiOptions
        {
            Engine = engine,
            DocumentRoot = "/srv/www/site",
            FpmEndpoint = "localhost:9000"
        };
        var snippet = engine == ServerEngine.Nginx
            ? NginxFastCgiGenerator.GenerateHandlerRoute(options)
            : ApacheFastCgiGenerator.GenerateHandlerRoute(options);
        var updated = ConfigBlockEditor.InsertGeneratedSnippet(doc,
            doc.VirtualHosts.Single(), snippet);
        Assert.Empty(updated.Diagnostics);
        Assert.Single(updated.VirtualHosts);
        Assert.Contains("localhost:9000", updated.Text);
        if (engine == ServerEngine.Nginx)
            Assert.Contains("root \"/srv/www/site\";", snippet);
        else
            Assert.Contains("FilesMatch", snippet);
    }
}
