# Phase 0 developer-surface guard packet: independent review request

**Date:** 2026-07-19

**Requested verdict:** approve or reject Phase 0 exit for tasks 3.1 through 3.12. Do not authorize
task 4.0 unless every release-blocking finding is resolved or explicitly amended.

## Review snapshot

- Baseline checkpoint: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- Review target: that commit plus the current uncommitted Phase 0 worktree diff.
- Product-source scope: no changed path under `src/`.
- OpenSpec progress before approval: 29/112 complete; 3.12 remains open; 4.0 remains open and blocked.
- Dirty manifest: the exact review-target path list is frozen in
  [`developer-facing-interface-phase-00-guard-packet-dirty-manifest-2026-07-19.txt`](developer-facing-interface-phase-00-guard-packet-dirty-manifest-2026-07-19.txt).
  Compare it with `git status --porcelain=v1 --untracked-files=all` before reviewing; any drift
  requires refreshing the manifest and rerunning the evidence commands.

## Reproduced evidence

| Lane | Result | Meaning |
|---|---:|---|
| `Disposition=Infrastructure` | 36 passed, 0 failed, 0 skipped | Guard and fixture structure is internally valid. |
| `Disposition=ExpectedRed` | 0 passed, 18 failed, 0 skipped | Every failure is an approved missing product contract; no superseded scenario remains. |
| Compile fixtures, `Green` | pass; 25 named family entries covering the exact staged inventory | The compile harness and source inventory are valid. |
| Compile fixtures, `ExpectedRed` | 1 intended product red | Exact staged authoring families are not implemented. |
| Package fixtures, `ExpectedRed` | 8 intended product reds | All application/provider/companion journeys lack only exact local-feed packages. |
| Strict OpenSpec validation | both changes valid | Planning and coordinated concurrency deltas parse strictly. |
| Whitespace/source scope | `git diff --check` pass; no `src/` paths | Phase 0 remains guard-only. |

## Commands

```powershell
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore --filter "Disposition=Infrastructure" --logger "console;verbosity=minimal"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore --filter "Disposition=ExpectedRed" --logger "console;verbosity=minimal"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition ExpectedRed
openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
git diff --check
git diff --name-only -- src
```

Expected-red commands return a nonzero exit code by design. Review their named failures rather
than treating the process exit alone as evidence.

## Required review questions

1. Does the 14-entry semantic ledger cover tasks 3.1-3.10 and all four 3.11 slices without paused
   routing, public jobs, lease expiry, query statistics, or other superseded scenarios?
2. Do the green guards prove exact package/project/assembly identity, row-scoped dependencies,
   diagnostics and failure ownership, complete tier classification, compiled friend metadata,
   recursive leak rejection, exact staged authoring, and root-only placement?
3. Are application, provider-author, DAG, deadline/retry, and all four lease/governance fixture
   lanes sufficiently concrete to turn green without inventing a future signature or protocol?
4. Are all 18 product failures intentional, actionable, and mapped to later implementation tasks?
5. Is task 4.0 still blocked everywhere until this review is approved?

## Approval recording

Record the reviewed commit plus dirty-file manifest, exact commands/results, findings, and verdict
in a new immutable dated review. Only an approval with no unresolved release-blocking finding may
close task 3.12 and unblock task 4.0.
