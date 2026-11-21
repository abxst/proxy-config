# Architecture

Proxy Config keeps the UI independent from server-specific syntax.

Desktop UI -> typed project models -> validation -> NGINX / Apache generators.

Import path: NGINX / Apache source -> text-preserving syntax AST -> selected-node replacement, structural multi-block operation or generated-host insertion -> reparsed text -> export.

## Projects

- `ProxyConfig.App`: Avalonia desktop shell, static-site form, offline explorer and Save dialog.
- `ProxyConfig.Core`: shared models, static-site validation and embedded documentation catalog.
- `ProxyConfig.Core.Syntax`: immutable source spans, structural parsers, structural block/directive mutations, Virtual Host insertion and offline include graph.
- `ProxyConfig.Nginx`: NGINX static-site generator.
- `ProxyConfig.Apache`: Apache HTTP Server static-site generator.
- `ProxyConfig.Tests`: regression tests for the generators and catalog.

## Directive documentation catalog

`src/ProxyConfig.Core/Registry/catalog.json.gz` is generated from the official NGINX and Apache documentation indexes. It stores rows with server, name, module, syntax, context, default, description, status, override class, compatibility notes and documentation URL, plus NGINX variable references.

- Compressed and embedded as an assembly resource: no internet connection needed at runtime.
- All entries (including duplicate names in different modules) are preserved.
- The app can search names, modules, syntax, context and descriptions.
- Catalog inclusion is **not** proof of OSS availability or support in any particular server installation.
- Blank source metadata stays blank. Version, edition and platform support are not guessed from missing documentation fields.
- This reference catalog is not the future typed, version-aware validation registry.

## Context advisories

`ConfigContextAnalyzer` walks the imported AST and compares recognized directive
placements against the catalog's documented context lists. It separates NGINX
HTTP/Stream/Mail module families and homonymous block/directive entries; Apache
containers are mapped to server-config, virtual-host, directory and .htaccess
scopes. Apache `IfModule`/`IfDefine`/`IfVersion` inherit their enclosing context.

Advisories do not rewrite text or prevent export. Unknown directives, unusual
containers, absent metadata and ambiguous family matches remain unverified.
The app cannot infer which modules, edition or version are installed on the
target server. Runtime syntax checks against an actual server binary remain
outside the scope of this offline-only app.

`ConfigProjectAnalyzer` reports structural/include errors and **exact** duplicate
HTTP hostname/listener bindings across reachable files, with file/line
references. It does not infer wildcard route overlap or NGINX/Apache routing
precedence; distinct listener addresses remain distinct. NGINX `stream`
hosts are not interpreted as HTTP hosts.

`ConfigTargetCompatibility` accepts an optional target version and an explicitly
supplied module list. Selected upstream-documented minimum versions are
checked for NGINX Stream/gRPC and Apache FastCGI/HTTP2, plus bounded module
checks for proxy, FastCGI and TLS features. A blank module inventory means
**unknown**, not that modules are unavailable. Findings do not block export
or replace target-server syntax checks.

## Multi-file workspace and typed assistance

`ConfigProjectScaffolder` takes one or more named, structurally valid generated
snippets and emits an in-memory `ConfigWorkspace`. NGINX HTTP hosts/upstreams
are included under `http` from `conf.d/*.conf`. The standalone Stream preset is
unwrapped into `stream.d/*.conf` under one root-level `stream` block. Local
`mime.types` and `fastcgi_params` are bundled when referenced. Apache VirtualHosts
are written under `sites/*.conf`; `httpd.conf` contains distinct `Listen` ports
and user-editable `ServerRoot` / optional installed-module configuration.
The UI exports the selected preset as a full-project ZIP and retains the
existing single-snippet export. Core callers can compose multiple fragments.
The generator-pane composer can queue multiple immutable preset snapshots
(across generator pages), remove/reorder them and export only the selected
server engine's queue; with an empty queue, full-project export uses the current
live preview. Exact repeated hostname/listener signatures are refused when
adding a queued preset; numeric filename prefixes preserve queue order for
globbing include directives. Non-exact route overlaps remain advisory.

