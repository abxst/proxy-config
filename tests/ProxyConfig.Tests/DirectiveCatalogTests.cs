using ProxyConfig.Core.Registry;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class DirectiveCatalogTests
{
    private static readonly Lazy<DirectiveCatalog> Cached = new(DirectiveCatalog.LoadEmbedded);

    [Fact]
    public void OfflineCatalog_CoversCrawledPublicIndexes()
    {
        var catalog = Cached.Value;

        Assert.Equal(1009, catalog.Entries.Count(e => e.Server == "nginx"));
        Assert.Equal(730, catalog.Entries.Count(e => e.Server == "apache"));
        Assert.Equal(258, catalog.Entries.Count(e => e.IsVariable));
        Assert.All(catalog.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Name));
            Assert.False(string.IsNullOrWhiteSpace(e.Module));
            Assert.StartsWith("https://", e.DocumentationUrl);
        });
    }

    [Theory]
    [InlineData("nginx", "root", "ngx_http_core_module")]
    [InlineData("nginx", "proxy_pass", "ngx_http_proxy_module")]
    [InlineData("apache", "DocumentRoot", "core")]
    [InlineData("apache", "ProxyPass", "mod_proxy")]
    public void Search_FindsFamiliarDirectives(string engine, string name, string module)
    {
        var results = Cached.Value.Search(engine, name);

        Assert.Contains(results, e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            && e.Module.Equals(module, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Search_IsCaseInsensitiveAndEngineScoped()
    {
        var results = Cached.Value.Search("apache", "DOCUMENTROOT");

        Assert.Contains(results, e => e.Name == "DocumentRoot");
        Assert.All(results, e => Assert.Equal("apache", e.Server));
    }

    [Fact]
    public void Search_OptionallyIncludesNginxVariables()
    {
        var withoutVariables = Cached.Value.Search("nginx", "$uri");
        var withVariables = Cached.Value.Search("nginx", "$uri", includeVariables: true);

        Assert.DoesNotContain(withoutVariables, e => e.IsVariable);
        Assert.Contains(withVariables, e => e.IsVariable && e.Name == "$uri");
    }

    [Fact]
    public void Catalog_PreservesModuleSpecificNameOverloads()
    {
        var duplicates = Cached.Value.Entries
            .Where(e => e.Server == "nginx")
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        Assert.NotEmpty(duplicates);
    }
}
