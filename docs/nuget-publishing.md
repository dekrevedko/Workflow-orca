# NuGet.org trusted publishing

Status: GUIDE. The GitHub workflow supports NuGet.org publication. An owning NuGet.org
account and its trusted publishing policy must be configured before the first upload.
The existing `0.1.0-preview.1` packages remain available as GitHub release downloads.

## One-time account setup

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

## Publish the existing preview

After the workflow is merged into `master` and account setup is complete:

```powershell
gh workflow run release.yml --ref master -f version=0.1.0-preview.1 -F nuget-only=true
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
dotnet add YourProject.csproj package OrcaCore.Engine.Ephemeral --version 0.1.0-preview.1
```

Use this command after the version is available on NuGet.org. Until then, follow the
[GitHub download installation instructions](packages.md#install-in-another-project).
The prerelease limitations and the 75% engine coverage gate still apply.
