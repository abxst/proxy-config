using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigProjectAnalyzerTests
{
    [Fact]
    public void NginxWarnsForExactDuplicateHostnameAndListenerAcrossFiles()
    {
        var project = ConfigProjectScaffolder.Create(
            new ConfigProjectScaffolder.Options { Engine = ServerEngine.Nginx },
            [
                new("first", "server {\n listen 80;\n server_name site.example;\n}\n"),
                new("second", "server {\n listen *:80;\n server_name SITE.EXAMPLE;\n}\n")
            ]);
        var findings = ConfigProjectAnalyzer.Analyze(project, ServerEngine.Nginx, "nginx.conf");

        var duplicate = Assert.Single(findings);
        Assert.Equal("duplicate-host", duplicate.Code);
        Assert.Equal("conf.d/second.conf", duplicate.File);
        Assert.Equal(1, duplicate.Line);
        Assert.Equal("conf.d/first.conf", duplicate.RelatedFile);
        Assert.Equal(1, duplicate.RelatedLine);
    }

    [Fact]
    public void NginxDifferentHostsAndPortsAreNotConflicts()
    {
        var project = ConfigProjectScaffolder.Create(
            new ConfigProjectScaffolder.Options { Engine = ServerEngine.Nginx },
            [
                new("first", "server { listen 80; server_name a.example; }"),
                new("different-name", "server { listen 80; server_name b.example; }"),
                new("different-port", "server { listen 443; server_name a.example; }")
            ]);
        Assert.DoesNotContain(ConfigProjectAnalyzer.Analyze(project,
            ServerEngine.Nginx, "nginx.conf"), f => f.Code == "duplicate-host");
    }

    [Fact]
    public void ApacheDetectsSameAliasOnSameBinding()
    {
        var project = ConfigProjectScaffolder.Create(
            new ConfigProjectScaffolder.Options { Engine = ServerEngine.Apache },
            [
                new("primary", "<VirtualHost *:443>\nServerName a.example\nServerAlias www.example\n</VirtualHost>\n"),
                new("secondary", "<VirtualHost *:443>\nServerName b.example\nServerAlias www.example\n</VirtualHost>\n")
            ]);
        var duplicate = Assert.Single(ConfigProjectAnalyzer.Analyze(
            project, ServerEngine.Apache, "httpd.conf"));
        Assert.Equal("duplicate-host", duplicate.Code);
        Assert.Contains("www.example", duplicate.Message);
    }

    [Fact]
    public void ExcludesUnreachableFilesAndNonHttpStreamHosts()
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate("nginx.conf", """
            events { worker_connections 1024; }
            http { include conf.d/*.conf; }
            stream { include stream.d/*.conf; }
            """);
        workspace.AddOrUpdate("conf.d/site.conf",
            "server { listen 80; server_name a.example; }");
        workspace.AddOrUpdate("stream.d/port.conf",
            "server { listen 80; server_name a.example; }");
        workspace.AddOrUpdate("orphan.conf",
            "server { listen 80; server_name a.example; }");
        Assert.Empty(ConfigProjectAnalyzer.Analyze(workspace,
            ServerEngine.Nginx, "nginx.conf"));
    }

    [Fact]
    public void IncludeAndSyntaxProblemsAreReportedWithSourceFile()
    {
        var project = new ConfigWorkspace();
        project.AddOrUpdate("nginx.conf", "http { include sites/*.conf; include missing.conf; }\n");
        project.AddOrUpdate("sites/bad.conf", "server { listen 80;");
        var issues = ConfigProjectAnalyzer.Analyze(project, ServerEngine.Nginx, "nginx.conf");
        Assert.Contains(issues, f => f.Code == "include" && f.File == "nginx.conf");
        Assert.Contains(issues, f => f.Code == "syntax" && f.File == "sites/bad.conf");
    }
}
