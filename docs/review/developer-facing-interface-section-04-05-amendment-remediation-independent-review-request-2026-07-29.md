# Independent implementation review request — Section 4/5 Revision 8 remediation

**Date:** 2026-07-29  
**Requested verdict:** `APPROVE` or `REJECT`  
**Scope:** tasks `4.16`–`4.21`, `5.11`, and `5.13`–`5.15`  
**Authorization requested:** Section 4/5 remediation exit and the later start of task `6.0`

This request is an evidence packet, not an approval. Task `6.0` remains open and blocked until a
new independent implementation review approves this exact target.

## 1. Authority and review boundary

Review the target in this order:

1. `docs/specs/17-selected-mode-capability-matrix.md`;
2. `docs/specs/17-public-authoring-contract.cs`;
3. canonical requirements under `openspec/specs/`;
4. `openspec/changes/reshape-developer-facing-interfaces/tasks.md`;
5. the approved Revision 8 amendment and
   `docs/review/developer-facing-interface-amendment-revision-08-canonical-synchronization-record-2026-07-29.md`;
6. implementation plans and immutable historical review evidence.

Canonical specifications control. Older amendments, requests, and verdicts are evidence only.
Preserve every historical request, manifest, verdict, and the 2026-07-29 synchronization record.

## 2. Frozen target provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline checkpoint | `8c2dd712` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | `8c2dd712` is an ancestor of `HEAD` |
| Target | `HEAD` plus the complete current dirty worktree, including this request and manifest |
| Self-inclusive porcelain entries | 410 |
| Entry classes | 320 modified / 9 deleted / 81 untracked |
| Raw-manifest SHA-256 | `38187E3EE5A56E957AE97B10D177C032F2E540F067ECD73BCED3DF67FD6A0343` |
| LF-normalized sorted-status SHA-256 | `9EFFE2BDE4B7DEC3C845BC8484AA21928BA02310F4DBC05800F275D0AB853BF9` |
| Reshape tasks | 76 complete / 60 pending / 136 total; 0 duplicate IDs |

The authoritative self-inclusive manifest is:

`docs/review/developer-facing-interface-section-04-05-amendment-remediation-dirty-manifest-2026-07-29.txt`

Reproduce it before reading conclusions and again after validation. Any target drift invalidates the
review.

## 3. Implementation claims to re-derive

### Section 4 lifecycle closure

- One internal authoring session owns workflow configuration, graph mutation, lifecycle state, and
  phase/scope tokens.
- The lifecycle is exactly `Open`, `JoinPending`, and `Frozen`.
- A successful join returns a successor-epoch façade; superseded and escaped callback handles are
  rejected.
- Root terminal selection is an atomic freeze with exactly one winner.
- Completion builders contain a concrete frozen build result and do not build from a live mutable
  builder.
- Repeated `Build`/`TryBuild` calls preserve structure, ordered diagnostics, and fingerprint.
- The catalogued lifecycle diagnostics are exactly `SFE-AUTH-001` through `SFE-AUTH-005`, with
  exact primary and related authored locations.
- Every rejected governed lifecycle operation leaves the authored graph unchanged.
- The portable-member intersection is executable across ephemeral/durable root, nested, branch,
  item, and leased builders; no public portable builder was added.
- The exact public authoring declaration companion has no diff.

### Section 5 failure, limits, fingerprint, and fan-out closure

- `WorkflowFailure.AuthoredLocation` and the closed runtime-created
  `FailureOccurrence.Root`/`Branch`/`Item` union are attached at failure creation.
- Aggregate origin and ordered cause provenance survive detachment and fixed-codec round trips.
  `WorkflowFailure` retains reference equality and occurrences retain value equality.
- The `orcacore-json-v1` discriminator allowlist accepts only `root`, `branch`, and `item`.
- `MaxActiveFibers`, `SFE-LIMIT-003`, and `SFE-LIMIT-008` are absent from active product roles and
  catalogs. No replacement branch-width/live-fiber acceptance limit exists. Allocation exhaustion
  remains infrastructure failure.
- Structural fingerprints include codec format and inspectable authored structure only. They
  exclude compiler format, mode, definition identity/version, and every compiler option.
- Opaque behavior changes remain version-owned. `MaxInternalInstructionsPerQuantum` is bound by
  compiler-format/runtime compatibility rather than fingerprinting.
- Nonterminal durable instances enforce compiler-profile compatibility, subject only to the
  approved pre-v1 hard cutover.
- `ForEach` snapshots values through the fixed codec before admission, enforces authored `MaxItems`
  and encoded-value budgets separately, preserves tagged group identity under flat aggregation,
  and replays consistently in both engines.
- Supported nested `If` and linear child execution remain reachable; nested fan-out does not.

