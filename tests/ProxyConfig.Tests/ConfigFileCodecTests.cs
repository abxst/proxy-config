using System.IO.Compression;
using ProxyConfig.Core.Syntax;
using Xunit;

namespace ProxyConfig.Tests;

public sealed class ConfigFileCodecTests
{
    [Theory]
    [InlineData(ConfigEncodingKind.Utf8)]
    [InlineData(ConfigEncodingKind.Utf8Bom)]
    [InlineData(ConfigEncodingKind.Utf16LittleEndian)]
    [InlineData(ConfigEncodingKind.Utf16BigEndian)]
    [InlineData(ConfigEncodingKind.Utf32LittleEndian)]
    [InlineData(ConfigEncodingKind.Utf32BigEndian)]
    public void AllSupportedEncodingsRoundTripExactly(ConfigEncodingKind kind)
    {
        const string original = "# tiếng Việt\r\nserver_name mẫu.vn;\r\n";
        var encoded = ConfigFileCodec.Encode(original, kind);
        var decoded = ConfigFileCodec.Decode(encoded);

        Assert.Equal(kind, decoded.Encoding);
        Assert.Equal(original, decoded.Text);
        Assert.Equal(encoded, ConfigFileCodec.Encode(decoded.Text, decoded.Encoding));
    }

    [Fact]
    public void InvalidUtf8IsRejectedInsteadOfSilentlyCorruptingText()
    {
        Assert.Throws<InvalidDataException>(() =>
            ConfigFileCodec.Decode([0xC3, 0x28]));
    }

    [Fact]
    public void WorkspaceZipRetainsOriginalBomAndEncoding()
    {
        var workspace = new ConfigWorkspace();
        const string content = "ServerName mẫu.vn\r\n";
        workspace.AddOrUpdate("vhost.conf", content, ConfigEncodingKind.Utf16LittleEndian);
        using var output = new MemoryStream();
        workspace.WriteZip(output);
        output.Position = 0;
        using var zip = new ZipArchive(output, ZipArchiveMode.Read);
        using var stream = zip.GetEntry("vhost.conf")!.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        Assert.Equal(ConfigFileCodec.Encode(content, ConfigEncodingKind.Utf16LittleEndian),
            buffer.ToArray());
    }
}
