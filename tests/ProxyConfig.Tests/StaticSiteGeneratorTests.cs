using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class StaticSiteGeneratorTests
{
    [Fact]
    public void Nginx_RootSite_GeneratesRootAndSpaFallback()
    {
        var options = Create(ServerEngine.Nginx) with
        {
            SpaFallback = true,
            CacheStaticAssets = true,
            Compression = true
        };

        var config = NginxStaticSiteGenerator.Generate(options);

        Assert.Contains("server_name example.com;", config);
        Assert.Contains("root /srv/www/example;", config);
        Assert.Contains("try_files $uri $uri/ /index.html;", config);
        Assert.Contains("expires 30d;", config);
        Assert.Contains("gzip on;", config);
    }

    [Fact]
    public void Nginx_MountedDirectory_UsesAlias()
    {
        var options = Create(ServerEngine.Nginx) with
        {
            UrlPath = "/assets",
            StaticDirectory = "/data/assets"
        };

        var config = NginxStaticSiteGenerator.Generate(options);

        Assert.Contains("location /assets/", config);
        Assert.Contains("alias /data/assets/;", config);
        Assert.DoesNotContain("root /data/assets;", config);
    }

    [Fact]
    public void Apache_RootSite_GeneratesDocumentRootAndDirectory()
    {
        var options = Create(ServerEngine.Apache) with
        {
            DirectoryListing = true,
            SpaFallback = true
        };

        var config = ApacheStaticSiteGenerator.Generate(options);

        Assert.Contains("DocumentRoot \"/srv/www/example\"", config);
        Assert.Contains("<Directory \"/srv/www/example\">", config);
        Assert.Contains("Require all granted", config);
        Assert.Contains("Options +Indexes", config);
        Assert.Contains("FallbackResource /index.html", config);
    }

    [Fact]
    public void Apache_WindowsTarget_NormalizesPathSeparators()
    {
        var options = Create(ServerEngine.Apache) with
        {
            TargetPlatform = TargetPlatform.Windows,
            StaticDirectory = @"C:\Web\Example"
        };

        var config = ApacheStaticSiteGenerator.Generate(options);

        Assert.Contains("DocumentRoot \"C:/Web/Example\"", config);
    }

    [Fact]
    public void Validator_RejectsControlCharacterInjection()
    {
        var options = Create(ServerEngine.Nginx) with
        {
            Domain = "example.com\ninclude bad.conf"
        };

        var errors = StaticSiteValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("control", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validator_RejectsSpaFallbackForMountedPath()
    {
        var options = Create(ServerEngine.Nginx) with
        {
            UrlPath = "/app/",
            SpaFallback = true
        };

        var errors = StaticSiteValidator.Validate(options);

        Assert.Contains(errors, error => error.Contains("SPA fallback", StringComparison.Ordinal));
    }

    private static StaticSiteOptions Create(ServerEngine engine) => new()
    {
        Engine = engine,
        Domain = "example.com",
        StaticDirectory = "/srv/www/example",
        UrlPath = "/",
        ListenPort = 80,
        IndexFiles = ["index.html", "index.htm"]
    };
}