## 4. Recorded validation evidence

These are claims for the reviewer to reproduce, not evidence to trust.

| Check | Recorded result |
|---|---|
| `dotnet build OrcaCore.slnx --no-restore -m:1` | 0 warnings / 0 errors |
| Core suite | 447 passed / 0 failed |
| Ephemeral suite | 165 passed / 0 failed |
| Durable suite | 305 passed / 0 failed |
| Hosting suite | 15 passed / 0 failed |
| Infrastructure guards | 68 passed / 0 failed |
| Expected-red lane | 104 intentional named failures / 0 passes |
| Section 4 behavior scenarios | 4 passed / 0 failed |
| Section 5 behavior scenarios | 7 passed / 0 failed |
| Lifecycle-focused regressions | 20 passed / 0 failed |
| Focused Core lifecycle/failure/compiler/model regressions | 66 passed / 0 failed |
| Public/API/member-absence guards | 11 passed / 0 failed |
| Fixed-codec/failure/fingerprint Core regressions | 14 passed / 0 failed |
| Durable failure-envelope mapper regressions | 4 passed / 0 failed |
| Structured fan-out/replay — Core | 7 passed / 0 failed |
| Structured fan-out/replay — Ephemeral | 43 passed / 0 failed |
| Structured fan-out/replay — Durable | 48 passed / 0 failed |
| Green compile-fixture script | passed |
| Product-authoring expected-red compile script | passed with 0 remaining product-authoring gaps |
| Strict OpenSpec validation | 17 passed / 0 failed |
| `git diff --check` | exit 0; line-ending notices only |

The expected-red result is intentionally separate: its 104 failures are not passing evidence and
must remain mapped to later sections.

The compile-fixture lane freshly packs the current source, compiles the exact and product-positive
consumers, verifies 26 source-fixture plus 26 product-package forbidden-member `CS1061`
diagnostics, and rejects a deliberately incomplete package.

## 5. Required independent checks

Run at minimum:

```powershell
dotnet build OrcaCore.slnx --no-restore -m:1 -v minimal
dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj --no-build --no-restore
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-build --no-restore --filter "Disposition=ExpectedRed"
pwsh tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1
pwsh tests/OrcaCore.DeveloperSurface.Guards/run-expected-red-compile-fixtures.ps1
openspec.cmd validate --all --strict --no-interactive
git diff --exit-code -- docs/specs/17-public-authoring-contract.cs
git diff --check
```

Also independently:

- execute every Section 4/5 behavior scenario rather than accepting scenario-name presence;
- exercise both modes and all applicable root, nested, branch, item, and leased authoring roles;
- prove exact primary/related lifecycle locations and unchanged graph on every rejected mutation;
- mutate each approved fingerprint contributor and each prohibited non-contributor separately;
- verify failure provenance through creation, aggregation, detachment, durable envelope conversion,
  and fixed-codec round trip;
- verify wide fan-out and deterministic replay in both engines;
- scan active product code for `MaxActiveFibers`, retired diagnostic codes, replacement branch
  limits, public authoring-surface drift, reachable nested fan-out, and unapproved fingerprint
  contributors;
- verify task accounting, zero duplicate task IDs, and that task `6.0` remains open;
- reproduce the manifest byte-for-byte before and after all checks.

## 6. Questions requiring an explicit answer

1. Does every lifecycle transition enforce the canonical phase/scope capability rules with exact
   locations and no rejected graph mutation?
2. Do completion builders consume only a frozen snapshot and remain stable across repeated builds?
3. Is the concrete public authoring surface unchanged and is portable parity complete without a
   public portable façade?
4. Is failure occurrence non-forgeable externally, closed to the three approved variants, and
   preserved without changing equality semantics?
5. Are every live-fiber acceptance limit and both retired diagnostics gone without a replacement?
6. Does the fingerprint mutation/non-mutation matrix exactly match canonical contributor
   ownership, including compiler-profile compatibility for durable continuation?
7. Do tagged flattening, `MaxItems`, encoded-value admission, supported nested `If`, and linear
   child execution behave consistently across both engines and replay?
8. Are all 104 expected-red failures genuinely later-section gaps, with no Section 4/5 scenario
   left red?
9. Does the frozen manifest reproduce before and after validation with no target drift?

## 7. Verdict instructions

Write one new dated immutable verdict under `docs/review/`. Do not edit any existing request,
manifest, verdict, source, test, spec, task, plan, status, or documentation file.

Return `APPROVE` only if this exact Section 4/5 remediation target is complete and no release blocker
remains. Approval authorizes the implementation owner to begin task `6.0` in a later turn; it does
not mark that task complete. Otherwise return `REJECT` with source-grounded blockers.
