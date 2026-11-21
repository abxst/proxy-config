using ProxyConfig.Core.Registry;
using ProxyConfig.Core.Validation;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class DirectiveArgumentFormTests
{
    private static readonly DirectiveCatalog Catalog = DirectiveCatalog.LoadEmbedded();

    private static DocumentedDirective Find(string server, string name) =>
        Catalog.Entries.First(e => e.Server == server && e.Name == name);

    [Theory]
    [InlineData("nginx", "gzip", "on", "unexpected")]
    [InlineData("nginx", "autoindex", "off", "enabled")]
    [InlineData("apache", "SSLEngine", "on", "sometimes")]
    [InlineData("nginx", "proxy_http_version", "1.1", "3.0")]
    public void SimpleChoicesProduceTypedInputAndRejectInvalidOptions(
        string server, string name, string accepted, string rejected)
    {
        var form = DirectiveArgumentForm.Infer(Find(server, name));
        Assert.Equal(DirectiveArgumentForm.InputKind.Choice, form.Kind);
        Assert.Null(form.Validate(accepted));
        Assert.NotNull(form.Validate(rejected));
        Assert.Null(form.Validate(rejected, ignoreForm: true));
    }

    [Fact]
    public void SimpleNginxSizeFormRejectsInvalidAndInjectingValues()
    {
        var form = DirectiveArgumentForm.Infer(Find("nginx", "client_max_body_size"));
        Assert.Equal(DirectiveArgumentForm.InputKind.Size, form.Kind);
        Assert.Null(form.Validate("32m"));
        Assert.Null(form.Validate("0"));
        Assert.NotNull(form.Validate("-32m"));
        Assert.NotNull(form.Validate("32m; include /tmp/a"));
    }

    [Fact]
    public void ComplexDirectiveStaysRawWithoutPretendingToValidate()
    {
        var form = DirectiveArgumentForm.Infer(Find("apache", "ProxyPass"));
        Assert.Equal(DirectiveArgumentForm.InputKind.Raw, form.Kind);
        Assert.Null(form.Validate("some unsupported syntax"));
    }
}
