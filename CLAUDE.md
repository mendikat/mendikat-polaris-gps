# Mendikat TrailLab

WPF desktop GPX editor (.NET 10, MVVM without framework, Mapsui). UI in Spanish; code and comments in English; UI texts in `src/Mendikat.TrailLab.App/Resources/Strings.resx`.

- Projects: `Domain` (model, no WPF), `Gpx` (reader/writer), `Mapping` (map providers, layers, DEM), `App` (WPF), `tests/` (xUnit v3, no network).
- No specs: the spec documents (requirements, traceability, roadmap, ADRs) were removed on 2026-10-28. When something visible to users changes, update `README.md` and the help guide `src/Mendikat.TrailLab.App/Help/ayuda.html`; nothing else. Tests carry no requirement traits.
- Build/test: `dotnet build` (warnings are errors) and `dotnet test`. If the app is open in the debugger (DLLs locked), use `dotnet build --artifacts-path <scratch>/art` and run `dotnet <scratch>/art/bin/Mendikat.TrailLab.Tests/debug/Mendikat.TrailLab.Tests.dll`.
- Only GPX is supported (WPT was removed). Algorithms that exist in mendikat.net (PHP repo at `../mendikat`) must give the same results.
- Git: commit only when asked; never push unless asked. Commit messages in English, without any `Co-Authored-By: Claude` line.
