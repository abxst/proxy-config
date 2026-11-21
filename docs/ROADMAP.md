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
- [x] Project-level multi-file import and encoding audit (v0.6/v0.8); Apache ServerRoot is an explicit workspace option

## M3 — Validation (partially shipped in v0.5)

- [x] Structural syntax check for inserted directives/blocks (v0.4)
- [x] Non-blocking, catalog-backed context mismatch warnings for known directives (v0.5)
- [x] Scope-aware directive suggestions, including NGINX HTTP/Stream/Mail and Apache containers (v0.5)
- [x] Typed-choice and simple numeric/size form assistance for unambiguous catalog signatures (v0.6)
- [ ] Complete per-directive argument grammars and semantic validation
- [ ] Exact installed module, edition/platform and version compatibility checks
- [x] Narrow non-blocking security review hints for directory listing and server token exposure (v0.9)
- [ ] Complete reference/dependency diagnostics, broader security lint with policy profiles

## M4 — Visual configuration editor (partially shipped in v0.4)

- [x] Configuration tree for a single imported text document
- [x] Offline catalog search to suggest directive names and display documented syntax/context/module
- [x] Undo/redo for applied AST edits and parsed text changes
- [x] **Multi-block:** multiple NGINX server or Apache VirtualHost siblings; explicit root scope insertion
- [x] **Nested multi-block:** construct multiple NGINX brace blocks and Apache containers by block name and arguments, including location/Directory/Location
- [x] Add, remove, duplicate and reorder sibling blocks/directives while preserving unmodified source spans; moves across comments are deliberately blocked
- [ ] Registry-driven typed property editor and validation for each directive argument
- [ ] Verified, target-server-aware context/module/version compatibility and semantic diagnostics (M3; advisory context checks implemented)
- [x] Move blocks/directives into a different parent within the same file, with cycle and attached-comment guards (v0.7)
- [x] Cross-file relocation between explicitly imported files with guarded one-level undo (v0.8)
- [ ] Drag and drop and command palette
- [x] Multi-file editor with explicit imports, independent edit histories and ZIP export (v0.6)
- [x] Add static filesystem-serving route into a selected host with existing generator options (v0.7)
- [ ] Per-block proxy/redirect/upstream forms (cross-file block moves implemented v0.8)

## M5 — Presets

Static Website is the first implemented preset. Continue with:

- [x] Redirect / virtual-host snippets
- [x] Reverse proxy / WebSocket
- [x] Basic TLS paths and custom headers for host presets
- [x] HTTP load balancer (NGINX upstream round-robin/least_conn/ip_hash; Apache request-based balancer) (v0.9)
- [ ] gRPC proxying and advanced timeout/buffering
- [x] FastCGI/PHP host preset for both engines (v0.10)
- [ ] Per-route FastCGI insertion into existing hosts
- [ ] Advanced TLS profile and certificate options
- [x] NGINX TCP/UDP stream forwarding preset (v0.10)
- [ ] Mail proxy preset where supported

## M6 — Raw editor and round trip (partially shipped in v0.6)

- [ ] Syntax highlighting
- [x] Registry-driven directive name lookup and simple enum choices
- [x] Structural and context diagnostics
- [x] Visual/raw synchronization after Parse
- [x] Source-relative, bounded line diff before export
- [x] Workspace include-graph panel with explicitly imported file set
- [ ] Rich completion, line/column navigation and scalable diff with hunk controls

## M7 — Coverage and hardening

- Audit every public documented directive against target upstream snapshots
- [x] Deterministic bounded-input fuzz tests for both parsers (v0.8)
- [x] Regression test parsing 1,000 Apache Virtual Hosts (v0.8)
- [x] Strict UTF-8/UTF-16/UTF-32 BOM round trips, reject invalid bytes (v0.8)
- [ ] Continuous structured fuzzing with sanitizers and real-world corpus
- [ ] Golden corpus from official examples and large config performance benchmarks
