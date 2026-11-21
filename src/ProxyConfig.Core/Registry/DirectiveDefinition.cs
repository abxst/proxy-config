namespace ProxyConfig.Core.Registry;

public sealed record DirectiveArgumentDefinition(
    string Name,
    string Type,
    bool Required = true,
    IReadOnlyList<string>? AllowedValues = null);

public sealed record DirectiveDefinition
{
    public required string Id { get; init; }
    public required string Server { get; init; }
    public required string Name { get; init; }
    public required string Module { get; init; }
    public required string Syntax { get; init; }
    public IReadOnlyList<string> Contexts { get; init; } = [];
    public IReadOnlyList<DirectiveArgumentDefinition> Arguments { get; init; } = [];
    public string? DefaultValue { get; init; }
    public string? Since { get; init; }
    public string? Removed { get; init; }
    public string Edition { get; init; } = "oss";
    public IReadOnlyList<string> Platforms { get; init; } = [];
    public required string DocumentationUrl { get; init; }
}
