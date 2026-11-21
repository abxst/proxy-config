using ProxyConfig.Core.Syntax;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigTextDiffTests
{
    [Fact]
    public void UnchangedTextHasNoDiff()
    {
        Assert.Equal("No changes from imported source.",
            ConfigTextDiff.Build("server {\n}\n", "server {\n}\n"));
    }

    [Fact]
    public void ModifiedAndAddedDirectiveAreVisible()
    {
        var diff = ConfigTextDiff.Build(
            "server {\n    listen 80;\n}\n",
            "server {\n    listen 443 ssl;\n    server_name example.org;\n}\n");
        Assert.Contains("-     listen 80;", diff);
        Assert.Contains("+     listen 443 ssl;", diff);
        Assert.Contains("+     server_name example.org;", diff);
    }

    [Fact]
    public void HugeDiffIsBounded()
    {
        var original = string.Join('\n', Enumerable.Repeat("a", 1_000));
        var current = string.Join('\n', Enumerable.Repeat("b", 1_000));
        Assert.Contains("safety limit", ConfigTextDiff.Build(original, current));
    }
}
