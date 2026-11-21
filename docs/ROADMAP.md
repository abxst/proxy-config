# Roadmap

## M0 — Foundation ✅

- Avalonia desktop shell
- .NET 10 solution
- CI build/test/publish
- Shared static-site model
- NGINX and Apache generator backends

## M1 — Public documentation explorer ✅ (reference catalog)

- Commit compact registry extracted from official NGINX/Apache documentation indexes
- Include syntax, module, context, default and available upstream status/compatibility fields
- Preserve module-level duplicates and indexed NGINX variables
- Offline search, detail panel, source URLs and catalog tests
- `.conf` export from the Static Website preset
- Build/test both Debug and Release and publish tagged GitHub Releases
- Follow SemVer tags for automated pre-releases and tagged stable releases

**Still required before the catalog can drive arbitrary config editing:** exact edition/platform/version support audit; module availability metadata; argument grammar.

## M2 — AST and parsers

- Trivia-preserving AST
- NGINX lexer/parser/generator
- Apache lexer/parser/generator
- Unknown/raw nodes
- Include graph and circular include protection

## M3 — Validation

- Syntax and argument validation
- Context/module/version checks
- Reference and dependency diagnostics
- Security lint warnings without automatic rewriting

## M4 — Visual configuration editor

- Configuration tree
- Registry-driven property editor
- Search and command palette
- Undo/redo

## M5 — Presets

Static Website is the first implemented preset. Continue with:

- [x] Redirect / virtual-host snippets
- [x] Reverse proxy / WebSocket
- [x] Basic TLS paths and custom headers for host presets
- [ ] gRPC proxying and advanced timeout/buffering
- Load balancer
- FastCGI/PHP
- TLS
- TCP/UDP stream and mail proxy where supported

## M6 — Raw editor and round trip

- Syntax highlighting
- Completion from registry
- Diagnostics
- Visual/raw synchronization
- Diff before export

## M7 — Coverage and hardening

- Audit every public documented directive against target upstream snapshots
- Fuzz parsers
- Golden corpus from official examples
- Large configuration performance tests
