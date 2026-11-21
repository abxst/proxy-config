# Proxy Config

Proxy Config is an offline Windows desktop configuration generator and documentation browser for **NGINX** and **Apache HTTP Server**. Built with Avalonia 12 and .NET 10.

## Available today

- Static Website preset: domain, listen port, target-server directory, URL mount path and index files
- NGINX / Apache configuration preview with SPA fallback, directory listing, caching and compression options
- **Reverse Proxy preset**: hostname/port, upstream HTTP(S) origin, mounted URL path, prefix rewriting, forwarded Host, WebSocket, custom request/response headers
- **Redirect preset**: 301/302/307/308 to a target origin, preserving the incoming request path and query
- Optional **TLS certificate and private key paths** for the reverse-proxy and redirect presets
- **Export `.conf`** using the system Save dialog (no server installation or deployment)
- **Multi-block Import / Editor**: open existing NGINX/Apache `.conf`, edit individual nodes, add directives and nested containers, duplicate/remove/reorder sibling blocks, append generated Virtual Hosts, undo/redo, and export
- **Directive-assisted editing**: search the offline catalog within the editor and insert directives with their name and raw argument string into a selected block or root scope
- **Context advisories (v0.5)**: display non-blocking warnings when a known directive is placed outside its documented NGINX/Apache context; prefer context-appropriate suggestions during insertion
- Recognize NGINX HTTP/Stream/Mail module families and distinct block/directive variants; recognize Apache VirtualHost, directory containers, conditional wrappers and root `.htaccess` documents
- **Typed argument assistance (v0.6)**: infer choice lists, non-negative integers and simple NGINX sizes from unambiguous catalog syntax; retain editable raw arguments and a validation override for advanced cases
- **Multi-file workspace (v0.6)**: open multiple configs or explicitly choose a folder, switch among files with independent undo/redo, create/remove project files and review `include`/`Include` edges plus missing/cyclic dependencies
- **Project ZIP export**: save all in-memory files with safe relative paths, or export the selected `.conf` individually; view bounded line diff against imported content and search the structural tree
- **Cross-parent editing (v0.7)**: relocate a selected block/directive into another block in the same file (or root), rejecting self-nesting and potentially attached comments
- **Static route insertion (v0.7)**: choose a specific existing NGINX server/Apache VirtualHost and insert a generated static filesystem route with SPA/index/cache/compression options, leaving other hosts intact
- **Cross-file moves (v0.8)**: move selected directive/block into a different project file and chosen destination scope; one-level transfer undo refuses to overwrite files subsequently changed
- **Encoding preservation (v0.8)**: strict decoding for UTF-8, UTF-8 BOM, UTF-16 and UTF-32; individual/ZIP export preserves source encoding and BOM. Legacy encodings are rejected rather than silently corrupted.
- **Parser hardening (v0.8)**: deterministic malformed-input fuzz regression tests, span invariants, large Apache host corpus and encoding round-trip tests
- **HTTP Load Balancer preset (v0.9)**: NGINX `upstream` plus `server` supporting round-robin, least connections or client-IP hash; Apache `VirtualHost` + `mod_proxy_balancer` using request-based round-robin; two to 32 validated HTTP backend origins
- **PHP-FPM / FastCGI preset (v0.10)**: NGINX `fastcgi_pass` with a missing-file guard; Apache `mod_proxy_fcgi` / `FilesMatch`. Enter the target-server document root, PHP index files and an explicit FastCGI TCP endpoint
- **NGINX Stream preset (v0.10)**: configure TCP or UDP port forwarding, target endpoint and proxy timeout. Generates a standalone main-scope `stream` block; intentionally cannot append it to an HTTP Virtual Host
- **Route editor (v0.11)**: insert a validated reverse-proxy route or PHP-FPM handler into a selected existing HTTP server/VirtualHost; reject NGINX Stream/Mail servers and preserve other hosts
- **TLS protocol profile (v0.12)**: reverse-proxy and redirect host presets support TLS 1.2 + 1.3 or TLS 1.3 only, with explicit NGINX `ssl_protocols` / Apache `SSLProtocol` directives and certificate/key validation
- **Route conflict detection (v0.13)**: block exact duplicate NGINX `location`, Apache `ProxyPass`/`Alias` or PHP `FilesMatch` selectors when inserting built-in routes into existing hosts
- **TLS security hints (v0.13)**: non-blocking review warnings for explicitly configured obsolete SSL/TLS protocol versions
- **NGINX gRPC route (v0.14)**: insert `grpc_pass` into a selected HTTP host with validated TCP backend, `grpc://`/`grpcs://` selection and independent read/send timeouts; the existing listener must be configured for HTTP/2
- **Host logging and IP ACL forms (v0.15)**: optional logging and CIDR allow/deny policies in generated HTTP hosts, plus a Log / ACL tab to add them to imported hosts or locations. NGINX uses `access_log`/`error_log` and ordered `allow`/`deny`; Apache uses `CustomLog`/`ErrorLog`/`LogLevel` and `Require ip` authorization containers.
- **Full project scaffold ZIP (v0.16)**: export a generator preset as a multi-file project with a root `nginx.conf`/`httpd.conf`, HTTP site includes, optional NGINX stream include, local MIME definitions and FastCGI parameters when referenced. Apache `Listen` lines are derived from VirtualHost ports; the optional module include and `ServerRoot` are editable. The builder also accepts multiple named fragments programmatically.
- **Project diagnostics (v0.17)**: scan all files reachable through the selected config's include graph; flag malformed includes/syntax and exact duplicate NGINX/Apache HTTP hostname+listener bindings, including Apache ServerAlias. In the Import / Editor Include tab, optionally enter a target version and explicit module inventory for documented Stream, gRPC, FastCGI, HTTP/2, proxy and TLS compatibility hints. Findings are advisory with file/line references.
- **Multi-preset ZIP composer (v0.18)**: queue snapshots of several generated sites, proxies, load balancers, PHP hosts and NGINX Stream ports in the same full-project archive. Add, remove and reorder items from the generator pane; validate composition and reject an exact duplicate HTTP hostname/listener before queuing. NGINX and Apache queues remain separate during export.
- **Security review hints (v0.9)**: non-blocking warnings for explicit directory listing, detailed server version tokens and broad Apache `AllowOverride All`
- Text-preserving AST parser: unknown directives, comments and formatting survive import/edit/export; parse diagnostics flag structural problems
- Include-graph API for a caller-supplied set of files with wildcard expansion, missing required includes, cycle detection and depth limits
- **Offline Directive Explorer**: source-linked NGINX and Apache directives, syntax, module, context and available documented metadata
- Search across **1,009 NGINX directive entries**, **730 Apache directive entries** and **258 NGINX variable references** from official upstream indexes
- Windows x64 GitHub Actions builds and automated Releases

