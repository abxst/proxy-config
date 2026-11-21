using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigBlockEditorTests
{
    private const string NginxSource =
        "http {\n" +
        "    # preserved before server\n" +
        "    server {\n" +
        "        listen 80;\n" +
        "        server_name example.org;\n" +
        "    }\n" +
        "}\n";

    private const string ApacheSource =
        "# preserve this comment\n" +
        "<VirtualHost *:80>\n" +
        "    ServerName example.org\n" +
        "</VirtualHost>\n";

    [Fact]
    public void Nginx_CreatesSeveralNestedLocationsAndDirectives()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, NginxSource);
        var server = doc.VirtualHosts.Single();
        doc = ConfigBlockEditor.AddBlock(doc, server, "location", "/api/");
        server = doc.VirtualHosts.Single();
        doc = ConfigBlockEditor.AddBlock(doc, server, "location", "/static/");
        var locations = doc.VirtualHosts.Single().Children
            .Where(n => n.Name == "location").ToArray();
        Assert.Equal(2, locations.Length);

        doc = ConfigBlockEditor.AddDirective(doc, locations[0], "proxy_pass", "http://127.0.0.1:3000");
        Assert.Empty(doc.Diagnostics);
        Assert.Contains("proxy_pass http://127.0.0.1:3000;", doc.Text);
    }

    [Fact]
    public void Nginx_DirectiveInsertionRetainsUnknownText()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, NginxSource);
        var server = doc.VirtualHosts.Single();
        var result = ConfigBlockEditor.AddDirective(doc, server, "client_max_body_size", "32m");

        Assert.Empty(result.Diagnostics);
        Assert.Contains("client_max_body_size 32m;", result.Text);
        Assert.Contains("# preserved before server", result.Text);
        Assert.Equal("server", ConfigBlockEditor.GetContext(result, result.VirtualHosts[0].Children
            .Single(n => n.Name == "client_max_body_size")));
    }

    [Fact]
    public void Apache_CreatesMultipleContainersAndPerBlockDirectives()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, ApacheSource);
        var host = doc.VirtualHosts.Single();
        doc = ConfigBlockEditor.AddBlock(doc, host, "Directory", "\"/srv/www\"");
        host = doc.VirtualHosts.Single();
        doc = ConfigBlockEditor.AddBlock(doc, host, "Location", "\"/api\"");
        var directory = doc.AllNodes.Single(n => n.Name == "Directory");
        doc = ConfigBlockEditor.AddDirective(doc, directory, "Require", "all granted");

        Assert.Empty(doc.Diagnostics);
        Assert.Contains("<Directory \"/srv/www\">", doc.Text);
        Assert.Contains("<Location \"/api\">", doc.Text);
        Assert.Contains("Require all granted", doc.Text);
        Assert.Contains("# preserve this comment", doc.Text);
    }

    [Fact]
    public void MultipleTopLevelVirtualHostsAreSupported()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, ApacheSource);
        var updated = ConfigBlockEditor.AddBlock(doc, null, "VirtualHost", "*:8080");

        Assert.Equal(2, updated.VirtualHosts.Count);
        Assert.Empty(updated.Diagnostics);
        Assert.StartsWith(ApacheSource, updated.Text);
    }

    [Fact]
    public void DuplicateAppendsToSameParentAndPreservesComment()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, NginxSource);
        var server = doc.VirtualHosts.Single();
        var node = server.Children.Single(n => n.Name == "listen");
        var duplicated = ConfigBlockEditor.Duplicate(doc, node);

        Assert.Equal(2, duplicated.VirtualHosts.Single().Children.Count(n => n.Name == "listen"));
        Assert.Contains("# preserved before server", duplicated.Text);
    }

    [Fact]
    public void RemoveDoesNotDeleteAdjacentComments()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, ApacheSource);
        var node = doc.AllNodes.Single(n => n.Name == "ServerName");
        var updated = ConfigBlockEditor.Remove(doc, node);

        Assert.Empty(updated.Diagnostics);
        Assert.DoesNotContain("ServerName", updated.Text);
        Assert.Contains("# preserve this comment", updated.Text);
    }

    [Fact]
    public void MoveSwapsNeighboringDirectives()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx,
            "server {\n    listen 80;\n    server_name demo.local;\n}\n");
        var serverName = doc.AllNodes.Single(n => n.Name == "server_name");
        var updated = ConfigBlockEditor.Move(doc, serverName, -1);

        Assert.Empty(updated.Diagnostics);
        Assert.True(updated.Text.IndexOf("server_name", StringComparison.Ordinal) <
            updated.Text.IndexOf("listen", StringComparison.Ordinal));
    }

    [Fact]
    public void MoveRefusesCrossingComments()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx,
            "server {\n    listen 80;\n    # don't detach this comment\n    server_name demo.local;\n}\n");
        Assert.Throws<InvalidOperationException>(() =>
            ConfigBlockEditor.Move(doc, doc.AllNodes.Single(n => n.Name == "server_name"), -1));
    }

    [Theory]
    [InlineData("safe; another on")]
    [InlineData("hello # ignored")]
    [InlineData("hello { bad")]
    [InlineData("hello\ninclude malicious.conf")]
    public void Nginx_RejectsDirectiveInjection(string arguments)
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, "");
        Assert.Throws<ArgumentException>(() =>
            ConfigBlockEditor.AddDirective(doc, null, "add_header", arguments));
    }

    [Fact]
    public void Nginx_AllowsSemicolonInsideQuotedValue()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, "");
        var updated = ConfigBlockEditor.AddDirective(doc, null, "add_header", "X-Sample \"a;b\"");
        Assert.Empty(updated.Diagnostics);
        Assert.Equal("add_header X-Sample \"a;b\";\n", updated.Text);
    }

    [Fact]
    public void InvalidBlockNameAndStaleNodesAreRejected()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, NginxSource);
        Assert.Throws<ArgumentException>(() =>
            ConfigBlockEditor.AddBlock(doc, null, "location; include", "/"));
        var foreign = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, "server {}").VirtualHosts.Single();
        Assert.Throws<ArgumentException>(() => ConfigBlockEditor.Remove(doc, foreign));
    }
}
