# Polaris GPS: privacy and code signing policy

## Code signing policy

Releases are built from this repository by GitHub Actions (`.github/workflows/build.yml`) on version tags, never on a
developer's computer. Once the project is approved, the setup program and the `.msi` will be signed with free code
signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

- Committers and reviewers: [Javier Urrutia](https://github.com/mendikat) (javi@mendikat.net)
- Approvers (each release is a draft until approved and published by hand): [Javier Urrutia](https://github.com/mendikat)

Third-party binaries included in the installers keep their own signatures and licences: the .NET 10 Desktop Runtime
installer (signed by Microsoft) and the ERDAS ECW/JP2 SDK of Hexagon (not free software, see `LICENSE-EXCEPTION.md`).

## Privacy

Polaris GPS does not send any personal or usage data to Mendikat or anyone else. It only connects to the services needed
for what the user does: map tiles of the chosen cartography, the IGN Spain elevation model (pointer elevation and
*Adjust elevations*), the BRouter routing server when *Follow paths* is on, and the GitHub API when the user chooses
*Check for updates*. Those providers receive the requests as with any web browsing. The technical log stays on the
user's computer.

Contact: Javier Urrutia · javi@mendikat.net · https://www.mendikat.net