These files are **scaffolds**, not guaranteed portable drop-in installations:
Apache MPM/module load order, user/group/runtime paths, NGINX dynamic Stream
modules, certificates and deployment locations are all target-dependent.
The builder refuses malformed snippets and obvious root-context mistakes,
but does not replace a real server's `nginx -t` / `httpd -t` check.

`ConfigWorkspace` holds only explicitly imported or user-created logical files,
using relative paths safe for ZIP export. Its include graph evaluates only those
files, never resolving external target-server files on disk. The Avalonia import
dialog accepts multiple selected files; folder import reads configuration-like
extensions under the user-chosen root, skips symbolic links and applies count
and size limits. Each editor tab retains its own file history when switching.

For NGINX, relative `include` targets resolve from the main config's directory
(configuration prefix), even when the directive occurs in a nested include.
Apache uses the specified `ServerRoot` when supplied, otherwise the source
file directory for offline advisory graph analysis.

`DirectiveArgumentForm` derives safe single-field choices, non-negative integers
or simple NGINX sizes only from unambiguous published directive syntax.
Anything more complex stays an editable raw string; typed validation can be
disabled for advanced configurations. `ConfigTextDiff` presents a bounded,
line-based comparison against the initial imported source before export.

## Cross-parent editing and static routes

The import editor's find-next, one-based line jump and AST-to-source commands
use `ConfigTextNavigator` and Avalonia `TextBox` selection/caret APIs. These
methods read only the current source text, handle CRLF/LF and UTF-16 offsets,
and wrap raw text search at EOF. An AST source jump is refused if the imported
text has changed since Parse, avoiding stale offsets.

`ConfigBlockEditor.MoveToParent` removes a selected syntax span, reparses,
finds the destination node in the new snapshot, and inserts the moved fragment
with relative indentation. Moves into self/descendant nodes or those likely
to detach adjacent/inline comments are rejected. `InsertGeneratedSnippet`
handles a structurally valid collection of generated directives/containers.

The Import / Editor Static tab reuses the proven NGINX/Apache static-site
generators, extracts their route-specific children (excluding listen/hostname
directives), and inserts them into the selected existing server/VirtualHost.
The other hosts and directives remain untouched. This does not silently
create, enable or deploy a target server.

## Cross-file moves and encoding integrity

`MoveBetweenDocuments` computes immutable source and destination snapshots
before either is changed. The UI commits both snapshots together; a one-level
undo succeeds only if neither file has changed since the move, preventing loss
of subsequent edits. The two files' local per-file undo stacks are cleared
when a cross-file transaction commits to avoid half-undoing it.

`ConfigFileCodec` detects explicit UTF BOMs and otherwise requires valid UTF-8.
It refuses malformed text bytes instead of inserting Unicode replacement
characters. Each workspace file carries its original Unicode encoding,
which is respected when exporting an individual file or ZIP project.
Legacy 8-bit encodings require explicit conversion outside the app.

## Load balancer preset and security hints

`LoadBalancerValidator` accepts two to 32 distinct, canonical HTTP origin
backends and safe pool/path/host inputs. NGINX outputs an `upstream` and a
`server` in the `http` context; Apache outputs a `VirtualHost` containing a
`<Proxy "balancer://...">` pool and `ProxyPass` routing. NGINX supports
round-robin, least connections and client IP hash. The Apache form deliberately
supports only request-based round-robin, requiring optional proxy and
balancer modules as documented in the generated comments.

`ConfigSecurityLinter` recognizes a small number of explicit, potentially
surprising exposure settings (directory listings, server tokens, .htaccess
override scope). These are review hints, not assertions of exploitable
vulnerabilities, and never rewrite or block configuration exports.

## PHP-FPM/FastCGI and NGINX Stream presets

