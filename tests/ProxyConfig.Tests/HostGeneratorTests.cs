using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class HostGeneratorTests
{
    [Fact]
    public void Nginx_ReverseProxy_PreservesHostAndForwardsHeaders()
    {
        var config = NginxHostGenerator.Generate(Proxy(ServerEngine.Nginx) with
        {
            UrlPath = "/api/",
            UpstreamUrl = "http://127.0.0.1:3000",
            RequestHeaders = [new HttpHeader("X-Example", "hello world")],
            ResponseHeaders = [new HttpHeader("X-Content-Type-Options", "nosniff")]
        });

        Assert.Contains("location /api/ {", config);
        Assert.Contains("proxy_pass http://127.0.0.1:3000/;", config);
        Assert.Contains("proxy_set_header Host $host;", config);
        Assert.Contains("proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;", config);
        Assert.Contains("proxy_set_header X-Example \"hello world\";", config);
        Assert.Contains("add_header X-Content-Type-Options \"nosniff\" always;", config);
    }

    [Fact]
    public void Nginx_ReverseProxy_CanKeepOriginalPathAndUseWebSockets()
    {
        var config = NginxHostGenerator.Generate(Proxy(ServerEngine.Nginx) with
        {
            UrlPath = "/chat",
            StripPrefix = false,
            WebSocket = true
        });

        Assert.Contains("location /chat/ {", config);
        Assert.Contains("proxy_pass http://127.0.0.1:3000;", config);
        Assert.DoesNotContain("proxy_pass http://127.0.0.1:3000/;", config);
        Assert.Contains("proxy_set_header Upgrade $http_upgrade;", config);
        Assert.Contains("proxy_http_version 1.1;", config);
    }

    [Fact]
    public void Apache_ReverseProxy_MapsPathWithProxyPassReverse()
    {
        var config = ApacheHostGenerator.Generate(Proxy(ServerEngine.Apache) with
        {
            UrlPath = "/api",
            StripPrefix = false,
            PreserveHost = false,
            RequestHeaders = [new HttpHeader("X-Tenant", "blue")],
            ResponseHeaders = [new HttpHeader("X-Frame-Options", "DENY")]
        });

        Assert.Contains("ProxyPass \"/api/\" \"http://127.0.0.1:3000/api/\"", config);
        Assert.Contains("ProxyPassReverse \"/api/\" \"http://127.0.0.1:3000/api/\"", config);
        Assert.Contains("ProxyPreserveHost Off", config);
        Assert.Contains("RequestHeader set X-Tenant \"blue\"", config);
        Assert.Contains("Header always set X-Frame-Options \"DENY\"", config);
    }

    [Fact]
    public void Apache_WebSocket_EnablesProtocolUpgrade()
    {
        var config = ApacheHostGenerator.Generate(Proxy(ServerEngine.Apache) with
        {
            WebSocket = true
        });

        Assert.Contains("upgrade=websocket", config);
        Assert.Contains("2.4.47", config);
    }

    [Theory]
    [InlineData(ServerEngine.Nginx)]
    [InlineData(ServerEngine.Apache)]
    public void Tls_WindowsTarget_NormalizesCertificatePaths(ServerEngine engine)
    {
        var options = Proxy(engine) with
        {
            EnableTls = true,
            ListenPort = 443,
            TargetPlatform = TargetPlatform.Windows,
            CertificateFile = @"C:\certs\server.pem",
            PrivateKeyFile = @"C:\certs\server.key"
        };
        var config = engine == ServerEngine.Nginx
            ? NginxHostGenerator.Generate(options)
            : ApacheHostGenerator.Generate(options);

        Assert.Contains("C:/certs/server.pem", config);
        Assert.Contains("C:/certs/server.key", config);
        Assert.Contains(engine == ServerEngine.Nginx ? "listen 443 ssl;" : "SSLEngine On", config);
    }

    [Fact]
    public void Redirect_Nginx_PreservesPathAndQuery()
    {
        var config = NginxHostGenerator.Generate(Proxy(ServerEngine.Nginx) with
        {
            Mode = HostConfigMode.Redirect,
            RedirectDestination = "https://new.example.org",
            RedirectStatus = 308
        });

        Assert.Contains("return 308 https://new.example.org$request_uri;", config);
        Assert.DoesNotContain("proxy_pass", config);
    }

    [Fact]
    public void Redirect_Apache_UsesNativeRedirect()
    {
        var config = ApacheHostGenerator.Generate(Proxy(ServerEngine.Apache) with
        {
            Mode = HostConfigMode.Redirect,
            RedirectDestination = "https://new.example.org",
            RedirectStatus = 301
        });

        Assert.Contains("Redirect 301 \"/\" \"https://new.example.org/\"", config);
        Assert.DoesNotContain("ProxyPass", config);
    }

    [Theory]
    [InlineData("http://backend.example/secret")]
    [InlineData("http://user:pass@backend.example")]
    [InlineData("http://backend.example?x=1")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://backend.example;include")]
    public void Validator_RejectsInvalidUpstream(string upstream)
    {
        var options = Proxy(ServerEngine.Nginx) with { UpstreamUrl = upstream };
        Assert.Contains(HostConfigValidator.Validate(options), s => s.Contains("Upstream"));
        Assert.Throws<ArgumentException>(() => NginxHostGenerator.Generate(options));
    }

    [Fact]
    public void Validator_RejectsUnsafeDomainAndMount()
    {
        var options = Proxy(ServerEngine.Nginx) with
        {
            Domain = "example.com;include",
            UrlPath = "/api/../admin"
        };
        var errors = HostConfigValidator.Validate(options);
        Assert.Contains(errors, e => e.Contains("Domain"));
        Assert.Contains(errors, e => e.Contains("Proxy URL path"));
    }

    [Fact]
    public void Validator_RejectsTlsWithoutKeys()
    {
        var options = Proxy(ServerEngine.Apache) with { EnableTls = true };
        Assert.Equal(2, HostConfigValidator.Validate(options).Count);
    }

    [Fact]
    public void Validator_RejectsRedirectWithNonOriginDestination()
    {
        var options = Proxy(ServerEngine.Nginx) with
        {
            Mode = HostConfigMode.Redirect,
            RedirectDestination = "https://new.example.org/path"
        };
        Assert.Contains(HostConfigValidator.Validate(options), e => e.Contains("Redirect destination"));
    }

    [Fact]
    public void HeaderParser_AcceptsOneHeaderPerLineAndEscapesQuotesInGenerators()
    {
        var headers = HttpHeaderParser.Parse("X-Info: a \"quoted\" value\nX-Other: enabled", out var errors);
        Assert.Empty(errors);
        Assert.Equal(2, headers.Count);
        var nginx = NginxHostGenerator.Generate(Proxy(ServerEngine.Nginx) with { ResponseHeaders = headers });
        Assert.Contains("X-Info \"a \\\"quoted\\\" value\"", nginx);
    }

    [Fact]
    public void HeaderParser_RejectsMalformedNameAndControlCharacters()
    {
        var headers = HttpHeaderParser.Parse("Bad Header: value\nX-OK: accepted\nX-Bad: line\rbroken", out var errors);
        Assert.Single(headers);
        Assert.Equal("X-OK", headers[0].Name);
        Assert.Equal(2, errors.Count);
    }

    private static HostConfigOptions Proxy(ServerEngine engine) => new()
    {
        Engine = engine,
        Domain = "example.com",
        ListenPort = 80,
        Mode = HostConfigMode.ReverseProxy
    };
}
