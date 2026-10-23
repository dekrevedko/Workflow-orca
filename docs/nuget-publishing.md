# NuGet.org trusted publishing

Status: GUIDE. Updated 2026-10-22. All twelve `0.1.0-preview.2` packages are available on
[NuGet.org](https://www.nuget.org/packages/OrcaCore.Engine.Ephemeral/0.1.0-preview.2).
The [publishing run](https://github.com/dekrevedko/Workflow-orca/actions/runs/39905678249)
verified public version indexes and source metadata, then restored and ran a fresh consumer
using only NuGet.org. The packages also remain available as GitHub release downloads.

## One-time account setup

The repository is configured for the NuGet.org profile
[`Dekrevedko`](https://www.nuget.org/profiles/Dekrevedko), with repository variable
`NUGET_USER=Dekrevedko` and the policy below. These steps
document the setup for maintenance or a change of owner.

1. [Sign in to NuGet.org](https://www.nuget.org/users/account/LogOn) with Microsoft and
   create a NuGet profile. The profile username can differ from the GitHub username.
2. Under that profile, open **Trusted Publishing** and add a GitHub policy:

   | Field | Value |
   |---|---|
   | Policy owner | The NuGet profile that will own the packages |
   | Repository owner | `dekrevedko` |
   | Repository | `Workflow-orca` |
   | Workflow file | `release.yml` |
   | Environment | `nuget` |
   | Package glob | `OrcaCore*` |
   | Permissions | Push new packages and new versions |

3. Set the repository Actions variable `NUGET_USER` to the NuGet profile username.
   This is a public account identifier; the workflow obtains its publishing credential
   through GitHub OIDC. Passwords and API keys do not belong in the repository or chat.

The GitHub environment `nuget` restricts deployments to `master` and tags matching `v*-*`.
The NuGet policy must use the same environment and workflow filename. Microsoft's
[trusted publishing guide](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
describes the account-side setup; the official [NuGet login action](https://github.com/NuGet/login)
exchanges the workflow identity for a temporary publishing credential.

## Package signatures

NuGet.org automatically adds a repository signature to uploaded packages. This workflow
does not add an author signature; publishing does not require our own code-signing certificate.
Optional author signing requires a trusted certificate and timestamp. Registering a certificate
on NuGet.org can make author signing mandatory for future submissions; see Microsoft's
[signature reference](https://learn.microsoft.com/en-us/nuget/reference/signed-packages-reference)
and [certificate requirements](https://learn.microsoft.com/en-us/nuget/nuget-org/package-signing-certificates).
The GitHub `SHA256SUMS` file describes the original release downloads; NuGet.org's added
signature can change the package file hash.

## Publish the existing preview

To resume or reverify publication of the existing preview:

```powershell
gh workflow run release.yml --ref master -f version=0.1.0-preview.2 -F nuget-only=true
```

This mode downloads the already published GitHub release files. It verifies the exact
twelve-package manifest, SHA-256 checksums, package IDs, versions, repository URL, and
source commit against the release tag before requesting a publishing credential.
It preserves the source and version of the existing GitHub release.

## Publish subsequent previews

Push a new prerelease tag through the normal release workflow. Once `NUGET_USER` is set,
successful build/test/coverage and package-consumer gates lead to the GitHub release,
then NuGet.org publication. The OIDC permission is restricted to the NuGet job.
GitHub release publication remains usable before account setup is complete.

The NuGet push tolerates an already uploaded version so an interrupted twelve-package
submission can resume. The following public-feed check still requires every package's
metadata to match the expected source commit. A successful push alone does not complete
the job: all twelve versions must be listed in the public feed, then a fresh external
consumer must restore exclusively from NuGet.org and run the typed workflow successfully.

Processing is checked for up to twenty minutes. The job summary lists package links only
after verification succeeds. If processing takes longer, rerun the existing-release mode;
it resumes publication and verification without rebuilding or replacing release files.

Install a verified version with:

```powershell
dotnet add YourProject.csproj package OrcaCore.Engine.Ephemeral --version 0.1.0-preview.2
```

The [GitHub download installation instructions](packages.md#install-from-github-release-downloads)
provide an alternative directory feed.
The prerelease limitations and the 75% engine coverage gate still apply.

## GitHub Packages mirror

The release workflow also publishes the verified `.nupkg` release assets to
`https://nuget.pkg.github.com/dekrevedko/index.json`. It grants `packages: write`
only to that job and uses the repository's temporary `GITHUB_TOKEN`; no additional
publishing secret or local package credential is required. The `.nuspec`
`RepositoryUrl` links all twelve packages to `dekrevedko/Workflow-orca`.

Normal releases run both registry jobs after CI and GitHub release creation succeed.
To mirror an existing release without rebuilding it or repeating NuGet.org publication:

```powershell
gh workflow run release.yml --ref master -f version=0.1.0-preview.2 -F github-packages-only=true
```

The existing-release modes are mutually exclusive and require `master`. The mirror
downloads the release's twelve packages and `SHA256SUMS`, verifies identities and
source metadata against the tag, and requires that source to be merged into `master`.
It pushes those exact files and restores all twelve into a fresh external consumer.
Source mapping routes every OrcaCore dependency to GitHub; the check also verifies
cache-origin metadata and each downloaded package's original SHA-256. A fallback
restore from NuGet.org cannot satisfy this gate. Retries tolerate already uploaded
versions but still require the complete round-trip check.

New GitHub packages default to private visibility, independently of their linked
repository's visibility. Maintainers can change each package to Public in Package
settings; this is irreversible. The job summary records the actual visibility and
repository link. Consumers still need authentication for a public GitHub NuGet feed.
See [GitHub's visibility documentation](https://docs.github.com/en/packages/learn-github-packages/configuring-a-packages-access-control-and-visibility)
and the [installation configuration](packages.md#install-from-github-packages).
