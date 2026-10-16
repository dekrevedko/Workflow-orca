# DAG run Task 2.1 typed stream identity slice independent review verdict

**Date:** 2026-10-14
**Reviewer:** independent review
**Scope reviewed:** the forty-four-entry freeze on base `89e3b18aed62446583211478f1eb8545e4fb85ed`
(tree `40a09219…`), named by
`developer-facing-interface-section-08-task-8-4-dag-run-task-2-1-stream-identity-dirty-manifest-2026-10-07.txt`.
This is the first source slice of `admit-durable-dag-run-progression` Task 2.1, after my round-99
APPROVE of the canonical transition (17,572 B, `e89e7066…`), landed as checkpoint `1770b2b`,
evidence `18bf865` and activation `89e3b18`. It contains:
- `DurableAggregateKind` and the evolved `WorkflowStreamId` (seven approved `OrcaCore` API keys);
- workflow-only append/tail rejection in the InMemory, PostgreSQL and SQL Server providers and
  the shared test fake;
- four Core identity facts, two shared certification facts and three new guard facts;
- the `OrcaCore` API baseline, package-source provenance for the four changed packages, and
  declaration accounting;
- the round-99 carry-forward: transition evidence binding, whole-file canonical content pins, a
  corrected provenance artifact, and refreshed status blocks, Task 7.3 rows, design and tasks;
- the scope artifact, the request and the manifest.

**Authorization requested:** checkpoint of this partial source target and its direct-child
evidence. There is no activation.

This verdict authorizes that checkpoint and the evidence child, in the sequence the request states.
It authorizes no task checkbox (2.1, 2.4, reshape 8.4/8.5 stay open), none of the other 358 API
keys, and no run persistence, protocol, schema, pump or bridge work.

## Summary

The slice is small, exact and correctly guarded.

- **Identity.** `WorkflowStreamId` keeps its constructor, `InstanceId` and `workflow-…` rendering,
  and adds `AggregateKind`, nullable `DagRunId` and `ForDagRun(Guid)`, as RC-3 §2 specifies.
  Empty GUIDs, null instances and default values fail closed; `InstanceId` throws
  `InvalidOperationException` on a DAG or default value; equality and hashing separate the two
  families even at the same GUID.
- **API.** The `OrcaCore` baseline moves from `ef13f660…` to `a8bb5838…`, exactly the approved
  catalog: seven added keys, none removed or changed. The thirteen other baselines are unchanged.
- **Providers.** The only stream-taking entry points are append (through the batch) and load-tail.
  All three providers and the fake validate both before any storage, lock or connection access;
  SQL Server also validates in its internal state engine.
- **Carry-forward.** All five round-99 observations are handled or explicitly deferred. Notably,
  every canonical spec file is now content-pinned through a guard-source aggregate, which closes
  the gap my round-99 K8 control exposed.
- **Evidence.** Every anchor, record and inventory reproduces. Builds and every product, container
  and guard lane are green once a new, environment-wide NuGet advisory is downgraded to a warning
  (see "Environment"). Nineteen of my twenty-three targeted controls are red; the four
  green ones expose two test-strength gaps, both non-blocking.

The remaining observations are non-blocking.

## Method

Validation ran in one disposable detached worktree at `89e3b18`, and probes in a second. Both held
the same forty-four entries.

- Every script asserted its disposable location and that the main `HEAD` was still `89e3b18`.
- Main was never modified, and the review created no ref in the reviewed repository.
- Provenance, the canonical content record, the superseded catalog, Task 7.3 rows and the
  current-match refresh were recomputed with my own scripts.

### Environment

Two machine-level events affected this review, neither caused by the target.

- **NuGet audit.** `System.Security.Cryptography.Pkcs` 9.0.4, transitive through the SQL Server
  provider, now has a moderate (GHSA-2fmv-rv2c-59x7) and a high (GHSA-846f-f764-76mx) advisory.
  With `-warnaserror`, NU1902/NU1903 fail every build. The unmodified base `89e3b18` fails
  identically, and no manifest path touches a project, props or package file. I validated with
  only those two codes downgraded (`WarningsNotAsErrors=NU1902;NU1903`); both builds then report no
  other warning. The handoff's 2026-10-07 run predates the advisories.
- **SDK update.** A Visual Studio/.NET installer ran during the review and replaced SDK 10.0.401
  with 10.0.402, removing the 10.0 targeting pack while it ran. Steps that hit that window failed
  with NETSDK1226 or an SDK-resolver error and were rerun after the installer finished. The tables
  below report only completed runs, and state which SDK each used.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 44 lines, 3,003 B, `268d3728…` | identical (`268d3728fddf4225e266af65fe3bd09d1aeaa93f5dd5cd14cd27a5239dc17a49`); the NUL form has 44 entries (`41db6ac6…`); diff (37) plus untracked (7) paths are exactly the 44; nothing staged |