`FastCgiValidator` checks a hostname, explicit TCP endpoint, target-server
document root, listen port and safe index filenames. NGINX emits a server
snippet with `try_files` to avoid invoking PHP for a missing script; Apache
emits a `VirtualHost` with a `FilesMatch` / `SetHandler` pair. These require
existing PHP-FPM and server modules; no local filesystem or service is probed.
The Apache handler requires `mod_proxy_fcgi` (Apache 2.4.10+) and `mod_proxy`.

`StreamProxyValidator` validates the NGINX stream listen port, TCP/UDP choice,
timeout and target endpoint. The generator emits a root-level `stream` block,
not an HTTP-context `server`. The editor disables generated-host appending for
this preset to avoid corrupting the destination HTTP context. An NGINX build
with the stream module is required. Neither preset attempts to inspect
installed modules or to deploy any configuration.

## Inserting proxy routes and PHP handlers into existing hosts

The Import / Editor Proxy route and PHP handler tabs reuse the same validated
generator models, extract generated location/directive fragments and apply
them through the text-preserving `InsertGeneratedSnippet` operation.
`IsHttpVirtualHost` requires an actual Apache VirtualHost or NGINX `server`
at the root/`http` level; it refuses `server` nodes in `stream` or `mail`
blocks. A PHP handler's NGINX location has its own document root to avoid
modifying the existing server root. Apache keeps the existing VirtualHost
DocumentRoot. Overlapping-path and installed-module validation remain out
of scope; check generated directives before using them on a real server.

## Explicit TLS protocol allowlists

The host preset models have a small `TlsProtocolProfile` choice (TLS 1.2 +
TLS 1.3, or TLS 1.3 only) when TLS is enabled. NGINX writes `ssl_protocols`
and Apache writes `SSLProtocol`, alongside existing certificate/key paths.
The presets do not claim compatibility with all deployed server and crypto
library versions. In NGINX, `ssl_protocols` is applied when the TLS
connection is negotiated; verify the default server's effective settings
when several TLS Virtual Hosts share one listening socket. These profiles
are only protocol allowlists, not complete cipher or PKI hardening policies.

## Exact route-conflict guard and TLS review hints

`ConfigRouteConflictDetector` compares the generated route's explicit selector
against direct children of the selected HTTP VirtualHost. It refuses repeated
NGINX `location` headers, Apache `ProxyPass`/`Alias` mount points, or repeated
Apache `FilesMatch` headers. The comparison is intentionally narrow: it does
not claim to understand overlapping wildcard or regex paths, rewrite order,
or complex inherited routing. No existing config is automatically removed.

`ConfigSecurityLinter` also displays non-blocking warnings for explicit
legacy SSL/TLS protocol selections; ambiguous Apache exclusions are not
interpreted as positive protocol enables. These remain review hints rather
than an exhaustive security audit.

## NGINX gRPC routes

`GrpcRouteValidator` accepts an explicitly typed TCP backend, a safe
location path, a `grpc://` or `grpcs://` scheme choice and bounded read/send
timeouts. `NginxGrpcRouteGenerator` emits only a `location` block with
`grpc_pass`, `grpc_read_timeout` and `grpc_send_timeout`. It can be inserted
into an imported HTTP `server` through the same duplicate-selector guard.
It does not enable HTTP/2 or install modules on the frontend server. The
target NGINX installation must have HTTP/2 and gRPC modules, and the existing
listener must be configured appropriately for gRPC clients. Apache's
`mod_proxy_http2` behavior is distinct and is not presented as equivalent.

## HTTP logging and IP access policy forms

The shared `HostLoggingOptions` / `HostLoggingValidator` and `IpAccessOptions` /
`IpAccessValidator` provide bounded, opt-in logging and IP-only access rules.
NGINX emits `access_log` (built-in combined format), `error_log`, and ordered
`allow`/`deny` directives; Apache emits `CustomLog` with an inline combined
format, `ErrorLog`/`LogLevel`, plus `Require ip` authorization inside a
`Location`. Apache denylisting uses `<RequireAll>` with an affirmative
`Require all granted` and negative `Require not ip` entries. Apache allowlisting
uses `<RequireAny>`, so each address may authorize.

