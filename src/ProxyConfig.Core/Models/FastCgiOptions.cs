namespace ProxyConfig.Core.Models;

public sealed record FastCgiOptions
{
    public ServerEngine Engine { get; init; } = ServerEngine.Nginx;
    public string Domain { get; init; } = "example.com";
    public int ListenPort { get; init; } = 80;
    public TargetPlatform TargetPlatform { get; init; } = TargetPlatform.Linux;
    public string DocumentRoot { get; init; } = "/srv/www/example";
    public string FpmEndpoint { get; init; } = "127.0.0.1:9000";
    public IReadOnlyList<string> IndexFiles { get; init; } = ["index.php", "index.html"];
}
