# Architecture

Proxy Config keeps the UI independent from server-specific syntax.

UI -> project model -> typed configuration model / AST -> validators -> NGINX or Apache generators.

## Projects

- ProxyConfig.App: Avalonia desktop shell and view models.
- ProxyConfig.Core: shared models, validation, diagnostics and directive registry types.
- ProxyConfig.Nginx: NGINX parser/generator/validator backend.
- ProxyConfig.Apache: Apache HTTP Server parser/generator/validator backend.
- ProxyConfig.Tests: cross-backend behavior and regression tests.

## Directive registry

The registry is intentionally metadata-driven. A directive definition can carry server, module, syntax, argument schema, contexts, default, version range, edition/platform constraints and the official documentation source.

The application must eventually generate forms from this metadata rather than creating a bespoke screen for each directive.

## Round-trip requirement

When import support lands, unknown or not-yet-typed directives must be retained as raw AST nodes. Importing and exporting a configuration must never silently delete input that the current registry does not understand.

## Static serving

Static serving is a high-level preset that emits native configuration:

- NGINX root mapping: root + index + try_files.
- NGINX mounted mapping: location + alias.
- Apache root mapping: DocumentRoot + Directory.
- Apache mounted mapping: Alias + Directory.

The path entered in the desktop UI refers to the target server filesystem. It is not required to exist on the machine running Proxy Config.
