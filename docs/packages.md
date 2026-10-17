# Install and publish OrcaCore prerelease packages

Status: GUIDE. Requires .NET 10. APIs, provider schemas, and persistence formats can change.
Durable DAG execution is still incomplete; packaging does not complete the implementation gates
in the active OpenSpec changes. Start with the ephemeral engine guide or the durable samples.

The [2026-10-16 publishing audit](github-publishing-status.md) records the preparation checks
and the owner-approved 75% coverage gate. Version `0.1.0-preview.1` is available on
[NuGet.org](https://www.nuget.org/packages/OrcaCore.Engine.Ephemeral/0.1.0-preview.1)
and as [GitHub release downloads](https://github.com/dekrevedko/Workflow-orca/releases).

## Install in another project

Install the engine role you need directly from NuGet.org:

```powershell
dotnet add YourProject.csproj package OrcaCore.Engine.Ephemeral --version 0.1.0-preview.1
```

The application contracts package `OrcaCore` and internal kernel are transitive dependencies.
The [ephemeral guide](ephemeral-engine-developer-guide.md) shows registration and typed workflows.
For durable applications, reference `OrcaCore.Durable.Hosting` and the selected
`OrcaCore.Providers.PostgreSql` or `OrcaCore.Providers.SqlServer` package at the same version.
`OrcaCore.Providers.InMemory` is for development and testing.

The [NuGet.org publishing run](https://github.com/dekrevedko/Workflow-orca/actions/runs/38979035794/attempts/2)
verified all twelve public versions and their source metadata. A fresh external consumer
restored exclusively from NuGet.org and ran the typed workflow, returning `42`.

### Install from GitHub release downloads

Download and extract the `orcacore-packages` artifact from a successful CI run, or download all
twelve `.nupkg` assets and `SHA256SUMS` from a
[GitHub prerelease](https://github.com/dekrevedko/Workflow-orca/releases).
Keep the full set together so NuGet can resolve OrcaCore's package dependencies.

Register the extracted directory as a local NuGet source, then reference the desired role:

```powershell
# Replace the directory and version with those you downloaded.
dotnet nuget add source C:\packages\OrcaCore --name OrcaCore-local
dotnet add YourProject.csproj package OrcaCore.Engine.Ephemeral --version 0.1.0-preview.1
```

GitHub release downloads work without a package-registry credential. They are a directory feed
after download; the release URL itself is not a NuGet source.

NuGet.org publication uses [trusted publishing](nuget-publishing.md). Its final job verifies
all twelve packages on the public feed and runs the clean consumer before reporting availability.

## Build local packages immediately

From the repository root in PowerShell 7:

```powershell
./eng/Pack-Packages.ps1 -Version 0.1.0-preview.local.1 -OutputDirectory artifacts/my-packages
./eng/Test-Packages.ps1 -Version 0.1.0-preview.local.1 -PackageDirectory artifacts/my-packages
```

Use an empty output directory for each pack. The script packs exactly the twelve source projects
in the current package manifest, applies one prerelease version, records the source commit in
NuGet metadata, and writes SHA-256 checksums. `-NoBuild` is only for a current Release build.
The consumer check restores all twelve packages with a fresh cache outside the repository and
executes the typed sample, asserting output `42`. It leaves its temporary directory for inspection.
This checks package installation and an ephemeral journey, not full provider or DAG certification.

## Publish on GitHub

Open a pull request against `master`. CI runs the solution build (including benchmarks),
benchmark smoke checks, unit and acceptance suites, infrastructure contract guards, real PostgreSQL
and SQL Server tests, and integration tests. It also enforces the 75% engine line-coverage gate.
Only a successful run can upload the installable package artifact.

Create and push a prerelease tag on the reviewed commit:

```powershell
git tag v0.1.0-preview.1
git push origin v0.1.0-preview.1
```

The `Publish prerelease` workflow reruns CI and the external package consumer before creating the
GitHub prerelease with all twelve packages and checksums. It only grants `contents: write` to the
final publishing job and needs no additional secrets. After the workflow exists on the default
branch, it can also be run manually with a prerelease version. Stable versions are rejected.
Do not reuse a published version; choose a new version for changed source.

The [NuGet.org publishing guide](nuget-publishing.md) covers the one-time trusted publisher
setup and publishing an existing GitHub prerelease without rebuilding it. The GitHub Packages
registry is a separate destination and is not configured. Signing, SourceLink, and stable API
compatibility remain outside this prerelease workflow.
