using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigSyntaxTests
{
    private const string NginxSample =
        "# nginx global\r\n" +
        "http {\r\n" +
        "    include \"sites/*.conf\";\r\n" +
        "    server {\r\n" +
        "        listen 80;\r\n" +
        "        server_name example.com; # keep this comment\r\n" +
        "        location /api/ {\r\n" +
        "            proxy_set_header X-Test \"quoted # ; { }\";\r\n" +
        "            proprietary_directive alpha beta;\r\n" +
        "        }\r\n" +
        "    }\r\n" +
        "}\r\n";

    private const string ApacheSample =
        "# global comment\n" +
        "IncludeOptional \"conf.d/*.conf\"\n" +
        "<VirtualHost *:80>\n" +
        "  ServerName example.com\n" +
        "  <IfModule mod_rewrite.c>\n" +
        "    RewriteEngine On\n" +
        "  </IfModule>\n" +
        "  CustomModuleDirective enabled \\\n" +
        "    verbose\n" +
        "</VirtualHost>\n";

    [Fact]
    public void Nginx_ParsesNestedBlocksAndPreservesAllTrivia()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, NginxSample);

        Assert.Empty(doc.Diagnostics);
        Assert.Equal(NginxSample, doc.Text);
        Assert.Single(doc.VirtualHosts);
        Assert.Equal("server", doc.VirtualHosts[0].Name);
        Assert.Contains(doc.AllNodes, n => n.Kind == ConfigNodeKind.Directive &&
            n.Name == "proprietary_directive");
        Assert.Contains(doc.AllNodes, n => n.Kind == ConfigNodeKind.Directive &&
            n.Name == "proxy_set_header");
        Assert.Equal(NginxSample, string.Concat(doc.Nodes.Select(doc.GetText)));
        Assert.True(doc.AllNodes.Where(n => n.Kind == ConfigNodeKind.Block).All(n => n.IsClosed));
    }

    [Fact]
    public void Nginx_ReplaceDirective_OnlyTouchesRequestedSpan()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, NginxSample);
        var directive = doc.AllNodes.Single(n => n.Name == "server_name");
        var result = doc.ReplaceNode(directive, "server_name changed.example;");

        Assert.Empty(result.Diagnostics);
        Assert.Contains("server_name changed.example; # keep this comment", result.Text);
        Assert.Contains("proprietary_directive alpha beta;", result.Text);
        Assert.Equal(NginxSample.Replace("server_name example.com;", "server_name changed.example;"),
            result.Text);
        Assert.Throws<ArgumentException>(() => result.ReplaceNode(directive, "server_name invalid;"));
    }

    [Fact]
    public void Nginx_AppendingHost_UsesHttpContextAndKeepsExistingHosts()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, NginxSample);
        var next = doc.AppendVirtualHost("server {\n    listen 8080;\n    server_name new.example;\n}");

        Assert.Empty(next.Diagnostics);
        Assert.Equal(2, next.VirtualHosts.Count);
        Assert.StartsWith("# nginx global\r\nhttp {", next.Text);
        Assert.Contains("    server {\r\n        listen 8080;", next.Text);
        Assert.EndsWith("}\r\n", next.Text);
    }

    [Fact]
    public void Nginx_UnclosedBracesAndQuotes_GetDiagnosticsWithoutDataLoss()
    {
        const string malformed = "server {\n  listen \"80;\n";
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, malformed);
        Assert.NotEmpty(doc.Diagnostics);
        Assert.Equal(malformed, doc.Text);
        Assert.Equal(malformed, string.Concat(doc.Nodes.Select(doc.GetText)));
    }

    [Fact]
    public void Apache_ParsesContainerAndContinuedDirectives()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, ApacheSample);

        Assert.Empty(doc.Diagnostics);
        Assert.Single(doc.VirtualHosts);
        Assert.Contains(doc.AllNodes, n => n.Kind == ConfigNodeKind.Block && n.Name == "IfModule");
        var custom = doc.AllNodes.Single(n => n.Name == "CustomModuleDirective");
        Assert.Contains("verbose\n", doc.GetText(custom));
        Assert.Equal(ApacheSample, string.Concat(doc.Nodes.Select(doc.GetText)));
    }

    [Fact]
    public void Apache_AppendingMultipleVirtualHosts_PreservesOriginalText()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, ApacheSample);
        var next = doc.AppendVirtualHost("<VirtualHost *:443>\n    ServerName secure.example\n</VirtualHost>");

        Assert.Empty(next.Diagnostics);
        Assert.Equal(2, next.VirtualHosts.Count);
        Assert.StartsWith(ApacheSample, next.Text);
        Assert.Contains("secure.example", next.Text);
    }

    [Fact]
    public void Apache_UnmatchedContainerReportsProblem()
    {
        var text = "<VirtualHost *:80>\n  ServerName missing.example\n</Location>\n";
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, text);
        Assert.Contains(doc.Diagnostics, d => d.Message.Contains("Unmatched"));
        Assert.Contains(doc.Diagnostics, d => d.Message.Contains("Unclosed"));
        Assert.Equal(text, doc.Text);
    }

    [Fact]
    public void Nginx_IncludeGraph_ExpandsGlobAndDetectsCycle()
    {
        var files = new Dictionary<string, string>
        {
            ["/etc/nginx/nginx.conf"] = "http { include sites/*.conf; }",
            ["/etc/nginx/sites/app.conf"] = "server { listen 80; }\ninclude nginx.conf;",
            ["/etc/nginx/sites/static.conf"] = "server { listen 8080; }"
        };
        var graph = ConfigIncludeGraph.Build(ServerEngine.Nginx, "/etc/nginx/nginx.conf", files);

        Assert.Equal(3, graph.Edges.Count);
        Assert.Equal(3, graph.VisitedFiles.Count);
        Assert.Contains(graph.Issues, p => p.Message.Contains("Circular include"));
        Assert.Contains(graph.Edges, edge => edge.To.EndsWith("static.conf"));
    }

    [Fact]
    public void Apache_IncludeGraph_UsesSuppliedServerRootAndOptionalIncludes()
    {
        var files = new Dictionary<string, string>
        {
            ["/etc/httpd/httpd.conf"] = "Include conf.d/host.conf\nIncludeOptional conf.d/missing*.conf\n",
            ["/etc/httpd/conf.d/host.conf"] = "<VirtualHost *:80>\n  ServerName local.example\n</VirtualHost>"
        };
        var graph = ConfigIncludeGraph.Build(ServerEngine.Apache,
            "/etc/httpd/httpd.conf", files, apacheServerRoot: "/etc/httpd");
        Assert.Single(graph.Edges);
        Assert.Empty(graph.Issues);
    }

    [Fact]
    public void MissingRequiredIncludeIsReportedWithoutReadingDisk()
    {
        var graph = ConfigIncludeGraph.Build(ServerEngine.Nginx, "/a/nginx.conf",
            new Dictionary<string, string> { ["/a/nginx.conf"] = "include missing.conf;" });
        Assert.Contains(graph.Issues, d => d.Message.Contains("Missing required include"));
    }
}
