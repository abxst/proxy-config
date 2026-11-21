namespace ProxyConfig.Core.Registry;

/// <summary>
/// A reference entry extracted from public upstream documentation. Inclusion
/// does not prove that a particular installed server supports this directive.
/// </summary>
public sealed record DocumentedDirective(
    string Server,
    string Name,
    string Module,
    string Syntax,
    string Context,
    string DefaultValue,
    string Description,
    string Status,
    string Override,
    string Compatibility,
    string DocumentationUrl)
{
    public bool IsVariable => Server == "nginx-variable";
    public string ServerLabel => IsVariable ? "NGINX variable" : Server == "nginx" ? "NGINX" : "Apache";
    public string KindLabel => IsVariable ? "Variable" : "Directive";
}