> The directive catalog is a **documentation reference**, not a compatibility guarantee. Some entries require optional/third-party modules, specific versions, platforms, or commercial editions. Context checks are **advisories**, not authoritative semantic validation; ambiguous/unknown constructs do not generate hard failures. Typed forms handle only simple, unambiguous signatures; complex arguments remain raw. PHP-FPM, gRPC and NGINX Stream require corresponding software/modules already enabled on the target server. NGINX access rules are first-match and may override inherited rules in a narrower location; Apache `Require` policies are evaluated within authorization contexts, so check interactions with other `Location`/`Directory` blocks. Log files are paths on the target server, which must permit writes; an omitted Apache `CustomLog` may still inherit parent logging. Non-Unicode legacy code pages require external conversion. The editor does not deploy or reload a server.

## Build locally

Requires .NET 10 SDK.

```sh
dotnet restore ProxyConfig.sln
dotnet build ProxyConfig.sln --configuration Release
dotnet test ProxyConfig.sln --configuration Release
dotnet run --project src/ProxyConfig.App/ProxyConfig.App.csproj
```

To use the packaged Windows app, download `ProxyConfig-Release-win-x64.zip` from [GitHub Releases](https://github.com/abxst/proxy-config/releases), extract the ZIP and launch `ProxyConfig.App.exe`. It is framework-dependent and requires the .NET 10 Windows Desktop Runtime.

## CI and SemVer tags

GitHub Actions builds, tests and packages **both Debug and Release** on pull requests, changes to `main`, manual runs and `v*` tag pushes.

| Trigger | Output |
| --- | --- |
| Pull request | Two downloadable workflow artifacts; no public release |
| Push to `main` | Automatic GitHub **pre-release**, tag `vX.Y.Z-debug.<run>.<attempt>`; both Debug and Release ZIPs |
| Push tag `vX.Y.Z` | Stable GitHub **Release** for that tag; both Debug and Release ZIPs |
| Push tag `vX.Y.Z-rc.1` | GitHub **pre-release** for that SemVer tag |
| Manual workflow run | Two downloadable workflow artifacts; no public release |

Release publication runs **only after both configurations pass build and tests**. The workflow has `contents: write` permission on the publishing job only. Tag values follow [SemVer 2.0.0](https://semver.org/) with a leading `v`; incorrect SemVer tags and tags that do not match `VERSION` fail validation.

The `VERSION` file records the next release base version, currently `0.18.0`. Update that file when starting another release cycle. Existing `debug-build-*` tags from the earlier workflow are retained for history.

Create an official version:

```sh
git tag v0.18.0
git push origin v0.18.0
```

Pre-release tags are generated by CI and do not need to be created manually.

## Documentation inventory

The embedded `src/ProxyConfig.Core/Registry/catalog.json.gz` was generated from public documentation indexes at:

- [NGINX directive index](https://nginx.org/en/docs/dirindex.html)
- [NGINX variable index](https://nginx.org/en/docs/varindex.html)
- [Apache HTTP Server 2.4 directive index](https://httpd.apache.org/docs/2.4/mod/directives.html)

Regenerate the offline catalog from a complete crawled inventory:

```sh
python scripts/build_directive_catalog.py Server_Config_Directive_Inventory.json
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/ROADMAP.md](docs/ROADMAP.md) for architecture, known limitations and future work.
See [docs/PARSER_SCOPE.md](docs/PARSER_SCOPE.md) for the structural import/edit/export contract and its known limits.