Generated host presets apply these fragments only when requested; unknown
module availability is not guessed. In the editor, log insertion requires
an existing HTTP Virtual Host, while IP rules accept a Virtual Host or an
existing matching location. They reject potentially conflicting direct rules
rather than silently overwriting other security settings. Network inputs
are strict dotted-decimal IPv4 or IPv6 with optional CIDR; alternate numeric
IPv4 spellings are rejected. Paths refer to the target server. These policies
do not manage log rotation or deployed authorization/module state.

The separate `TrustedProxyOptions` / `TrustedProxyValidator` model accepts only
1–64 explicit, canonical IPv4/IPv6/CIDR proxy sources for `X-Forwarded-For`.
NGINX emits `set_real_ip_from`, `real_ip_header` and `real_ip_recursive` inside
the HTTP server; Apache emits `RemoteIPHeader` / `RemoteIPTrustedProxy` inside
the VirtualHost. An unrestricted `0.0.0.0/0` or `::/0` trusted network is
refused. Imported-host insertion rejects overlapping existing real-IP rules.
This requires optional `ngx_http_realip_module` or `mod_remoteip`; it does not
install modules or verify whether an upstream proxy sanitizes input headers.

## Export

The Static Website screen generates native config text with a validation gate. The UI saves UTF-8 text using Avalonia's platform SaveFilePicker; it does not write to a running server.

## Host presets

`HostConfigOptions` has an opt-in reverse-proxy tuning profile. NGINX writes
connection, read and send timeouts plus response buffering directly inside
the chosen `location`. When WebSocket is enabled, the explicit read timeout
replaces the otherwise generated 3600-second default, avoiding duplicate
directives. Apache maps the portable connection/read settings to `ProxyPass`
worker `connectiontimeout`/`timeout`; it intentionally does not claim an
equivalent buffering or independent send-timeout setting.

The shared `HostConfigOptions` model and `HostConfigValidator` drive two more presets:

- Reverse Proxy: mounted route, upstream HTTP(S) origin, strip/retain prefix, Host forwarding, WebSocket, request/response headers
- Redirect: HTTP status 301/302/307/308, preserving request URI; target is an HTTP(S) origin

Both presets optionally generate TLS certificate/key directives. They deliberately reject untrusted newlines, invalid hostnames and unsupported URL shapes. The NGINX generator returns an `http`-context `server` snippet, while Apache returns a `VirtualHost` snippet; neither is a full global server configuration.

Apache proxying requires `mod_proxy` + `mod_proxy_http`; WebSocket upgrade requires HTTP Server 2.4.47+, TLS requires `mod_ssl`, and custom headers require `mod_headers`. The generator adds explanatory comments; it does not attempt to load modules automatically.

`VERSION` and the CI SemVer validation gate control automatic `vX.Y.Z-debug.N.N` pre-releases and manually tagged `vX.Y.Z` releases.

## Round-trip requirement

The text-preserving import/export implementation supports selected-node replacement and syntactically checked insertion of individual directives/blocks, deletion, duplication and within-parent reordering. Every syntax node refers to an exact source span; untouched text remains unmodified. Reordering refuses to cross comments rather than guessing their attachment; the UI retains snapshots for undo/redo.

The include graph resolves only from an explicitly supplied map of files. It does not read arbitrary system paths, follow target-server filesystem links, or deploy. Full semantic validation and argument typing remain separate future work.

## Static serving

- NGINX root mapping: `root` + `index` + `try_files`.
- NGINX mounted mapping: `location` + `alias`.
- Apache root mapping: `DocumentRoot` + `<Directory>`.
- Apache mounted mapping: `Alias` + `<Directory>`.

The directory entered in the desktop UI refers to the target server filesystem; it is not required to exist on the computer running Proxy Config.
