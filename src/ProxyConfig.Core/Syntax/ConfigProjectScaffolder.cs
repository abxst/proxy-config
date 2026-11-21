using System.Text;
using System.Text.RegularExpressions;
using ProxyConfig.Core.Models;

namespace ProxyConfig.Core.Syntax;

/// <summary>
/// Builds a portable, multi-file configuration scaffold from validated snippets.
/// It never reads local server files, installs modules or claims that a target
/// installation supports the resulting directives.
/// </summary>
public static class ConfigProjectScaffolder
{
    private const int MaxFragments = 128;
    private const int MaxFragmentCharacters = 1024 * 1024;
    private const int MaxProjectCharacters = 16 * 1024 * 1024;

    public sealed record Options
    {
        public required ServerEngine Engine { get; init; }
        public int NginxWorkerConnections { get; init; } = 1024;
        public string ApacheServerRoot { get; init; } = "/etc/httpd";
        public string ApacheModuleInclude { get; init; } = "conf.modules.d/*.conf";
    }

    public sealed record Fragment(string Name, string Text);

    public static ConfigWorkspace Create(Options options, IReadOnlyList<Fragment> fragments)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fragments);
        if (fragments.Count is < 1 or > MaxFragments)
            throw new ArgumentException($"Supply between 1 and {MaxFragments} fragments.", nameof(fragments));

        var files = new ConfigWorkspace();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var httpFiles = new List<string>();
        var streamFiles = new List<string>();
        var apachePorts = new SortedSet<int>();
        var characterCount = 0;
        var needsFastCgi = false;

        foreach (var fragment in fragments)
        {
            if (fragment is null || fragment.Name is null ||
                !Regex.IsMatch(fragment.Name, @"\A[a-zA-Z0-9_-]{1,64}\z", RegexOptions.CultureInvariant) ||
                !names.Add(fragment.Name))
                throw new ArgumentException("Fragment names must be unique, 1–64 ASCII letters, digits, _ or -.");
            if (string.IsNullOrWhiteSpace(fragment.Text) || fragment.Text.Length > MaxFragmentCharacters)
                throw new ArgumentException($"Fragment '{fragment.Name}' is empty or exceeds 1 MiB.");
            characterCount += fragment.Text.Length;
            if (characterCount > MaxProjectCharacters)
                throw new ArgumentException("Project source exceeds 16 MiB.");

            var document = ConfigSyntaxDocument.Parse(options.Engine, fragment.Text);
            if (document.Diagnostics.Count > 0 ||
                document.AllNodes.Any(node => node.Kind == ConfigNodeKind.Raw))
                throw new ArgumentException($"Fragment '{fragment.Name}' contains malformed structural syntax.");

            var significant = document.Nodes.Where(node =>
                node.Kind is not (ConfigNodeKind.Trivia or ConfigNodeKind.Comment)).ToArray();
            if (significant.Length == 0)
                throw new ArgumentException($"Fragment '{fragment.Name}' contains no configuration.");

            if (options.Engine == ServerEngine.Nginx)
            {
                var streamWrapper = significant.Length == 1 &&
                    significant[0].Kind == ConfigNodeKind.Block &&
                    significant[0].Name.Equals("stream", StringComparison.Ordinal);
                if (streamWrapper)
                {
                    var wrapper = significant[0];
                    var inner = document.Text[wrapper.OpenEnd..wrapper.CloseStart];
                    if (!wrapper.Children.Any(node => node.Kind is ConfigNodeKind.Block or ConfigNodeKind.Directive))
                        throw new ArgumentException($"Stream fragment '{fragment.Name}' has no directives.");
                    var content = document.Text[..wrapper.Start] + inner + document.Text[wrapper.End..];
                    var path = $"stream.d/{fragment.Name}.conf";
                    files.AddOrUpdate(path, content.Trim('\r', '\n') + "\n");
                    streamFiles.Add(path);
                    continue;
                }

                if (significant.Any(node =>
                    node.Kind == ConfigNodeKind.Block &&
                    (node.Name is "http" or "events" or "mail" or "stream")) ||
                    significant.Any(node => node.Kind == ConfigNodeKind.Directive &&
                    (node.Name is "worker_processes" or "pid" or "daemon" or "load_module")))
                    throw new ArgumentException($"Fragment '{fragment.Name}' contains root-level NGINX settings. Supply HTTP-context snippets or a single stream block.");
                if (significant.Any(node => node.Kind == ConfigNodeKind.Block &&
                    node.Name is not ("server" or "upstream" or "map" or "geo" or "split_clients" or "limit_except")))
                    throw new ArgumentException($"Fragment '{fragment.Name}' contains a block whose HTTP context is not supported by the scaffold.");
                var httpPath = $"conf.d/{fragment.Name}.conf";
                files.AddOrUpdate(httpPath, fragment.Text);
                httpFiles.Add(httpPath);
                needsFastCgi |= document.AllNodes.Any(node => node.Kind == ConfigNodeKind.Directive &&
                    node.Name == "include" && document.GetText(node).Trim() == "include fastcgi_params;");
                continue;
            }

            if (options.Engine != ServerEngine.Apache)
                throw new ArgumentOutOfRangeException(nameof(options), "Unsupported server engine.");
            if (significant.Any(node => node.Kind != ConfigNodeKind.Block ||
                !node.Name.Equals("VirtualHost", StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Fragment '{fragment.Name}' must contain only Apache VirtualHost blocks and comments.");

            foreach (var host in significant)
            {
                var opening = document.Text[host.Start..host.OpenEnd].Trim();
                if (!opening.StartsWith("<VirtualHost ", StringComparison.OrdinalIgnoreCase) ||
                    !opening.EndsWith('>'))
                    throw new ArgumentException($"Cannot determine listen ports for fragment '{fragment.Name}'.");
                var endpoints = opening["<VirtualHost".Length..^1].Split(
                    (char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (endpoints.Length == 0)
                    throw new ArgumentException($"VirtualHost in '{fragment.Name}' lacks an address.");
                foreach (var endpoint in endpoints)
                {
                    var colon = endpoint.LastIndexOf(':');
                    if (colon <= 0 || !int.TryParse(endpoint[(colon + 1)..], out var port) ||
                        port is < 1 or > 65535)
                        throw new ArgumentException($"VirtualHost address '{endpoint}' needs an explicit TCP port 1–65535.");
                    apachePorts.Add(port);
                }
            }
            files.AddOrUpdate($"sites/{fragment.Name}.conf", fragment.Text);
        }

        if (options.Engine == ServerEngine.Nginx)
        {
            if (options.NginxWorkerConnections is < 1 or > 1048576)
                throw new ArgumentOutOfRangeException(nameof(options), "Worker connections must be 1–1,048,576.");
            var root = new StringBuilder();
            root.AppendLine("# Generated project scaffold; review installed modules and target paths.");
            root.AppendLine("worker_processes auto;");
            root.AppendLine("events {");
            root.AppendLine($"    worker_connections {options.NginxWorkerConnections};");
            root.AppendLine("}");
            if (httpFiles.Count != 0)
            {
                root.AppendLine("http {");
                root.AppendLine("    include mime.types;");
                root.AppendLine("    default_type application/octet-stream;");
                root.AppendLine("    sendfile on;");
                root.AppendLine("    include conf.d/*.conf;");
                root.AppendLine("}");
                files.AddOrUpdate("mime.types", NginxMimeTypes);
            }
            if (streamFiles.Count != 0)
            {
                root.AppendLine("# Requires the NGINX stream module.");
                root.AppendLine("stream {");
                root.AppendLine("    include stream.d/*.conf;");
                root.AppendLine("}");
            }
            if (needsFastCgi)
                files.AddOrUpdate("fastcgi_params", NginxFastCgiParams);
            files.AddOrUpdate("nginx.conf", root.ToString());
        }
        else
        {
            ValidateApacheRoot(options.ApacheServerRoot);
            ValidateApacheModulePattern(options.ApacheModuleInclude);
            var root = new StringBuilder();
            root.AppendLine("# Generated project scaffold; review MPM, module loads and runtime paths.");
            root.AppendLine($"ServerRoot \"{options.ApacheServerRoot}\"");
            root.AppendLine("ServerName localhost");
            if (options.ApacheModuleInclude.Length > 0)
                root.AppendLine($"IncludeOptional \"{options.ApacheModuleInclude}\"");
            foreach (var port in apachePorts)
                root.AppendLine($"Listen {port}");
            root.AppendLine("Include \"sites/*.conf\"");
            files.AddOrUpdate("httpd.conf", root.ToString());
        }

        return files;
    }

    private static void ValidateApacheRoot(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512 ||
            value.IndexOfAny(['"', '\'', '\r', '\n', '#', '<', '>']) >= 0 ||
            !value.StartsWith('/') && !(value.Length > 2 && char.IsLetter(value[0]) &&
                value[1] == ':' && value[2] is '/' or '\\'))
            throw new ArgumentException("Apache ServerRoot must be an absolute, single-line server path without quotes.");
    }

    private static void ValidateApacheModulePattern(string value)
    {
        if (value.Length > 512 || value.IndexOfAny(['"', '\'', '\r', '\n', '#', '<', '>']) >= 0 ||
            value.Contains("..", StringComparison.Ordinal))
            throw new ArgumentException("Apache module IncludeOptional must be a safe, single-line target path.");
    }

    private const string NginxMimeTypes = """
        types {
            text/html html htm;
            text/css css;
            text/plain txt;
            text/xml xml;
            application/javascript js mjs;
            application/json json;
            application/xml atom rss;
            application/wasm wasm;
            application/pdf pdf;
            application/zip zip;
            application/octet-stream bin exe;
            image/avif avif;
            image/gif gif;
            image/jpeg jpeg jpg;
            image/png png;
            image/svg+xml svg;
            image/webp webp;
            image/x-icon ico;
            font/woff woff;
            font/woff2 woff2;
            audio/mpeg mp3;
            video/mp4 mp4;
        }
        """;

    private const string NginxFastCgiParams = """
        fastcgi_param QUERY_STRING $query_string;
        fastcgi_param REQUEST_METHOD $request_method;
        fastcgi_param CONTENT_TYPE $content_type;
        fastcgi_param CONTENT_LENGTH $content_length;
        fastcgi_param REQUEST_URI $request_uri;
        fastcgi_param DOCUMENT_URI $document_uri;
        fastcgi_param DOCUMENT_ROOT $document_root;
        fastcgi_param SERVER_PROTOCOL $server_protocol;
        fastcgi_param REQUEST_SCHEME $scheme;
        fastcgi_param HTTPS $https if_not_empty;
        fastcgi_param GATEWAY_INTERFACE CGI/1.1;
        fastcgi_param SERVER_SOFTWARE nginx/$nginx_version;
        fastcgi_param REMOTE_ADDR $remote_addr;
        fastcgi_param REMOTE_PORT $remote_port;
        fastcgi_param SERVER_ADDR $server_addr;
        fastcgi_param SERVER_PORT $server_port;
        fastcgi_param SERVER_NAME $server_name;
        fastcgi_param REDIRECT_STATUS 200;
        """;
}
