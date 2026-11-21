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

## M2 — AST and parsers ✅ (structural foundation)

- [x] Span/trivia-preserving AST and exact text round trip
- [x] NGINX structural lexer/parser and selected-node replacement
- [x] Apache structural parser, nested containers and continuation lines
- [x] Unknown/raw nodes retained
- [x] Include graph over explicitly supplied files, wildcards and cycle protection
- [x] Import/editor UI, multi-VirtualHost discovery and appending generated hosts
- [ ] Full directive-level grammar and semantic validation (M3)
- [ ] Project-level multi-file import, Apache ServerRoot resolution in UI and encoding audit

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
- **Multi-block (required):** multiple server/VirtualHost blocks in one file or project
- **Nested multi-block:** multiple NGINX `location`/`upstream`/`map` and Apache `<Directory>`/`<Location>`/`<Proxy>` blocks with context-aware containment
- Add, remove, duplicate, reorder and move blocks without dropping unknown directives or comments
- Per-block presets and independent settings for multiple hosts/routes/upstreams

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
