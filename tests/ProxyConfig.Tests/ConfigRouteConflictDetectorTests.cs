using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigRouteConflictDetectorTests
{
    [Fact]
    public void NginxBlocksExactLocationButAllowsUnrelatedPath()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, """
            http {
                server {
                    location /api/ { proxy_pass http://old; }
                }
            }
            """);
        var host = Assert.Single(doc.VirtualHosts);
        var same = "location /api/ { proxy_pass http://new; }";
        var other = "location /metrics/ { proxy_pass http://new; }";
        Assert.NotNull(ConfigRouteConflictDetector.FindExactDuplicate(doc, host, same));
        Assert.Null(ConfigRouteConflictDetector.FindExactDuplicate(doc, host, other));
    }

    [Fact]
    public void ApacheBlocksDuplicateProxyPassButNotUnrelatedPath()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, """
            <VirtualHost *:80>
                ProxyPass "/api/" "http://old/"
                ProxyPassReverse "/api/" "http://old/"
            </VirtualHost>
            """);
        var host = Assert.Single(doc.VirtualHosts);
        Assert.NotNull(ConfigRouteConflictDetector.FindExactDuplicate(doc, host,
            "ProxyPass \"/api/\" \"http://new/\""));
        Assert.Null(ConfigRouteConflictDetector.FindExactDuplicate(doc, host,
            "ProxyPass \"/static/\" \"http://new/\""));
    }

    [Fact]
    public void ApacheAliasMountIsComparedIndependentlyOfDestination()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, """
            <VirtualHost *:80>
                Alias "/files/" "/srv/original/"
            </VirtualHost>
            """);
        Assert.NotNull(ConfigRouteConflictDetector.FindExactDuplicate(doc,
            Assert.Single(doc.VirtualHosts), "Alias \"/files/\" \"/srv/new/\""));
    }

    [Fact]
    public void ApacheLocationAclSelectorIsDetectedBeforeAddingSecondPolicy()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, """
            <VirtualHost *:80>
                <Location "/admin/">
                    Require ip 192.0.2.0/24
                </Location>
            </VirtualHost>
            """);
        Assert.NotNull(ConfigRouteConflictDetector.FindExactDuplicate(doc,
            Assert.Single(doc.VirtualHosts),
            "<Location \"/admin/\">\n<RequireAny>\nRequire ip 10.0.0.0/8\n</RequireAny>\n</Location>"));
    }

    [Theory]
    [InlineData(ServerEngine.Nginx)]
    [InlineData(ServerEngine.Apache)]
    public void PhpHandlerSelectorIsDetectedIfRepeated(ServerEngine engine)
    {
        var source = engine == ServerEngine.Nginx
            ? "server { location ~ \\.php$ { fastcgi_pass localhost:9000; } }"
            : "<VirtualHost *:80>\n<FilesMatch \"\\.php$\">\nSetHandler \"proxy:fcgi://localhost:9000\"\n</FilesMatch>\n</VirtualHost>";
        var snippet = engine == ServerEngine.Nginx
            ? "location ~ \\.php$ { fastcgi_pass localhost:9001; }"
            : "<FilesMatch \"\\.php$\">\nSetHandler \"proxy:fcgi://localhost:9001\"\n</FilesMatch>";
        var doc = ConfigSyntaxDocument.Parse(engine, source);
        Assert.NotNull(ConfigRouteConflictDetector.FindExactDuplicate(doc,
            Assert.Single(doc.VirtualHosts), snippet));
    }
}
