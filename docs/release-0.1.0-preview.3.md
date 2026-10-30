# Preview.3 release preparation

Completed on 2026-10-29: preview.3 is published on NuGet.org and GitHub Packages.
See [the publication record](releases/0.1.0-preview.3/published-2026-10-29.md)
for source, registry verification and the six-cell exact-asset qualification.

## Historical preparation snapshot (2026-10-28)

Status: prepared candidate; not published. Base: master merge
`82811cd41aae577d8a1ae13258790d3da0c30d72`. PR #14 is merged with two parents;
issue #13 is closed as completed. Master CI
[40821352504](https://github.com/dekrevedko/Workflow-orca/actions/runs/40821352504)
passed all three jobs. This preparation changes no product source or public API.

Candidate version: `0.1.0-preview.3`; tag on publication: `v0.1.0-preview.3`.
The version is absent from all twelve NuGet.org indexes and the remote tag list
at preparation time. Recheck immediately before publication. The local GitHub
CLI account lacks `read:packages`, so it cannot independently check GitHub
package-version availability. The repository's release workflow uses its own
`GITHUB_TOKEN` with `packages:write`; the preview.2 mirror previously succeeded.
No credential refresh or plaintext token is needed for this preparation.

## Prepared changes

- [Release notes](prerelease-notes.md) describe the bounded ephemeral native
  profile and preserve historical preview.2 and durable-engine limitations.
- The reusable native workflow accepts a prerelease version and an existing CI
  artifact. Normal release publication requires six native cells on exactly
  `orcacore-packages`, verified by identity, checksums and source commit.
  The workflow does not rebuild a second feed in this mode. Its consumer report
  binds the chosen version and identical package hashes across all six cells.
- The normal release path is validate → full CI → native qualification → GitHub
  prerelease assets → both registries → clean registry consumers. Existing-release
  retry modes retain their original no-rebuild behavior; they are for a previously
  qualified immutable release, not a bypass for creating one.
- NuGet.org uses the configured `nuget` environment and NuGet trusted publishing
  (`NUGET_USER=Dekrevedko`). GitHub Packages uses the repository token scoped only
  to its publishing job. Both jobs use the same release assets and source metadata.

## Validation and final publication gate

Local preview.3 packing from the merged source produced exactly twelve packages.
The inventory, version, repository commit and checksums passed; a fresh external
consumer restored all twelve and returned `42`. All eight publication-script
positive/negative checks passed. These are validation packages built from the
merge commit, not the future tag's immutable release assets.

The release-preparation target is independently self-reviewed and remains local
for approval. The newly composed hosted workflow has not been executed during
preparation; it will run on the approved release source. Existing native run
40762764617 remains the corrected source qualification evidence, not a claim
that preview.3 has been published or qualified from a registry.

After approval of this target, commit the reviewed files and verdict separately,
open the release-preparation PR, require green CI, merge with a merge commit and
sync master. Publication needs a separate explicit authorization. Then recheck
version availability, create/push `v0.1.0-preview.3` at that reviewed master commit
(or dispatch normal `release.yml` with that version from master). Do not use a
mirror-only mode for initial publication. Verify all twelve versions on both
registries and a source-mapped fresh-cache consumer before calling it released.
The release workflow supplies these checks; never push packages manually or
store tokens in a committed NuGet.Config.
