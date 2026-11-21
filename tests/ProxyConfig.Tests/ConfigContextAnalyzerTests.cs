using ProxyConfig.Core.Models;
using ProxyConfig.Core.Registry;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigContextAnalyzerTests
{
    private static readonly DirectiveCatalog Catalog = DirectiveCatalog.LoadEmbedded();

    [Fact]
    public void Nginx_ValidHttpServerAndLocationHaveNoContextWarnings()
    {
        const string source = """
            worker_processes auto;
            http {
                server {
                    listen 80;
                    location /api/ {
                        proxy_pass http://127.0.0.1:3000;
                    }
                }
            }
            """;
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, source);

        Assert.Empty(doc.Diagnostics);
        Assert.Empty(ConfigContextAnalyzer.Analyze(doc, Catalog));
    }

    [Fact]
    public void Nginx_DetectsWrongDirectiveScopes()
    {
        const string source = """
            http {
                worker_processes 4;
                server {
                    location / {
                        listen 8080;
                    }
                    proxy_pass http://127.0.0.1:3000;
                }
            }
            """;
        var warnings = ConfigContextAnalyzer.Analyze(
            ConfigSyntaxDocument.Parse(ServerEngine.Nginx, source), Catalog);

        Assert.Equal(3, warnings.Count);
        Assert.Contains(warnings, w => w.Name == "worker_processes" && w.Scope == "http");
        Assert.Contains(warnings, w => w.Name == "listen" && w.Scope == "location");
        Assert.Contains(warnings, w => w.Name == "proxy_pass" && w.Scope == "server");
    }

    [Fact]
    public void Nginx_StreamServerMatchesItsModuleFamily()
    {
        const string source = """
            stream {
                server {
                    listen 9000;
                    proxy_pass 127.0.0.1:9001;
                }
            }
            """;
        var warnings = ConfigContextAnalyzer.Analyze(
            ConfigSyntaxDocument.Parse(ServerEngine.Nginx, source), Catalog);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Nginx_HomonymousServerDirectiveAndBlockAreDisambiguated()
    {
        const string source = """
            http {
                upstream backend {
                    server 127.0.0.1:8000;
                }
                server {
                    listen 80;
                }
            }
            """;
        Assert.Empty(ConfigContextAnalyzer.Analyze(
            ConfigSyntaxDocument.Parse(ServerEngine.Nginx, source), Catalog));
    }

    [Fact]
    public void Apache_DirectoryContextRecognizesDirectoryAndLocation()
    {
        const string source = """
            Listen 80
            <VirtualHost *:80>
                ServerName example.org
                <Directory "/srv">
                    Require all granted
                </Directory>
                <Location "/public">
                    Require all granted
                </Location>
            </VirtualHost>
            """;
        Assert.Empty(ConfigContextAnalyzer.Analyze(
            ConfigSyntaxDocument.Parse(ServerEngine.Apache, source), Catalog));
    }

    [Fact]
    public void Apache_RejectsKnownWrongScopesWithoutTreatingUnknownAsInvalid()
    {
        const string source = """
            Require all granted
            <VirtualHost *:80>
                Listen 443
                UnknownThirdPartyDirective foo
                <Directory "/srv">
                    ServerName example.org
                </Directory>
            </VirtualHost>
            """;
        var warnings = ConfigContextAnalyzer.Analyze(
            ConfigSyntaxDocument.Parse(ServerEngine.Apache, source), Catalog);

        Assert.Equal(3, warnings.Count);
        Assert.Contains(warnings, w => w.Name == "Require" && w.Scope == "server config");
        Assert.Contains(warnings, w => w.Name == "Listen" && w.Scope == "virtual host");
        Assert.Contains(warnings, w => w.Name == "ServerName" && w.Scope == "directory");
        Assert.DoesNotContain(warnings, w => w.Name == "UnknownThirdPartyDirective");
    }

    [Fact]
    public void Apache_IfModuleInheritsItsSurroundingScope()
    {
        const string source = """
            <VirtualHost *:80>
                <IfModule mod_headers.c>
                    ServerName example.org
                </IfModule>
            </VirtualHost>
            """;
        Assert.Empty(ConfigContextAnalyzer.Analyze(
            ConfigSyntaxDocument.Parse(ServerEngine.Apache, source), Catalog));
    }

    [Fact]
    public void Apache_HtaccessAllowsRequireAtRoot()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, "Require all granted\n");
        Assert.Single(ConfigContextAnalyzer.Analyze(doc, Catalog));
        Assert.Empty(ConfigContextAnalyzer.Analyze(doc, Catalog, ".htaccess"));
    }

    [Fact]
    public void InsertionAdvisoryUsesSelectedParentWithoutRewritingText()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx,
            "http {\n    server {\n        location / {}\n    }\n}\n");
        var location = doc.AllNodes.Single(n => n.Name == "location");
        var proxyPass = Catalog.Entries.Single(e => e.Server == "nginx" &&
            e.Name == "proxy_pass" && e.Module == "ngx_http_proxy_module");
        var listen = Catalog.Entries.First(e => e.Server == "nginx" &&
            e.Name == "listen" && e.Module == "ngx_http_core_module");

        Assert.Equal(ConfigContextAnalyzer.Verdict.Compatible,
            ConfigContextAnalyzer.AssessInsertion(doc, location, proxyPass));
        Assert.Equal(ConfigContextAnalyzer.Verdict.Incompatible,
            ConfigContextAnalyzer.AssessInsertion(doc, location, listen));
    }
}
