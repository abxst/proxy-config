using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigTargetCompatibilityTests
{
    [Fact]
    public void NginxStreamRequiresExplicitModuleAndMinimumVersion()
    {
        var workspace = ConfigProjectScaffolder.Create(
            new ConfigProjectScaffolder.Options { Engine = ServerEngine.Nginx },
            [new("stream", "stream {\n server { listen 9000; proxy_pass 127.0.0.1:9001; }\n}")]);
        var findings = ConfigTargetCompatibility.Analyze(workspace, ServerEngine.Nginx,
            "nginx.conf", new ConfigTargetCompatibility.Profile
            {
                Version = "1.8.1",
                InstalledModules = ["ngx_http_proxy_module"]
            });
        Assert.Contains(findings, f => f.Code == "min-version" &&
            f.Message.Contains("1.9.0", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Code == "missing-module" &&
            f.Message.Contains("ngx_stream_core_module", StringComparison.Ordinal));
        Assert.All(findings, f => Assert.StartsWith("https://nginx.org/", f.DocumentationUrl));
    }

    [Fact]
    public void NginxGrpcUsesDocumentedModuleAndVersionWhenProfileIsProvided()
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate("nginx.conf",
            "http { server { location /grpc { grpc_pass grpc://127.0.0.1:50051; } } }");
        var findings = ConfigTargetCompatibility.Analyze(workspace, ServerEngine.Nginx,
            "nginx.conf", new ConfigTargetCompatibility.Profile
            {
                Version = "1.13.9",
                InstalledModules = []
            });
        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Code == "min-version");
        Assert.Contains(findings, f => f.Code == "missing-module");
    }

    [Fact]
    public void ApacheHttp2AndFastCgiCheckVersionAndModules()
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate("httpd.conf", """
            <VirtualHost *:443>
              Protocols h2 http/1.1
              <FilesMatch "\.php$">
                SetHandler "proxy:fcgi://127.0.0.1:9000"
              </FilesMatch>
            </VirtualHost>
            """);
        var findings = ConfigTargetCompatibility.Analyze(workspace, ServerEngine.Apache,
            "httpd.conf", new ConfigTargetCompatibility.Profile
            {
                Version = "2.4.9",
                InstalledModules = ["mod_ssl"]
            });
        Assert.Equal(4, findings.Count);
        Assert.Contains(findings, f => f.Message.Contains("mod_proxy_fcgi", StringComparison.Ordinal));
        Assert.Contains(findings, f => f.Message.Contains("mod_http2", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownModulesAndVersionDoNotProduceInventedFailures()
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate("nginx.conf", "http { server { fastcgi_pass 127.0.0.1:9000; } }");
        Assert.Empty(ConfigTargetCompatibility.Analyze(workspace, ServerEngine.Nginx,
            "nginx.conf", new ConfigTargetCompatibility.Profile()));
    }

    [Fact]
    public void ApacheModuleIdentifiersAreAcceptedAsAliases()
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate("httpd.conf",
            "<VirtualHost *:443>\nProtocols h2 http/1.1\n</VirtualHost>\n");
        var findings = ConfigTargetCompatibility.Analyze(workspace, ServerEngine.Apache,
            "httpd.conf", new ConfigTargetCompatibility.Profile
            {
                Version = "2.4.62",
                InstalledModules = ["http2_module"]
            });
        Assert.Empty(findings);
    }

    [Fact]
    public void InvalidVersionProducesUserFacingFinding()
    {
        var workspace = new ConfigWorkspace();
        workspace.AddOrUpdate("nginx.conf", "events { worker_connections 128; }");
        var finding = Assert.Single(ConfigTargetCompatibility.Analyze(workspace, ServerEngine.Nginx,
            "nginx.conf", new ConfigTargetCompatibility.Profile { Version = "latest" }));
        Assert.Equal("invalid-target-version", finding.Code);
    }
}
