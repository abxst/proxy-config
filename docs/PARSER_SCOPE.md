# Structural parser: contract and boundaries

## Implemented

- NGINX: parse semicolon directives and nested brace blocks, with quote/escape and comment handling. Comments/whitespace are retained as source spans.
- Apache: parse one directive per logical line, backslash line continuation, nested `<VirtualHost>`/`<Directory>`/`<IfModule>`-style containers, comments and blank lines.
- Preserve arbitrary undocumented directives. Neither parser strips or rewrites unknown text.
- Edit a selected node by replacing only its original source span; the rest of the input stays exactly as it was.
- Insert NGINX/Apache directives and block containers into a selected parent, duplicate or delete nodes, and move sibling nodes up/down where there are no attached comments. A root-scope option permits additional top-level Virtual Hosts.
- Validate an inserted fragment as a single structurally parseable syntax node before changing the document. It does not validate semantic context or values.
- Search documented directive names and inspect syntax, context and module hints in the editor; undo/redo applied structural and parsed text changes.
- Report advisory warnings for known directives placed in contexts not listed by upstream docs, display source line numbers, and optionally hide clearly incompatible directive suggestions.
- Create an in-memory multi-file workspace through explicit file/folder selection or new-file commands, inspect include edges and missing dependencies, and export a safe ZIP containing the selected project files.
- Show a bounded line-by-line diff from imported text, search nodes by name/kind, and infer simple enum/number/size argument forms from the catalog with raw fallback.
- Relocate directives/blocks across parents within a document and insert built-in static route fragments into a selected existing host.
- Insert reverse-proxy routes or PHP-FPM handlers into a selected existing HTTP host; refuse NGINX stream/mail server contexts.
- Insert a validated NGINX gRPC route into an existing HTTP host while retaining its HTTP/2 and TLS listener configuration.
- Add validated host-level logging and IP-only allow/deny policies; Apache authorization is scoped to `<Location>` and NGINX rules to the selected HTTP server/location.
- Move syntax subtrees between explicitly imported files with an atomic two-document calculation and guarded one-level undo; retain Unicode file BOM/encoding on export.
- Display narrow, non-blocking security review hints for explicitly enabled directory listing and version exposure settings; no automatic rewrites.
- Identify Virtual Hosts and append another generated NGINX server (inside an existing `http` block when present) or Apache `VirtualHost`.
- The NGINX Stream preset produces a root-scope `stream` block; generated-host appending rejects it because `stream` cannot be nested under `http`.
- Evaluate a finite in-memory set of files for `include` / `Include` / `IncludeOptional` edges, wildcards, missing required paths, circular references and recursion depth.
- Show cross-file diagnostics for exact HTTP host binding collisions, include problems and structural parse errors. Optional target profiles advise on a bounded set of documented module/version constraints.
- Import UTF-8, UTF-16 or UTF-32 files (BOM-aware) through Avalonia's open dialog; export individual files and ZIP workspaces using their preserved encodings.
- Build a multi-file full-project scaffold from valid generated snippets, including root config, NGINX HTTP/Stream includes, MIME/FastCGI support and Apache VirtualHost Listen declarations.
- Queue multiple immutable presets in the generator composer; export numeric-prefixed include files to preserve user-selected order, with an exact duplicate HTTP host guard.

## Explicit limitations

- Parsing is structural, not a full NGINX/Apache grammar or validation against a specific binary. Invalid values inside syntactically well-formed directives are not diagnosed yet.
- Context warnings are conservative. Unknown directives or ambiguous third-party module contexts are kept without a warning. A context match does not guarantee validity on a particular installed server.
- Inserted directives must use single-line arguments; multi-line and compound snippets are intentionally rejected. Use direct node replacement for advanced raw snippets.
- The up/down commands reorder only within one parent; the separate "Move to" command can relocate to another parent in the same file. Both operations refuse potentially attached comments.
- Cross-parent moves cannot use the source node's own descendants as a target and reject potentially attached comments. Cross-file transfer undo is intentionally one-level and invalidates when either file has changed.
- Malformed non-Unicode legacy encodings are refused; byte-identical preservation is guaranteed only for the supported UTF formats, not arbitrary legacy code pages.
- Some unusual constructs (for example comments embedded within unfinished directives, uncommon Apache container formatting or non-UTF-8 encodings) may produce structural diagnostics. Original text is still retained.
- Include graph does not read arbitrary files from disk or remote systems. It resolves only explicitly supplied workspace files.
- The GUI imports multiple files into an in-memory workspace but edits one active document at a time. Folder import is explicitly user-selected and subject to limits; it does not follow included paths outside the workspace.
- Apache relative include resolution defaults to the source-file folder unless the caller supplies an Apache `ServerRoot` explicitly.
- NGINX relative includes resolve from the main config's directory (configuration prefix), not the source include file's folder.
- A top-level NGINX HTTP `server` snippet is suitable for a configuration included from an `http` block, not as a standalone full `nginx.conf`; a Stream preset emits a separate main-context `stream` block.
- Typed argument hints intentionally cover only trivial signatures. Complex parameters, condition-dependent meanings and module/version compatibility still require server-side verification.
- No server installation, restart, deployment, or server-side write operations are performed.
- Full-project ZIP is a template with target-dependent module/MPM/runtime assumptions, not a verified self-contained Apache or NGINX distribution. Per-snippet validation only rejects structural and obvious placement mistakes; module and version checks remain future work.

## Next steps

Per-directive typed grammar, installed version/module/context validation, more route-scoped property forms, semantic diagnostics, richer editing interactions and larger real-world configuration corpora.
