# DAG hosting runtime-view source independent review verdict

**Date:** 2026-10-04
**Reviewer:** independent review
**Scope reviewed:** the thirty-three-entry freeze on base `13fe5b996e4758e383ea6ac68d86bdaf10b940b8`,
named by
`developer-facing-interface-section-08-task-8-3-runtime-view-source-dirty-manifest-2026-10-04.txt`.
This is `admit-dag-hosting-runtime-view` source tasks 2.1–2.3: the ninth friend, the runtime view,
the Hosting mapping adapter, and the metadata and behavior guards. Task 2.4 is the checkpoint
gate this verdict decides.
**Authorization requested:** checkpoint of this source target only. This verdict does not
authorize:
- `Complete` promotion (tasks 3.1/3.2) or archival;
- the durable bridge, codec or child-start work, or reshape 8.4/8.5;
- any claim that DAG execution works.

## Summary

The source implements the approved canonical contract exactly:

- **Friend grant.** `OrcaCore.Dag` gains the single grant `InternalsVisibleTo("OrcaCore.Dag.Hosting")`.
  The friend allowlist fixtures name nine product friends. No test friend, package edge, or
  public API change is added; the public baseline passes 14/14.
- **Runtime view.** `DagRuntimeView<TRunInput>`, `DagRuntimeNodeDescriptor`, and
  `DagMappedInputResult` match the 15 approved signatures.
  - Mappers are now stored as typed wrappers, so `Build` never invokes them.
  - The DAG fingerprint inputs are unchanged: ordinals, node IDs, child identities, and
    dependency ordinals.
- **Evaluator.** It does the following before invoking the mapper:
  - requires the exact plan-local node reference;
  - eagerly validates every declared resultful output against its declared type, including
    outputs the mapper never reads;
  - rejects missing, extra, foreign, and resultless entries;
  - copies the caller's map into a read-only snapshot.

  Mapper exceptions, including mapper-thrown `OperationCanceledException`, become
  `DAG_INPUT_MAPPING_INVALID`; the three integrity exceptions escape.
- **Null policy.** `OutputOf` now returns a present null for reference and `Nullable<T>`
  outputs. Missing outputs and null for non-nullable value types stay invalid.
- **Hosting adapter.** It consumes exactly the three types and fifteen members. The metadata
  guard proves this on the shipped `OrcaCore.Dag.Hosting.dll` by decoding TypeRefs and MemberRefs
  separately and requiring exact equality. The adapter is not yet wired into registration or the
  coordinator, as scoped.
- **Round 89's notes.** Both are resolved:
  - status now names the approved atomic checkpoint `a0da21ba` and "compiled in this source
    candidate";
  - schema 8 renames the contract catalog to `historicalRuntimeViewContractArtifacts`, and design
    §12 says which entry is rejected and which governs.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes and my own JetBrains InspectCode run ran
in a second one. Both held the same thirty-three entries. Every script asserted its disposable
location and that the main `HEAD` was still `13fe5b9`. Main was never modified, and the review
created no ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 33 lines, 2,344 B, `6d92fc3d…83e0` | identical; the union of diff and untracked paths is exactly 33; nothing staged |
| Semantic record: 29 rows, ordinal sort | 3,909 B, `1c7afb55…79c1` | identical with the stated recipe |
| Active record: 32 rows (reviewer) | pinned in the fixture | 4,526 B, `21f8a9ccc46e4814850694d71296d86b338c59b9a77559df9dcceec0d8014ff7`, equal to the fixture |
| All-file content record, 33 rows (reviewer) | — | 4,681 B, `17b66a0e825cdf98a28193ae792ea0549b40af47b0805f277cf259e4d7fd9993` |
| Simulated checkpoint | tree `bd378abbadc5ba6989831814be1e67db0c9dcfa9` (handoff) | identical from a copied index and from a committed disposable copy; parent `13fe5b9`; 8 A / 25 M; all blobs equal the raw bytes |

- **Atomic chain:**
  - `a0da21b` has tree `572d86f6` and parent `64d97c6`;
  - `6c8bcd0` is its only child and adds the round-89 verdict byte-exact (9,749 B, `cce8c519…`);
  - `13fe5b9` checks only task 1.4.
  - The new guard binds source tasks to that actual approval, the evidence commit that adds the
    verdict, and the exact tree.
- **Provenance:** unchanged at 180 rows, 47,347 B, `40d4d8c0…`. Canonical specs and both deltas
  are untouched.
- **Pins:**
  - doc 17 numbered block `4f77f455…`; §17.5 `4e828319…`; CP-020/CP-022 `76fbb969…`;
  - Task 8.0 map `deef826a…`; reshape handoff unchanged at `ec0492e7…`;
  - all 22 Task 7.3 rows, with digest `caa9681f…`;
  - validation artifact `76d74024…`; behavior harness `ee97d40d…`;
  - 427 baseline and 91 append-only history records are byte-exact.
- **Other fixtures:**
  - only the Dag and Dag.Hosting package-source hashes change;
  - the crosswalk adds 1 file and 3 declarations;
  - completed reshape task 7.20 changes only its maintained inventory line (339 → 340 sources,
    1,402 → 1,405 declarations), as the approved Task 8.2 checkpoint did.
