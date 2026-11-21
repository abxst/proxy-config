using System.IO.Compression;
using System.Text;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Syntax;

/// <summary>
/// Explicitly imported, in-memory files. All paths are relative logical
/// project names; neither includes nor archive export access server paths.
/// </summary>
public sealed class ConfigWorkspace
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConfigEncodingKind> _encodings = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, string> Files => _files;

    public void AddOrUpdate(
        string relativePath, string content, ConfigEncodingKind encoding = ConfigEncodingKind.Utf8)
    {
        var key = NormalizeRelativePath(relativePath);
        _files[key] = content ?? string.Empty;
        _encodings[key] = encoding;
    }

    public bool TryRead(string relativePath, out string content) =>
        _files.TryGetValue(NormalizeRelativePath(relativePath), out content!);

    public ConfigIncludeGraph AnalyzeIncludes(
        ServerEngine engine, string rootFile, string? apacheServerRoot = null) =>
        ConfigIncludeGraph.Build(engine, NormalizeRelativePath(rootFile),
            _files, apacheServerRoot);

    public void WriteZip(Stream destination)
    {
        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true,
            entryNameEncoding: Encoding.UTF8);
        foreach (var pair in _files.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var entry = archive.CreateEntry(pair.Key, CompressionLevel.Optimal);
            using var stream = entry.Open();
            var bytes = ConfigFileCodec.Encode(pair.Value, _encodings[pair.Key]);
            stream.Write(bytes);
        }
    }

    public static string NormalizeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new ArgumentException("Workspace path must not be empty or contain control characters.");
        path = path.Replace('\\', '/');
        if (path.StartsWith('/') || path.Contains(':'))
            throw new ArgumentException("Workspace files must use relative paths.");
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(p => p is "." or ".."))
            throw new ArgumentException("Workspace path must not contain . or .. segments.");
        return string.Join('/', parts);
    }
}
