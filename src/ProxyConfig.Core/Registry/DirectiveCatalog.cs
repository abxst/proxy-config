using System.IO.Compression;
using System.Text.Json;

namespace ProxyConfig.Core.Registry;

/// <summary>
/// Read-only, offline reference catalog from official NGINX and Apache indexes.
/// It intentionally preserves module-specific entries with duplicate names.
/// </summary>
public sealed class DirectiveCatalog
{
    private const string EmbeddedResourceName = "ProxyConfig.Core.Registry.catalog.json.gz";

    private DirectiveCatalog(string generatedAtUtc, IReadOnlyList<DocumentedDirective> entries)
    {
        GeneratedAtUtc = generatedAtUtc;
        Entries = entries;
    }

    public string GeneratedAtUtc { get; }
    public IReadOnlyList<DocumentedDirective> Entries { get; }

    public static DirectiveCatalog LoadEmbedded()
    {
        using var resource = typeof(DirectiveCatalog).Assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Missing embedded catalog: {EmbeddedResourceName}");
        using var uncompressed = new GZipStream(resource, CompressionMode.Decompress);
        return Load(uncompressed);
    }

    public static DirectiveCatalog Load(Stream jsonStream)
    {
        using var document = JsonDocument.Parse(jsonStream);
        var root = document.RootElement;
        var entries = new List<DocumentedDirective>();

        foreach (var row in root.GetProperty("entries").EnumerateArray())
        {
            if (row.GetArrayLength() != 11)
                throw new FormatException("Invalid documented directive catalog row.");

            string At(int index) => row[index].GetString() ?? string.Empty;

            entries.Add(new DocumentedDirective(
                At(0), At(1), At(2), At(3), At(4), At(5),
                At(6), At(7), At(8), At(9), At(10)));
        }

        return new DirectiveCatalog(root.GetProperty("generatedAtUtc").GetString() ?? string.Empty,
            entries.AsReadOnly());
    }

    public IReadOnlyList<DocumentedDirective> Search(string server, string? query, bool includeVariables = false)
    {
        var needle = (query ?? string.Empty).Trim();
        return Entries
            .Where(item => item.Server == server ||
                           (includeVariables && server == "nginx" && item.IsVariable))
            .Where(item => needle.Length == 0 ||
                           Contains(item.Name, needle) || Contains(item.Module, needle) ||
                           Contains(item.Syntax, needle) || Contains(item.Context, needle) ||
                           Contains(item.Description, needle))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Module, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool Contains(string value, string query) =>
        value.Contains(query, StringComparison.OrdinalIgnoreCase);
}