- **Current-match refresh:** emulated, because Windows PowerShell 5.1 lacks `SHA256.HashData`.
  16 entries, 106 matches, 0 stale.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / 8 green, exactly `dag-hosting` red |
| Compile fixtures (`Green`) / public API baseline | green / 14/14 |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 243/243 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / strict OpenSpec | 15/15 / 20/20 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation | 243 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 243/243 |
| Planned activation: task 2.4 checked on that state, no guard edit | Infrastructure 243/243 |

## 3. JetBrains InspectCode (independent run)

- **Run:** InspectCode 2026.2.3.1 over `OrcaCore.slnx`, Release, scoped to the Dag and
  Dag.Hosting sources and the new guard.
- **Result:** 59 findings, 41 warnings and 18 notes, zero errors. This equals the retained author
  SARIF exactly, by count and by rule distribution.
- **Triage:** none is a functional defect.
  - Most are unused, naming, or cosmetic findings: unused positional, auto-property, member and
    type findings, naming, redundant qualifiers, a primary-constructor suggestion, and
    "can be private" notes.
  - Three are "always false by nullable annotation" null checks, deliberate at the friend boundary
    (`DagRuntimeView.cs:28`, `DagAuthoring.cs:426`).
  - One is the intentional phantom type parameter on `DagNodeRef<TOutput>`.

## 4. Independent negative controls

Each mutation ran in the probe copy and was restored byte-exact. The guards were rebuilt after
every source change.

| Control | Mutation | Result |
|---|---|---|
| P0 | unmodified: exact references / behavior harness (34) / policy self-test / canonical gate | green / green / green / green |
| P2 | Hosting reads internal `DagNodeRef.AuthoredOrdinal` | **red**: production member set differs |
| P3 | Hosting signature-only reference to `DagNodePlan<int>` | **red**: production type set differs |
| P4 | Hosting calls `DagMappedInputResult.Invalid()` | **red**: production member set differs |
| P5 | `AcceptsNull` accepts non-nullable value types | **red**: harness "null nonnullable" |
| P6 | lazy instead of eager output validation | **red**: harness "invalid map must fail before mapper" |
| P7 | extra-entry count check removed | **red**: harness "foreign output map" |
| P8 | caller map passed to the mapper without a snapshot | **red**: harness "output map copied before mapper" |
| P9 | second grant `InternalsVisibleTo("OrcaCore.Dag.Tests")` | **red**: exact product-friend list |
| P10 | harness drops one assertion, its hash repinned in guard source | **red**: 33 ≠ 34 executed assertions |
| P11 | mapper `OperationCanceledException` excluded from normalization | **red**: OCE escapes the harness |
| P1 (informational) | Hosting reads `NodePlans` through private reflection | green: reflection is outside metadata scope (P3-1) |
| R0 | restored | green; porcelain equals the manifest; the 33 target files equal main byte-for-byte |

## 5. Non-blocking observations (P3)

- **P3-1: reflection is outside the guard.** A private reflection read of `NodePlans` from Hosting
  (P1) creates no TypeRef or MemberRef, so the metadata guard stays green. Neither DAG package uses
  `System.Reflection` today, and the authoring guard has the same scope. Consider rejecting
  `System.Reflection` member references from `OrcaCore.Dag.Hosting.dll` in a later target, because
  the canonical requirement forbids a reflection bridge.
- **P3-2: the harness assembly isn't inspected.** The behavior harness gets friend access by
  compiling the adapter source into a probe executable named `OrcaCore.Dag.Hosting`. This is
  disclosed, the harness is hash-pinned, and today it uses only the adapter and public APIs.
  - Running the same `Inspect` allowlist over the harness assembly would keep it to the three
    types and fifteen members.
  - When 8.4/8.5 wire the adapter, behavior must also be proven through the shipped Hosting
    assembly.
- **P3-3: host misuse looks like a mapping failure.** Null arguments and extra, foreign, or
  resultless map entries return `DAG_INPUT_MAPPING_INVALID`. Failing closed is acceptable, but the
  8.4 bridge contract should state that the bridge supplies exactly the declared successful
  resultful outputs. Otherwise a bridge bug would be reported as a node mapping failure.
- **Recorded (expected):** "compiled in this source candidate, awaiting independent source
  approval/checkpoint" goes stale at this checkpoint. It is pinned, so refresh it in the 3.1/3.2
  closeout target, not in activation.

## 6. Reviewer hygiene and checkpoint instructions

`HEAD` is `13fe5b996e4758e383ea6ac68d86bdaf10b940b8`, with the thirty-three frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 33 manifest paths from the live index;
- confirm the tree is `bd378abbadc5ba6989831814be1e67db0c9dcfa9` with parent `13fe5b9`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it;
- clear the active freeze.

The activation should then check task 2.4 only; that rehearses green. Tasks 3.1/3.2 (the
`Complete` promotion bound to this verdict, checkpoint and tree) and reshape 8.4/8.5 (the durable
bridge) remain separate reviewed targets.

## Determination

The ninth friend is narrow and verified on the compiled Hosting assembly. The runtime view
implements the approved contract's identity, eager validation, snapshot, null, and exception rules
before any mapper call. The canonical, public, and historical surfaces are unchanged apart from the
intended friend and status updates.

**Verdict:** **APPROVE**
