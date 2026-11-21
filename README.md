# Proxy Config

Proxy Config is a desktop configuration generator for NGINX and Apache HTTP Server.

## Current foundation

- Avalonia 12 + .NET 10 desktop UI
- NGINX static-site generator
- Apache static-site generator
- Static directory, URL mount path, domain, port and index inputs
- SPA fallback, directory listing, cache and compression options
- Shared validation layer
- Directive registry domain model for the full public-directive catalog
- Unit tests and GitHub Actions build/test/publish

The long-term goal is an offline, registry-driven configuration IDE covering the public NGINX and Apache HTTP Server documentation without requiring either server to be installed.

## Build

    dotnet restore ProxyConfig.sln
    dotnet build ProxyConfig.sln -c Release
    dotnet test ProxyConfig.sln -c Release

Run the desktop app:

    dotnet run --project src/ProxyConfig.App/ProxyConfig.App.csproj

See docs/ARCHITECTURE.md and docs/ROADMAP.md for the implementation plan.
