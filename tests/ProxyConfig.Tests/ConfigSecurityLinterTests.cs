using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using ProxyConfig.Core.Validation;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigSecurityLinterTests
{
    [Fact]
    public void NginxFlagsDirectoryListingAndServerVersionExposure()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Nginx,
            "http {\nserver_tokens on;\nserver {\nlocation / { autoindex on; }\n}\n}\n");
        var findings = ConfigSecurityLinter.Analyze(doc);
        Assert.Equal(2, findings.Count);
        Assert.Contains(findings, f => f.Name == "autoindex");
        Assert.Contains(findings, f => f.Name == "server_tokens");
    }

    [Fact]
    public void ApacheFlagsOnlyExplicitlyEnabledReviewPatterns()
    {
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, """
            ServerTokens Full
            <Directory "/srv/www">
                Options -Indexes
                AllowOverride None
            </Directory>
            <Directory "/srv/public">
                Options +Indexes
                AllowOverride All
            </Directory>
            """);
        var findings = ConfigSecurityLinter.Analyze(doc);
        Assert.Equal(3, findings.Count);
        Assert.Contains(findings, f => f.Name == "Options");
        Assert.Contains(findings, f => f.Name == "AllowOverride");
        Assert.DoesNotContain(findings, f => f.Message.Contains("TRACE"));
    }

    [Fact]
    public void SafeValuesAndUnknownDirectivesDoNotProduceFindings()
    {
        var nginx = ConfigSyntaxDocument.Parse(ServerEngine.Nginx,
            "server { autoindex off; server_tokens off; vendor_option custom; }\n");
        Assert.Empty(ConfigSecurityLinter.Analyze(nginx));
    }

    [Fact]
    public void TlsProtocolReviewFlagsExplicitLegacyVersionsButNotModernAllowlists()
    {
        var nginx = ConfigSyntaxDocument.Parse(ServerEngine.Nginx, """
            http {
                ssl_protocols TLSv1.1 TLSv1.2;
                server {
                    ssl_protocols TLSv1.2 TLSv1.3;
                }
            }
            """);
        var apache = ConfigSyntaxDocument.Parse(ServerEngine.Apache, """
            SSLProtocol -all +TLSv1.2 +TLSv1.3
            <VirtualHost *:443>
                SSLProtocol All
            </VirtualHost>
            """);
        Assert.Single(ConfigSecurityLinter.Analyze(nginx));
        Assert.Single(ConfigSecurityLinter.Analyze(apache));
        Assert.All(ConfigSecurityLinter.Analyze(nginx)
            .Concat(ConfigSecurityLinter.Analyze(apache)),
            warning => Assert.Contains("Legacy", warning.Message));
    }
}
