# Task 3.1 ownership test-first evidence — 2026-10-17

This proposed target is based on `ef88957af4c41bf18def689ba81c71e1a1d18696`.
It implements no ownership fence, settlement tracker, sweep, retirement or public policy.
Task 3.1 and all later tasks stay open. Both registry records remain schema 13,
CanonicalApprovedSourcePending, with source approval null. Independent review of this
exact target is required before preparing the task 3.2 implementation.

## Binding verified through Git

| Anchor | Exact value |
|---|---|
| Binding checkpoint | `5f17b7e104b54aa4849795095a161216359ca6c7` |
| Its sole parent | `3eee5165e94ef91a0a1ef6eb4ec4b80ae1ea528f` |
| Approved tree | `baf557450590d86178e041194002a32cedc2b04c` |
| Verdict-only sole direct child / target base | `ef88957af4c41bf18def689ba81c71e1a1d18696` |
| Verdict path | `openspec/changes/add-ephemeral-terminal-retention/artifacts/task-2-2-binding-independent-review-verdict-2026-10-17.md` |
| Verdict bytes / raw SHA-256 | 7,581 / `e4d5fda7fe358398a213f694a2b0a4cc6f01c5801f62112d902c100309e8f97d` |

The author re-read parents, tree, child diff, committed blob, current bytes and digest.
The child adds only the verdict. All previous dated evidence, task checkboxes, registry,
canonical/delta and numbered specs are untouched. The maintained reshape task 7.20
completion note changes only its inventory attribution and totals, with its matching guard.

## Configuration and observability

The user selected “Include the minimal internal carrier” on 2026-10-17.
The nullable internal options property uses an immutable internal three-value settings
record. Null and a bounded profile (count 1, age 1 minute, sweep 1 second) reach the real
engine options. The engine stores these settings; policy execution and public host mapping
do not exist yet. These are internal configuration profiles. Public factory validation,
hosting agreement, automatic age/count and API/compile baselines remain task 4.2.

The existing five-argument engine constructor changes only from private to internal.
Its body and the friend list do not change. A test decorator delegates all storage to the
real InMemoryInstanceRegistry, signalling only after actual successful Remove. This gives
an explicit definitive cleanup barrier without GC or polling. Real definitions, facade
starts, handles, commands and replay run against the real engine.

Existing internal EvictInstance/EvictTerminalInstances calls are destructive removal
probes, not automatic-policy evidence. They expose missing physical/provisional/commit
protection. Task 3.2 must make ownership authoritative; 4.2 must route or isolate the old
helpers consistently. No new public engine, eviction command or friend is introduced.

## Cases and observed first failures

The new active source contains 11 theories / 74 cases. Its adjacent TSV records each case,
outcome and first failure. All 52 ExpectedRed cases fail behavior assertions, with no build,
discovery, deadlock-guard or harness failure. The 22 controls pass. The owning project
excluding ExpectedRed passes 110/110: 88 existing cases and 22 new controls. No excluded
historical tests are reactivated.

| Cases | Coverage | Current failure |
|---:|---|---|
| 2 | Cancel after Init during cooperative first step, exact cancellation token, no facade/handle/key, changed-input retry gets new ID, settled cleanup, both profiles | Running orphan; runtime remains |
| 4 | Escaping observer exception after Init, active or already Completed state, exact exception identity, no handle/key, both profiles | Active stays Running; unpublished runtime remains |
| 36 | Root/parallel branch/ForEach item x no capacity/throttle/pool x single/bulk removal x both profiles | Incomplete timed-out physical body is removed |
| 4 | Synchronous step with terminal commit callback blocked before acceptance, both removal paths/profiles | Pending callback unit is removed |
| 2 | Completed retry winner with an older timed-out body still running | Older physical attempt is ignored |
| 2 | First attempt times out; second retry is cancelled before acceptance while the first runs; exact cancellation, fence then removal barrier | Running orphan is not fenced |
| 2 | Exception after timeout with immutable Failed status and physical body outstanding | Provisional unit is removed early |
| 4 controls | Accepted event wait/day-long delay, subsequent start-token cancellation, fake-time advance beyond profile age, key replay | Waiting handle/key remain |
| 2 controls | Synchronous completion and same-key replay | One coherent published unit |
| 12 controls | Accepted root/branch/item physical body, explicit cancellation/termination, both profiles | Owned until return; correct terminal result |
| 4 controls | Shared throttle/pool across instances after timeout, both profiles | Second body cannot enter before first returns; captured first state stays Failed, without promising public-handle retention under count pressure |

