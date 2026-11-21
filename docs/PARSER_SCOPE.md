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
- Identify Virtual Hosts and append another generated NGINX server (inside an existing `http` block when present) or Apache `VirtualHost`.
- Evaluate a finite in-memory set of files for `include` / `Include` / `IncludeOptional` edges, wildcards, missing required paths, circular references and recursion depth.
- Import UTF-8 configuration through Avalonia's open dialog; export the current UTF-8 text through its save dialog.

## Explicit limitations

- Parsing is structural, not a full NGINX/Apache grammar or validation against a specific binary. Invalid values inside syntactically well-formed directives are not diagnosed yet.
- Context warnings are conservative. Unknown directives or ambiguous third-party module contexts are kept without a warning. A context match does not guarantee validity on a particular installed server.
- Inserted directives must use single-line arguments; multi-line and compound snippets are intentionally rejected. Use direct node replacement for advanced raw snippets.
- The up/down commands reorder only within one parent; the separate "Move to" command can relocate to another parent in the same file. Both operations refuse potentially attached comments.
- Cross-parent moves cannot use the source node's own descendants as a target and reject potentially attached comments. Cross-file moves remain pending.
- Some unusual constructs (for example comments embedded within unfinished directives, uncommon Apache container formatting or non-UTF-8 encodings) may produce structural diagnostics. Original text is still retained.
- Include graph does not read arbitrary files from disk or remote systems. It resolves only explicitly supplied workspace files.
- The GUI imports multiple files into an in-memory workspace but edits one active document at a time. Folder import is explicitly user-selected and subject to limits; it does not follow included paths outside the workspace.
- Apache relative include resolution defaults to the source-file folder unless the caller supplies an Apache `ServerRoot` explicitly.
- A top-level NGINX `server` snippet is suitable for a configuration included from an `http` block, not as a standalone full `nginx.conf`.
- Typed argument hints intentionally cover only trivial signatures. Complex parameters, condition-dependent meanings and module/version compatibility still require server-side verification.
- No server installation, restart, deployment, or server-side write operations are performed.

## Next steps

Typed directive grammar, version/module/context validation, a project-level file collection, visual property forms, semantic diagnostics and byte-preserving import/export across alternate encodings.
