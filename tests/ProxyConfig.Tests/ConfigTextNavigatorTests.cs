using ProxyConfig.Core.Syntax;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigTextNavigatorTests
{
    [Fact]
    public void LineStartsRespectWindowsAndUnixNewlines()
    {
        var source = "first\r\nsecond\nthird";
        Assert.True(ConfigTextNavigator.TryLineStart(source, 1, out var first));
        Assert.True(ConfigTextNavigator.TryLineStart(source, 2, out var second));
        Assert.True(ConfigTextNavigator.TryLineStart(source, 3, out var third));
        Assert.Equal(0, first);
        Assert.Equal(7, second);
        Assert.Equal(14, third);
        Assert.Equal(3, ConfigTextNavigator.LineAt(source, third));
        Assert.False(ConfigTextNavigator.TryLineStart(source, 4, out _));
    }

    [Fact]
    public void SearchNextWrapsAndCanMatchCaseInsensitively()
    {
        var source = "server Foo;\nserver BAR;\nserver foo;\n";
        var first = ConfigTextNavigator.FindNext(source, "foo");
        Assert.NotNull(first);
        Assert.Equal(7, first.Value.Start);
        Assert.Equal(10, first.Value.End);
        var next = ConfigTextNavigator.FindNext(source, "foo", first!.Value.End);
        Assert.NotNull(next);
        Assert.True(next.Value.Start > first.Value.Start);
        var wrap = ConfigTextNavigator.FindNext(source, "foo", source.Length);
        Assert.Equal(first, wrap);
        Assert.Null(ConfigTextNavigator.FindNext(source, "not-found"));
        Assert.Null(ConfigTextNavigator.FindNext(source, ""));
    }

    [Fact]
    public void UnicodeOffsetsMatchTextBoxUtf16Indices()
    {
        var source = "😀\nserver foo;\n";
        Assert.True(ConfigTextNavigator.TryLineStart(source, 2, out var secondLine));
        Assert.Equal(3, secondLine);
        Assert.Equal(2, ConfigTextNavigator.LineAt(source, secondLine));
    }
}
