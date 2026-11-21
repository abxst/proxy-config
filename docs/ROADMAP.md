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
- [x] Non-blocking legacy TLS protocol hints (v0.13)
- [x] Bounded IPv4/IPv6/CIDR validation for opt-in allow/deny policies, target-server log path validation (v0.15)
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
- [x] Per-host HTTP reverse-proxy route insertion (v0.11)
- [x] Guard exact duplicate route selectors before preset insertion (v0.13)
- [x] Per-host logging and IP policy forms with safe insertion into existing HTTP contexts (v0.15)
- [ ] Per-block redirect/upstream editing forms and conflict-aware routing

## M5 — Presets

Static Website is the first implemented preset. Continue with:

- [x] Redirect / virtual-host snippets
- [x] Reverse proxy / WebSocket
- [x] Basic TLS paths and custom headers for host presets
- [x] HTTP load balancer (NGINX upstream round-robin/least_conn/ip_hash; Apache request-based balancer) (v0.9)
- [x] NGINX gRPC route insertion and upstream read/send timeouts (v0.14)
- [ ] Apache HTTP/2 upstream proxying, per-route buffering controls and full gRPC compatibility audit
- [x] FastCGI/PHP host preset for both engines (v0.10)
- [x] Per-host PHP-FPM handler insertion into existing hosts (v0.11)
- [x] Explicit TLS 1.2+1.3 / TLS 1.3-only protocol profiles and certificate paths (v0.12)
- [ ] Advanced TLS cipher/ALPN/OCSP/session/stapling policy and certificate inventory
- [x] NGINX TCP/UDP stream forwarding preset (v0.10)
- [x] NGINX/Apache HTTP access/error logging and IP allowlist/denylist (v0.15)
- [ ] Structured logging formats, rotation targets, proxy-aware real-client IP trust lists and richer authorization expressions
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

## Large delivery milestones (v0.16 → v1.0)

M0–M7 describe feature areas. These larger milestones define independently
testable increments, each with one PR and green Windows Debug/Release CI.
`VERSION` follows SemVer; each successful push to `main` publishes tagged
Debug and Release archives.

### M8 — Full configuration project engine (v0.16/v0.18, in progress)

- [x] Generate a root `nginx.conf` or `httpd.conf` with separate include files.
- [x] Accept multiple named host fragments in the core project builder.
- [x] Put NGINX HTTP snippets in `conf.d`; unwrap standalone Stream into `stream.d`.
- [x] Package local MIME types and required FastCGI parameters without reading server files.
- [x] Derive Apache `Listen` from VirtualHosts; expose `ServerRoot` and optional module include.
- [x] Validate names, sizes, structural syntax and broad placement; use the existing safe ZIP writer.
- [x] Queue, remove and reorder multiple generated presets from the visual UI, with exact duplicate HTTP host guard.
- [ ] Additional NGINX global contexts and Apache main-scope options with target profiles.

**Acceptance:** no double-nested contexts, no missing generated include,
deterministic ZIP paths, and green Windows Debug/Release build + tests.
Projects still require installed modules and server-specific runtime paths.

### M9 — Semantic validation and compatibility (v0.17, in progress)

- [x] Exact cross-file HTTP hostname/listener collisions with source locations; report structural/include issues.
- [ ] Broad route/wildcard/regex conflict analysis with NGINX precedence and Apache rewrite semantics.
- [x] Opt-in target version and installed-module list for a bounded, documented feature set.
- [ ] Complete target profile with OS and edition capabilities.
- [x] Documented minimum-version/module advisories for NGINX Stream/gRPC and Apache FastCGI/HTTP2.
- [ ] Complete module dependency and version checks for the public directive inventory.
- [ ] Typed argument schemas for high-impact directives, raw fallback for others.
- [ ] Static-site exposure, proxy trust and TLS policy review profiles.

**Acceptance:** diagnostics include file/line/context/reason/source; ambiguous
module/version details remain non-blocking advisories.

### M10 — Advanced proxy, security and observability (v0.19/v0.20, in progress)

- [x] Per-route NGINX connect/read/send timeout and buffering controls; Apache ProxyPass connection/read timeouts.
- [ ] Per-route retry profiles, Apache/AJP-specific buffering, upstream health settings where supported.
- [ ] Apache HTTP/2 and gRPC upstream compatibility checks.
- [ ] Advanced TLS cipher/ALPN/session/OCSP configuration.
- [x] Explicit CIDR-based trusted X-Forwarded-For proxy policy for both engines, with module advisories and /0 guard.
- [ ] Structured logging formats, log destinations and advanced multi-hop real-client IP trust chains.
- [ ] Rich authorization policies and NGINX Mail proxy where supported.

**Acceptance:** golden generator tests for each supported engine or a
documented engine-specific exclusion.

### M11 — Visual/raw editor experience

- [ ] Multi-preset composer with navigable project/include tree.
- [ ] Registry-backed typed property panels for supported directive families.
- [ ] Syntax highlighting, completion, navigation and scalable diff hunks.
- [ ] Drag/drop and command palette with guarded structural edits.
- [ ] Per-block redirect, upstream, logging and authorization controls.

**Acceptance:** preserve unknown syntax/comments; undo/redo and ZIP round
trips remain lossless for supported encodings.

### M12 — Hardening and stable release

- [ ] Audit the documented directive catalog against pinned upstream snapshots.
- [ ] Real-world config corpus and structured fuzzing in CI.
- [ ] Large-project benchmarks, resource limits and archive security tests.
- [ ] Windows packaging smoke tests, distribution signing strategy and accessibility.
- [ ] User guide, changelog, support matrix and `v1.0.0` release gate.

**Acceptance:** green Windows Debug/Release builds, verified SemVer tag,
both release ZIP assets; no deployment or server writes.
