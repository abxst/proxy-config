# Roadmap

## M0 - Foundation

- Avalonia desktop shell
- .NET 10 solution
- CI build/test/publish
- Shared core model
- NGINX and Apache backend projects

## M1 - Registry

- Generate committed registries from official NGINX and Apache documentation
- Version/module/context/edition/platform metadata
- Registry validation tests
- Offline directive search

## M2 - AST and parsers

- Trivia-preserving AST
- NGINX lexer/parser/generator
- Apache lexer/parser/generator
- Unknown/raw nodes
- Include graph and circular include protection

## M3 - Validation

- Syntax and argument validation
- Context/module/version checks
- Reference and dependency diagnostics
- Security-oriented lint warnings without automatic rewriting

## M4 - Visual configuration editor

- Configuration tree
- Registry-driven property editor
- Search and command palette
- Undo/redo

## M5 - Presets

Static Website is the first implemented preset. Continue with:

- Redirect
- Reverse proxy
- WebSocket
- gRPC
- Load balancer
- FastCGI/PHP
- TLS
- TCP/UDP stream where supported
- Mail proxy where supported

## M6 - Raw editor and round trip

- Syntax highlighting
- Completion from registry
- Diagnostics
- Visual/raw synchronization
- Diff before export

## M7 - Coverage and hardening

- Audit every public documented directive in the target upstream snapshots
- Fuzz parsers
- Golden corpus from representative official examples
- Large configuration performance tests
