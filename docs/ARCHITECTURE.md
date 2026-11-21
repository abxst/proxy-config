# Architecture

Proxy Config keeps the UI independent from server-specific syntax.

Desktop UI -> typed project models -> validation -> NGINX / Apache generators.

## Projects

- `ProxyConfig.App`: Avalonia desktop shell, static-site form, offline explorer and Save dialog.
- `ProxyConfig.Core`: shared models, static-site validation and embedded documentation catalog.
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

## Export

The Static Website screen generates native config text with a validation gate. The UI saves UTF-8 text using Avalonia's platform SaveFilePicker; it does not write to a running server.

## Host presets

The shared `HostConfigOptions` model and `HostConfigValidator` drive two more presets:

- Reverse Proxy: mounted route, upstream HTTP(S) origin, strip/retain prefix, Host forwarding, WebSocket, request/response headers
- Redirect: HTTP status 301/302/307/308, preserving request URI; target is an HTTP(S) origin

Both presets optionally generate TLS certificate/key directives. They deliberately reject untrusted newlines, invalid hostnames and unsupported URL shapes. The NGINX generator returns an `http`-context `server` snippet, while Apache returns a `VirtualHost` snippet; neither is a full global server configuration.

Apache proxying requires `mod_proxy` + `mod_proxy_http`; WebSocket upgrade requires HTTP Server 2.4.47+, TLS requires `mod_ssl`, and custom headers require `mod_headers`. The generator adds explanatory comments; it does not attempt to load modules automatically.

`VERSION` and the CI SemVer validation gate control automatic `vX.Y.Z-debug.N.N` pre-releases and manually tagged `vX.Y.Z` releases.

## Round-trip requirement

When import support lands, unknown or not-yet-typed directives must be retained as raw AST nodes. Importing and exporting must never silently delete input that the current registry does not understand.

## Static serving

- NGINX root mapping: `root` + `index` + `try_files`.
- NGINX mounted mapping: `location` + `alias`.
- Apache root mapping: `DocumentRoot` + `<Directory>`.
- Apache mounted mapping: `Alias` + `<Directory>`.

The directory entered in the desktop UI refers to the target server filesystem; it is not required to exist on the computer running Proxy Config.