| Semantic record: 40 rows | 5,289 B, `cd5c653a…` | identical with the stated recipe |
| Active record: 43 rows | 5,965 B, `b0b9a6ed…` | identical (`b0b9a6ed38a3c225e349518b014cadfea6acef3d390d3c908acbfe694edb23cf`), equal to the fixture |
| All-file record, with status (my form) | not embedded | 44 rows, 6,120 B, `ab7e781141febeb670b02024b24802a5837bb576815cfccd62a99c4014622f9b` |
| Simulated checkpoint | tree `ac61630f2b4d26365ae824b0bb2dcb35b0ec8b48` | identical from a copied index (1,772 + 7 entries, no phantoms) and from a committed disposable copy; parent `89e3b18`; 7 A / 37 M; all 44 blobs equal the raw bytes (`hash-object --no-filters`); committed `diff --check` clean |
| Provenance (reviewer script) | 184 / 48,504 B / `fd6ad044…` | identical and unchanged from round 99: 173 synchronized, 8 superseded, 3 bootstrap-only, 0 pending |
| Whole canonical content | 14 / 1,635 B / `cee7cd37…` | identical, equal to the fixture rows and the guard-source pin; canonical and delta specs are byte-unchanged |
| Superseded provenance catalog | 14 / 2,468 B / `47b4201f…` | identical; it adds the round-99 artifact (`7fef563b…`) unchanged, and every listed file matches |

- **New artifacts.** The scope artifact (`64e20cbd…`) and provenance artifact (`f2739090…`) match
  their pins. The provenance artifact now states the untrimmed preamble recipe, which resolves my
  round-99 P3-4; preambles are unchanged at `595528c6…`.
- **Registry.** `approvedDagRunTransitionEvidence` binds `1770b2b`, tree `61b5900c`, evidence
  `18bf865` and my verdict `e89e7066`, equal to the source-pinned record. The guard checks the real
  tree, a single-parent direct child, the verdict's addition and bytes, and one terminal APPROVE.
  This resolves my round-99 P3-1.
- **Status.** The guide blocks name the checkpointed transition and limit this target to typed
  identity; all 22 Task 7.3 rows and the whole-artifact pin reproduce; a dated 2026-10-07 note is
  appended to `00-stack-decisions.md` and the 2026-10-05 entries are kept as history.
- **Current matches.** My emulation of `refresh-review-manifest-current-matches.ps1 -Check`, which
  still cannot run under Windows PowerShell 5.1, gives 16 entries, 100 matches and 0 stale.
- **Accounting.** 342 physical files / 1,415 declarations, active 194 / 727: two new files and nine
  new declarations (four Core, two certification, three guard).

## 2. Validation

SDK 10.0.401 unless noted; NU1902/NU1903 downgraded to warnings as described above.

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 errors; the only warnings are NU1902/NU1903 |
| Exact package feed / package fixtures | 12 packages / green fixtures pass, exactly `dag-hosting` red |
| Compile fixtures (`Green`) / public API baseline | green / 14/14 |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 354 / 79 / 99 / 37 / 24 / 100 |
| PostgreSQL / SQL Server / Integration | 103 / 74 / 11 |
| `Disposition=Infrastructure`, Release | 247/247 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / Debug / strict OpenSpec | 16/16 / 16/16 / 21/21 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation (SDK 10.0.402, after a full rebuild) | 247 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared (SDK 10.0.402) | Infrastructure 247/247 |

## 3. Source and test review

| Item | Finding |
|---|---|
| Contract §2 | constructor, property and workflow rendering retained; `DurableAggregateKind` closed at 0/1; `ForDagRun` rejects `Guid.Empty`; DAG text `dag-run-{D}`; no scalar conversion or raw-string constructor added |
| Equality | the record struct compares the private instance field, kind and `DagRunId`; same-GUID workflow and DAG values differ; the in-memory dictionaries keep their workflow semantics |
| Serialization | `WorkflowStreamId` is never serialized; SQL keys use `InstanceId.Value` after validation |
| Port coverage | append and load-tail are the only stream-taking ports; all providers, the SQL Server engine and the fake reject DAG and default values first |
| Tests | Core facts cover rendering, wrong-family access, family-separated hashing and empty/default values; certification proves rejection leaves a same-GUID workflow stream untouched and still appendable |
| Guards | whole canonical text pinned through a source aggregate with load-bearing synthetic drift/missing checks; transition evidence checked against real Git objects; API diff checked against the base blob and live extraction |

## 4. Independent negative controls

Twenty-five cases ran in the disposable probe copy, guard cases on the corpus and identity lanes
(19 tests) and source cases on the owning test project after a rebuild. Every guard case except
L0 and L17 cleared the active freeze first. L8 onward ran on SDK 10.0.402 after the installer
finished; L0–L7 ran on 10.0.401.

