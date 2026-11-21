using System.IO.Compression;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigProjectScaffolderTests
{
    private static ConfigProjectScaffolder.Options Nginx => new() { Engine = ServerEngine.Nginx };
    private static ConfigProjectScaffolder.Options Apache => new() { Engine = ServerEngine.Apache };

    [Fact]
    public void NginxScaffoldIncludesMultipleHttpHostsAndLocalMimeTypes()
    {
        var project = ConfigProjectScaffolder.Create(Nginx,
        [
            new("site-one", "server { listen 80; server_name one.test; }\n"),
            new("site-two", "# untouched\nserver { listen 443 ssl; server_name two.test; }\n")
        ]);

        Assert.Contains("worker_processes auto;", project.Files["nginx.conf"]);
        Assert.Contains("include conf.d/*.conf;", project.Files["nginx.conf"]);
        Assert.Contains("types {", project.Files["mime.types"]);
        Assert.Equal("# untouched\nserver { listen 443 ssl; server_name two.test; }\n",
            project.Files["conf.d/site-two.conf"]);
        var graph = project.AnalyzeIncludes(ServerEngine.Nginx, "nginx.conf");
        Assert.Empty(graph.Issues);
        Assert.Equal(4, graph.VisitedFiles.Count);

        using var stream = new MemoryStream();
        project.WriteZip(stream);
        stream.Position = 0;
        using var zip = new ZipArchive(stream);
        Assert.NotNull(zip.GetEntry("nginx.conf"));
        Assert.NotNull(zip.GetEntry("mime.types"));
        Assert.NotNull(zip.GetEntry("conf.d/site-one.conf"));
    }

    [Fact]
    public void NginxLoadBalancerKeepsUpstreamBesideHttpServer()
    {
        var content = """
            upstream pool {
                server 127.0.0.1:3000;
            }
            server {
                listen 80;
                location / { proxy_pass http://pool; }
            }
            """;
        var project = ConfigProjectScaffolder.Create(Nginx, [new("pool", content)]);
        Assert.Equal(content, project.Files["conf.d/pool.conf"]);
        Assert.Empty(project.AnalyzeIncludes(ServerEngine.Nginx, "nginx.conf").Issues);
    }

    [Fact]
    public void StreamWrapperIsLiftedIntoStreamIncludeWithoutDoubleNesting()
    {
        var project = ConfigProjectScaffolder.Create(Nginx,
        [
            new("http", "server { listen 80; }\n"),
            new("tcp", "# stream comment\nstream {\n    server { listen 9000; proxy_pass 127.0.0.1:9001; }\n}\n")
        ]);

        var root = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, project.Files["nginx.conf"]);
        Assert.Empty(root.Diagnostics);
        Assert.Contains(root.Nodes, n => n.Kind == ConfigNodeKind.Block && n.Name == "http");
        Assert.Contains(root.Nodes, n => n.Kind == ConfigNodeKind.Block && n.Name == "stream");
        Assert.DoesNotContain("stream {", project.Files["stream.d/tcp.conf"]);
        Assert.Contains("server { listen 9000;", project.Files["stream.d/tcp.conf"]);
        Assert.Empty(project.AnalyzeIncludes(ServerEngine.Nginx, "nginx.conf").Issues);
    }

    [Fact]
    public void NumberedPresetsComposeHttpAndStreamIntoDeterministicArchiveEntries()
    {
        var project = ConfigProjectScaffolder.Create(Nginx,
        [
            new("001-proxy", "server { listen 80; server_name proxy.test; }"),
            new("002-static", "server { listen 81; server_name static.test; }"),
            new("003-stream", "stream { server { listen 9000; proxy_pass 127.0.0.1:9001; } }")
        ]);
        Assert.Empty(project.AnalyzeIncludes(ServerEngine.Nginx, "nginx.conf").Issues);
        using var stream = new MemoryStream();
        project.WriteZip(stream);
        stream.Position = 0;
        using var zip = new ZipArchive(stream);
        string[] expected =
        [
            "conf.d/001-proxy.conf", "conf.d/002-static.conf", "mime.types",
            "nginx.conf", "stream.d/003-stream.conf"
        ];
        Assert.Equal(expected, zip.Entries.Select(e => e.FullName).ToArray());
    }

    [Fact]
    public void FastCgiPresetIncludesSelfContainedFastCgiParameterFile()
    {
        var host = """
            server {
                location ~ \.php$ {
                    include fastcgi_params;
                    fastcgi_pass 127.0.0.1:9000;
                }
            }
            """;
        var project = ConfigProjectScaffolder.Create(Nginx, [new("php", host)]);
        Assert.Contains("fastcgi_param SERVER_NAME", project.Files["fastcgi_params"]);
        Assert.Empty(project.AnalyzeIncludes(ServerEngine.Nginx, "nginx.conf").Issues);
    }

    [Fact]
    public void ApacheScaffoldDeduplicatesListenPortsAndIncludesVirtualHosts()
    {
        var project = ConfigProjectScaffolder.Create(Apache,
        [
            new("one", "<VirtualHost *:80>\nServerName one.test\n</VirtualHost>\n"),
            new("two", "<VirtualHost *:80 *:443>\nServerName two.test\n</VirtualHost>\n")
        ]);

        Assert.Contains("ServerRoot \"/etc/httpd\"", project.Files["httpd.conf"]);
        Assert.Contains("Listen 80" + Environment.NewLine, project.Files["httpd.conf"]);
        Assert.Contains("Listen 443" + Environment.NewLine, project.Files["httpd.conf"]);
        Assert.Equal(1, project.Files["httpd.conf"].Split("Listen 80").Length - 1);
        Assert.Equal(1, project.Files["httpd.conf"].Split("Listen 443").Length - 1);
        Assert.Contains("IncludeOptional", project.Files["httpd.conf"]);
        Assert.Empty(project.AnalyzeIncludes(ServerEngine.Apache, "httpd.conf").Issues);
        Assert.Equal(3, project.AnalyzeIncludes(ServerEngine.Apache, "httpd.conf").VisitedFiles.Count);
    }

    [Theory]
    [InlineData("../escape", "server { listen 80; }")]
    [InlineData("dup/name", "server { listen 80; }")]
    [InlineData("site", "http { server { listen 80; } }")]
    [InlineData("site", "events { worker_connections 1024; }")]
    [InlineData("site", "server { listen 80;")]
    public void InvalidNginxFragmentsAreRejected(string name, string snippet)
    {
        Assert.Throws<ArgumentException>(() =>
            ConfigProjectScaffolder.Create(Nginx, [new(name, snippet)]));
    }

    [Fact]
    public void DuplicateCaseInsensitiveFileNamesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => ConfigProjectScaffolder.Create(Nginx,
        [
            new("Site", "server { listen 80; }"),
            new("site", "server { listen 81; }")
        ]));
    }

    [Theory]
    [InlineData("<VirtualHost *:abc>\n</VirtualHost>")]
    [InlineData("<VirtualHost *:0>\n</VirtualHost>")]
    [InlineData("Listen 80\n<VirtualHost *:80>\n</VirtualHost>")]
    public void ApacheScaffoldRejectsNonPortablePortOrGlobalSnippet(string content)
    {
        Assert.Throws<ArgumentException>(() =>
            ConfigProjectScaffolder.Create(Apache, [new("site", content)]));
    }

    [Fact]
    public void ApacheRootAndModulePatternCannotInjectDirectives()
    {
        var host = new ConfigProjectScaffolder.Fragment(
            "site", "<VirtualHost *:80>\nServerName site.test\n</VirtualHost>\n");
        Assert.Throws<ArgumentException>(() => ConfigProjectScaffolder.Create(
            Apache with { ApacheServerRoot = "/etc/httpd\"\nListen 1234" }, [host]));
        Assert.Throws<ArgumentException>(() => ConfigProjectScaffolder.Create(
            Apache with { ApacheModuleInclude = "modules/*.conf\"\nListen 1234" }, [host]));
    }
}
