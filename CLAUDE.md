# Polaris GPS

WPF desktop GPX editor (.NET 10, MVVM without framework, Mapsui). UI in Spanish; code and comments in English; UI texts in `src/Polaris.Gps.App/Resources/Strings.resx` (Spanish, the default) with translations in `Strings.ca/eu/fr/pt.resx`: a new or changed text goes into all five (tests check keys and `{n}` placeholders).

- Projects: `Domain` (model, no WPF), `Gpx` (reader/writer), `Mapping` (map providers, layers, DEM), `App` (WPF), `tests/` (xUnit v3, no network), `installer/Polaris.Gps.Installer` (WiX 5 .msi; not in the solution, build it with `dotnet build installer\Polaris.Gps.Installer -c Release`), `installer/Polaris.Gps.Setup` (Burn bundle: .NET 10 runtime + the .msi, `PolarisGPS-<version>-setup.exe`; `dotnet build installer\Polaris.Gps.Setup -c Release`).
- No specs: the spec documents (requirements, traceability, roadmap, ADRs) were removed on 2026-10-28. When something visible to users changes, update `README.md` and the help guide `src/Polaris.Gps.App/Help/ayuda.html` with its translations `ayuda.ca/eu/fr/pt.html` (same sections and ids; shared `ayuda.css`; tests check them); nothing else. Tests carry no requirement traits.
- Build/test: `dotnet build` (warnings are errors) and `dotnet test`. If the app is open in the debugger (DLLs locked), use `dotnet build --artifacts-path <scratch>/art` and run `dotnet <scratch>/art/bin/Polaris.Gps.Tests/debug/Polaris.Gps.Tests.dll`.
- Only GPX is written; KML, KMZ, TCX, OziExplorer (.plt, .wpt) and CompeGPS/TwoNav (.trk, .wpt) are read and converted to GPX. Algorithms that exist in mendikat.net (PHP repo at `../mendikat`) must give the same results.
- Git: commit only when asked; never push unless asked. Commit messages in English, without any `Co-Authored-By: Claude` line.
