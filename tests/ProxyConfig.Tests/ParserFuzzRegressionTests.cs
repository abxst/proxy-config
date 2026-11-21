using ProxyConfig.Core.Models;
using ProxyConfig.Core.Syntax;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ParserFuzzRegressionTests
{
    private const string Alphabet = "abcXYZ0123_/;{}#<>\"'\\ \t\r\n$[]:苗µ";

    [Theory]
    [InlineData(ServerEngine.Nginx)]
    [InlineData(ServerEngine.Apache)]
    public void RandomBoundedInputNeverLosesSourceOrProducesOutOfRangeSpans(ServerEngine engine)
    {
        var random = new Random(4312);
        for (var iteration = 0; iteration < 250; iteration++)
        {
            var length = random.Next(0, 250);
            var text = new string(Enumerable.Range(0, length)
                .Select(_ => Alphabet[random.Next(Alphabet.Length)]).ToArray());
            var doc = ConfigSyntaxDocument.Parse(engine, text);
            Assert.Equal(text, doc.Text);

            foreach (var node in doc.AllNodes)
            {
                Assert.InRange(node.Start, 0, text.Length);
                Assert.InRange(node.End, node.Start, text.Length);
                Assert.Equal(text[node.Start..node.End], doc.GetText(node));
                foreach (var child in node.Children)
                {
                    Assert.InRange(child.Start, node.Start, node.End);
                    Assert.InRange(child.End, child.Start, node.End);
                }
            }

            var start = random.Next(text.Length + 1);
            var end = random.Next(start, text.Length + 1);
            const string replacement = "new_value";
            var changed = doc.ReplaceSpan(start, end, replacement);
            Assert.Equal(text[..start] + replacement + text[end..], changed.Text);
        }
    }

    [Fact]
    public void ThousandIndependentHostsAreParsedWithoutDroppingComments()
    {
        var source = string.Join('\n', Enumerable.Range(0, 1_000)
            .Select(i => $"# host {i}\n<VirtualHost *:{10000 + i}>\nServerName s{i}.example\n</VirtualHost>"));
        var doc = ConfigSyntaxDocument.Parse(ServerEngine.Apache, source);
        Assert.Empty(doc.Diagnostics);
        Assert.Equal(1_000, doc.VirtualHosts.Count);
        Assert.Equal(1_000, doc.Nodes.Count(n => n.Kind == ConfigNodeKind.Comment));
        Assert.Equal(source, doc.Text);
    }
}
