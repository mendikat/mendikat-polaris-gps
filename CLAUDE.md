# Mendikat TrailLab

WPF desktop GPX editor (.NET 10, MVVM without framework, Mapsui). UI in Spanish; code and comments in English; UI texts in `src/Mendikat.TrailLab.App/Resources/Strings.resx`.

- Projects: `Domain` (model, no WPF), `Gpx` (reader/writer), `Mapping` (map providers, layers, DEM), `App` (WPF), `tests/` (xUnit v3, no network).
- Spec-driven: every change updates `specs/REQUIREMENTS.md`, `specs/TRACEABILITY.md` and, if it affects users, `README.md`. Tests carry `[Trait("Req", "<Id>")]`. Current state and pending work: `specs/ROADMAP.md` («Después del plan»).
- Build/test: `dotnet build` (warnings are errors) and `dotnet test`. If the app is open in the debugger (DLLs locked), use `dotnet build --artifacts-path <scratch>/art` and run `dotnet <scratch>/art/bin/Mendikat.TrailLab.Tests/debug/Mendikat.TrailLab.Tests.dll`.
- Only GPX is supported (WPT was removed). Algorithms that exist in mendikat.net (PHP repo at `../mendikat`) must give the same results.
- Git: commit only when asked; never push unless asked. Commit messages in English, without any `Co-Authored-By: Claude` line.
