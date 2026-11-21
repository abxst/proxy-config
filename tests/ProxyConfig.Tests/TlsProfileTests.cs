using ProxyConfig.Apache;
using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using ProxyConfig.Nginx;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class TlsProfileTests
{
    [Theory]
    [InlineData(ServerEngine.Nginx, TlsProtocolProfile.Tls12And13, "ssl_protocols TLSv1.2 TLSv1.3;")]
    [InlineData(ServerEngine.Nginx, TlsProtocolProfile.Tls13Only, "ssl_protocols TLSv1.3;")]
    [InlineData(ServerEngine.Apache, TlsProtocolProfile.Tls12And13, "SSLProtocol -all +TLSv1.2 +TLSv1.3")]
    [InlineData(ServerEngine.Apache, TlsProtocolProfile.Tls13Only, "SSLProtocol -all +TLSv1.3")]
    public void TlsHostsEmitSelectedProtocolAllowlist(
        ServerEngine engine, TlsProtocolProfile profile, string directive)
    {
        var options = new HostConfigOptions
        {
            Engine = engine,
            Domain = "example.org",
            EnableTls = true,
            CertificateFile = "/etc/ssl/example.pem",
            PrivateKeyFile = "/etc/ssl/example.key",
            TlsProtocolProfile = profile,
        };
        var text = engine == ServerEngine.Nginx
            ? NginxHostGenerator.Generate(options)
            : ApacheHostGenerator.Generate(options);
        Assert.Contains(directive, text);
        Assert.Empty(ConfigSyntaxDocument.Parse(engine, text).Diagnostics);
    }

    [Fact]
    public void InvalidTlsProfileRejectedWhenTlsEnabled()
    {
        var options = new HostConfigOptions
        {
            Engine = ServerEngine.Nginx,
            Domain = "example.org",
            EnableTls = true,
            CertificateFile = "/etc/ssl/example.pem",
            PrivateKeyFile = "/etc/ssl/example.key",
            TlsProtocolProfile = (TlsProtocolProfile)77
        };
        Assert.Contains(HostConfigValidator.Validate(options),
            error => error.Contains("profile", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(ServerEngine.Nginx)]
    [InlineData(ServerEngine.Apache)]
    public void PlainHttpPresetDoesNotEmitTlsDirectives(ServerEngine engine)
    {
        var options = new HostConfigOptions
        {
            Engine = engine,
            Domain = "example.org",
            EnableTls = false,
            TlsProtocolProfile = TlsProtocolProfile.Tls13Only
        };
        var output = engine == ServerEngine.Nginx
            ? NginxHostGenerator.Generate(options)
            : ApacheHostGenerator.Generate(options);
        Assert.DoesNotContain("ssl_protocols", output);
        Assert.DoesNotContain("SSLProtocol", output);
    }
}