| # | Mutation | Result |
|---|---|---|
| L0 / L1 | none (freeze active) / freeze cleared | green / green, 19/19 |
| L2 | activated task 1.4 unchecked again | green; also green under the whole Infrastructure lane, 247/247. See P3-1 |
| L3 | task 2.1 checked on this partial slice | **red**: whole `tasks.md` pin |
| L4 | unowned canonical requirement body edited (`workflow-contracts`) | **red**: "Canonical content drift: openspec/specs/workflow-contracts/spec.md" |
| L5 | that edit plus a coherent fixture row and aggregate rehash | **red**: guard-source aggregate pin |
| L6 | one canonical content row dropped from the fixture | **red**: named missing path |
| L7 | fixture cites activation `89e3b18` as transition evidence | **red**: source-pinned evidence record |
| L8 | `DagRunId` line removed from the API baseline | **red**: exact baseline digest |
| L9 | transition verdict edited by one byte | **red**: verdict bytes and immutable history |
| L10 / L11 / L12 | scope artifact / new provenance artifact / superseded artifact edited | **red** / **red** / **red** |
| L13 / L14 | `CLAUDE.md` status reverted / new `00-stack-decisions` note edited | **red** / **red**: document pins and Task 7.3 rows |
| L15 | reshape 8.4 checked | **red** |
| L16 | crosswalk declarations back to 1,414 (crosswalk lane) | **red** |
| L17 | freeze active, `WorkflowStreamId` comment edited | **red**: active content record |
| L18 | guard source and fixture both cite `89e3b18` as evidence | **red**: real Git parent check |
| S1 | source: `ForDagRun` accepts `Guid.Empty` | **red**: Core empty/default fact |
| S2 | source: DAG text rendered with the `N` format | **red**: Core rendering fact |
| S3 | source: InMemory tail check removed (fake keeps it) | **red**: certification, InMemory case only |
| S4 | source: InMemory append check removed | green. See P3-2 |
| S5 / S6 | source: PostgreSQL early append / tail check removed | green / green. See P3-2 |
| R0–R2 | restored and rebuilt | green; worktree equals the manifest and every target byte equals main |

## 5. Non-blocking observations (P3)

- **P3-1: the activated task 1.4 is no longer protected.**
  - The `tasks.md` pin still normalizes the 1.4 checkbox, and no guard requires 1.4 to be
    complete. Un-checking it leaves the whole Infrastructure lane at 247/247 (L2).
  - The transition's real approval is bound, so no authority depends on the checkbox. But task
    states are meant to be checked literally, and the runtime-view precedent locked 1.4 as
    complete in its first source target.
  - In the next target, drop the 1.4 exemption and require 1.4 complete.
- **P3-2: the "before storage" ordering is not test-enforced.**
  - Removing InMemory's early append check (S4) leaves the certification green. Without it, the
    append creates the DAG stream entry and adds the batch's events before a later `InstanceId`
    read throws, a real partial write. The "WithoutEffects" fact only inspects the same-GUID
    workflow stream, which no DAG write touches.
  - Removing PostgreSQL's early checks (S5, S6) also stays green: later reads throw the same
    exception inside a rolled-back transaction or before the query runs, so nothing leaks, but
    the connection is opened first.
  - The shipped code is correct. When the next 2.1 slice reopens these providers, make the
    rejected batch carry an effect visible through workflow ports (for example an outbox or inbox
    record) and assert it never appears.
- **P3-3: one stale status sentence.** `CLAUDE.md` line 308 still says "this atomic 1.3/1.4 target
  still requires independent review/checkpoint", contradicting the refreshed sentences after it.
  No other document carries it.
- **Recorded:**
  - A default `WorkflowStreamId` throws from `ToString()`, because rendering reads `InstanceId`.
    This is deliberate and tested, and ports reject defaults as the contract requires, but logging
    or debugging a default value will throw.
  - Provider validation is written as `_ = streamId.InstanceId;`, relying on the getter's throw.
    It works and matches the contract's exception type; a named helper would state the intent.
  - The new transition-evidence guard also requires the evidence commit to be an ancestor of
    `HEAD`, which earlier approval guards do not. It holds on this branch but would fail after a
    squash.
  - The archive-resolver tightening, lane telemetry, the UnsafeAccessor/expression ban and the
    Hosting audit remain deferred to their named later slices.
  - ReSharper InspectCode was not rerun independently; the author's scoped run reported zero
    errors.

## 6. Reviewer hygiene and checkpoint sequence

`HEAD` is `89e3b18aed62446583211478f1eb8545e4fb85ed`, with the forty-four frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

Checkpoint sequence, as the request states:
1. Confirm zero drift against the 44-entry manifest and the anchors above.
2. Stage exactly those 44 paths on `89e3b18`, expecting tree `ac61630f…`, and commit.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.

There is no activation commit. Do not check 2.1, 2.4, reshape 8.4/8.5 or any later task. The next
source target is the remaining Task 2.1 run protocol and provider work, reviewed before its
checkpoint. Separately, the NuGet advisory needs a reviewed package fix before CI can pass
`-warnaserror` on any commit.

## Determination

The typed identity matches the approved contract and API catalog exactly, fails closed on every
invalid value, and keeps workflow behavior byte-compatible. Workflow-only ports reject DAG and
default identities before touching storage, the round-99 carry-forward is implemented with real
guards, and the controls show the important decisions are protected. The observations above are
non-blocking.

**Verdict:** **APPROVE**
