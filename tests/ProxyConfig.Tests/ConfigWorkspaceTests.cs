using System.IO.Compression;
using System.Text;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigWorkspaceTests
{
    [Fact]
    public void MultiFileNginxIncludesResolveAndZipRoundTripsUtf8()
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate("nginx.conf", "http {\n include sites/*.conf;\n}\n");
        workspace.AddOrUpdate("sites/one.conf", "server { server_name mẫu.vn; }\n");
        workspace.AddOrUpdate(@"sites\two.conf", "server { listen 8080; }\n");

        var graph = workspace.AnalyzeIncludes(ServerEngine.Nginx, "nginx.conf");
        Assert.Empty(graph.Issues);
        Assert.Equal(2, graph.Edges.Count);
        Assert.Equal(3, graph.VisitedFiles.Count);

        using var output = new MemoryStream();
        workspace.WriteZip(output);
        output.Position = 0;
        using var archive = new ZipArchive(output, ZipArchiveMode.Read, leaveOpen: true);
        Assert.Equal(3, archive.Entries.Count);
        var entry = archive.GetEntry("sites/one.conf");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        Assert.Equal("server { server_name mẫu.vn; }\n", reader.ReadToEnd());
    }

    [Fact]
    public void ApacheRelativeIncludesReportMissingRequiredAndIgnoreOptional()
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate("apache2.conf",
            "Include conf/virtualhosts.conf\nIncludeOptional conf/missing/*.conf\n");
        workspace.AddOrUpdate("conf/virtualhosts.conf",
            "<VirtualHost *:80>\nServerName example.org\n</VirtualHost>\n");

        var graph = workspace.AnalyzeIncludes(ServerEngine.Apache, "apache2.conf");
        Assert.Empty(graph.Issues);
        Assert.Single(graph.Edges);

        workspace.AddOrUpdate("apache2.conf", "Include does-not-exist.conf\n");
        var missing = workspace.AnalyzeIncludes(ServerEngine.Apache, "apache2.conf");
        Assert.Single(missing.Issues);
        Assert.Contains("Missing required", missing.Issues[0].Message);
    }

    [Theory]
    [InlineData("../outside.conf")]
    [InlineData("/etc/nginx.conf")]
    [InlineData(@"C:\Windows\file.conf")]
    [InlineData("sites/../../secret")]
    [InlineData("")]
    public void InvalidOrEscapingArchivePathsAreRejected(string path)
    {
        var workspace = new ConfigWorkspace();
        Assert.Throws<ArgumentException>(() => workspace.AddOrUpdate(path, "anything"));
    }

    [Fact]
    public void UnknownFileIsNotLoadedFromDisk()
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate("nginx.conf", "include outside.conf;\n");
        var graph = workspace.AnalyzeIncludes(ServerEngine.Nginx, "nginx.conf");

        Assert.Single(graph.Issues);
        Assert.Equal("nginx.conf", graph.VisitedFiles.Single());
    }
}
