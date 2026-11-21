using System.Text;

namespace ProxyConfig.Core.Syntax;

public enum ConfigEncodingKind
{
    Utf8,
    Utf8Bom,
    Utf16LittleEndian,
    Utf16BigEndian,
    Utf32LittleEndian,
    Utf32BigEndian
}

public sealed record ConfigImportedFile(
    string Path, string Text, ConfigEncodingKind Encoding = ConfigEncodingKind.Utf8);

/// <summary>
/// Strict BOM-aware text codec. It never replaces malformed byte sequences
/// silently, and re-emits the original encoding and BOM on export.
/// Legacy code pages require an explicit conversion outside this app.
/// </summary>
public static class ConfigFileCodec
{
    public static (string Text, ConfigEncodingKind Encoding) Decode(ReadOnlySpan<byte> bytes)
    {
        var encodingKind = ConfigEncodingKind.Utf8;
        var prefix = 0;
        if (bytes.StartsWith([0x00, 0x00, 0xFE, 0xFF]))
            (encodingKind, prefix) = (ConfigEncodingKind.Utf32BigEndian, 4);
        else if (bytes.StartsWith([0xFF, 0xFE, 0x00, 0x00]))
            (encodingKind, prefix) = (ConfigEncodingKind.Utf32LittleEndian, 4);
        else if (bytes.StartsWith([0xEF, 0xBB, 0xBF]))
            (encodingKind, prefix) = (ConfigEncodingKind.Utf8Bom, 3);
        else if (bytes.StartsWith([0xFE, 0xFF]))
            (encodingKind, prefix) = (ConfigEncodingKind.Utf16BigEndian, 2);
        else if (bytes.StartsWith([0xFF, 0xFE]))
            (encodingKind, prefix) = (ConfigEncodingKind.Utf16LittleEndian, 2);

        try
        {
            return (GetEncoding(encodingKind).GetString(bytes[prefix..]), encodingKind);
        }
        catch (DecoderFallbackException error)
        {
            throw new InvalidDataException(
                "Configuration file has invalid text bytes. Convert it explicitly before importing.", error);
        }
    }

    public static byte[] Encode(string text, ConfigEncodingKind kind)
    {
        var encoding = GetEncoding(kind);
        byte[] preamble = kind == ConfigEncodingKind.Utf8 ? [] : encoding.GetPreamble();
        var body = encoding.GetBytes(text);
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    private static Encoding GetEncoding(ConfigEncodingKind kind) => kind switch
    {
        ConfigEncodingKind.Utf8 => new UTF8Encoding(false, true),
        ConfigEncodingKind.Utf8Bom => new UTF8Encoding(true, true),
        ConfigEncodingKind.Utf16LittleEndian => new UnicodeEncoding(false, true, true),
        ConfigEncodingKind.Utf16BigEndian => new UnicodeEncoding(true, true, true),
        ConfigEncodingKind.Utf32LittleEndian => new UTF32Encoding(false, true, true),
        ConfigEncodingKind.Utf32BigEndian => new UTF32Encoding(true, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