The unsettled-abort cases await successful registry removal after releasing the physical
task once their missing fence/protection assertion is fixed. Timeout removal cases also
require eligibility after return and immutable terminal state. These later assertions are
prospective: the current recorded first red failure stops those paths.

Physical work and pending callbacks are released and observed in finally blocks.
FakeTimeProvider controls timeout and age. Explicit barriers establish dispatch, retry,
cancellation, blocked governance, commit and removal. Ten-second wall-clock limits are
deadlock guards, never retention age or scheduling sleeps. Original caller errors are
checked before lifecycle assertions. Not-found or abort never proves external effects
did not run or that retrying an unbound key is safe.

## Exact validation prerequisites

- The DAG source tripwire compares both complete product files to exact Git-base
  transformations: only the carrier declarations and constructor access change are allowed.
  Other source, DAG delta and shipped public API fixture edits remain forbidden.
- The DAG append-only comparison uses the existing fail-closed active-or-dated-archive
  resolver. Its existing active/duplicate/archive resolution test remains active.
- Seven one-at-a-time mutation probes reject carrier renaming/extra members, public
  constructor exposure, engine-body drift, unrelated source edits, a missing inventory
  coordinate and reverted inventory-note totals. A positive probe moves this change to
  its dated archive in the disposable copy: the actual DAG comparison passes there.
  Every probe is restored before final validation; logs are in the external receipt folder.
- Recovery inventory adds one active source with 11 declarations: physical files 344 ->
  345, declarations 1424 -> 1435, active files 196 -> 197, active declarations 736 -> 747.
  Historical rows, retired dispositions and their digest remain unchanged. The maintained
  reshape task 7.20 note and exact phrase guard name this slice and the new totals; no checkbox changes.
- Package-source provenance changes only the ephemeral source digest from
  `5c302c6a41225aaac4a3040dbeaf24da85621f2eb5994571b938e2a8ac8c9e40` to
  `a5bb0b7745a4c762b56bc3a825e66b7fcd6872382f24ae4480cc07a22a335855`.
  This records actual internal source, not premature public API/package-surface expectations.
  Eleven other package entries and all executable API/compile fixtures are unchanged.

Author-local receipts/logs are in `.artifacts/publishing-validation/issue-1-ownership/`.
Freeze hashes and final validation results are external to the self-inclusive manifest,
avoiding a self-referential hash. Reproduce against the exact staged short copy with Git
history present:

```powershell
openspec validate --all --strict
dotnet build OrcaCore.slnx -c Release -warnaserror
./tests/OrcaCore.DeveloperSurface.Guards/pack-exact-package-feed.ps1 -OutputDirectory artifacts/phase0-packages -NoBuild
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj -c Release --no-build --no-restore --filter "Disposition!=ExpectedRed"
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj -c Release --no-build --no-restore --filter "Slice=Issue1Task3.1&Disposition=ExpectedRed"
```

The final command is intentionally nonzero (52 behavior failures); all others must be
green. Unfiltered owning-project/CI unit execution is intentionally red during this
uncommitted TDD review. This is not a green implementation checkpoint or push permission.
An initial long-path Infrastructure run exposed the missing source-inventory row and an
error deleting temporary compile-fixture output. The first short-copy run also identified
the corresponding maintained task 7.20 inventory note prerequisite. Its log is retained; the final full
isolated staged lane must pass. The initial run must not be reported as green.

## Carried P3 dispositions

| P3 | This target / next owning gate |
|---|---|
| Ledger row 209 mislabeled Retain | Deferred to next reviewed ledger reconciliation; historical 499-row artifact stays immutable here |
| Doc 17 section 17.4 signature guard | Deferred to 4.2 with implementing API and baselines |
| Master integration conflicts | Separate reviewed freeze and integration merge commit, never squash; no integration here |
| Temporary ownership-row mapping | Preserve until v1 fixture changes in 4.2 |
| DAG guard active-path coupling | Addressed here through the existing fail-closed active/archive resolver |
| Guide implementation-pending text lacks owner | Assign explicitly in reviewed 4.5/5.3 release-text reconciliation before shipped claims |
| Developer guide still calls policy draft | Deferred to 4.5 guide reconciliation; implementation remains pending |

The earlier metrics concern also carries forward: internal abort cleanup must not silently
impersonate an operator termination command. No new cleanup metric is emitted here;
classify and verify task 3.2 telemetry before claiming completion.

E:/ocv193 (38 changed paths) and E:/ocv196 (6) matched the d1d3cf6/5f17b7e blobs exactly,
then were removed with git worktree remove. Logs and preservation receipts remain in
the ignored `retired-validation-copies` evidence folder.
