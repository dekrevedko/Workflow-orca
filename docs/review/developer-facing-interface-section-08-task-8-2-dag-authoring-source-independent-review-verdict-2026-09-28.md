# Task 8.2 typed DAG authoring source independent review verdict

**Date:** 2026-09-28
**Reviewer:** independent review
**Scope reviewed:** the thirty-three-entry freeze on base `db5f3fb2b0733c06ae052536748f5a445bf52de7`,
named by `developer-facing-interface-section-08-task-8-2-dag-authoring-source-dirty-manifest-2026-09-28.txt`.
It covers `admit-dag-authoring-friend-boundary` tasks 2.1–2.5 and reshape Task 8.2.
**Authorization requested:** approval and checkpoint of the Task 8.2 DAG authoring source. This
verdict does not grant it.

## Summary

Most of the slice is correct:
- **Friend grant:** the single compiled friend `OrcaCore -> OrcaCore.Dag` is the only new grant.
  `OrcaCore.Dag` still references only `OrcaCore`.
- **Shared hash:** `DefinitionFingerprint.ComputeCanonicalHash` is byte-equivalent to Core's former
  private hash (UTF-8, SHA-256, uppercase hex over the same input), so no workflow digest can
  drift.
- **Catalog:** the seven `DAG-AUTH-*` descriptors match document 17 exactly.
- **Public surface:** it matches §17.2.6 member-for-member, with no public constructor or factory.
- **Authoring behavior:**
  - dependency arrays and plan snapshots are copied;
  - authored ordinals and shared diagnostic ordering are preserved;
  - `Build` and `TryBuild` return the same diagnostics;
  - mappers are never executed.
- **Validation:** every lane reproduces.

Three blocking defects remain:

- **ZZZ-1 (P2): the member-reference guard misses non-public `OrcaCore` *type* references.**
  - Four references compile under the friend and leave the guard green:
    - `typeof` of an internal type;
    - a field of an internal type;
    - an implicit implementation of the internal `IWorkflowDefinitionRuntimeMetadata`;
    - an `is` test against an internal interface.
  - The canonical requirement says the guard SHALL reject every other non-public `OrcaCore` type or
    member.
- **ZZZ-2 (P2): `TryBuild` crashes the process on a valid, large DAG.** Its recursive cycle search
  overflows the stack (`0xC00000FD`) on a 20,000-node chain whose dependencies are wired forward.
- **ZZZ-3 (P2): four normative passages still say the friend is not compiled.** Documents 03, 08
  CP-020, and 10 PR-005, and document 17 §17.2.6, say this, while the target compiles the friend
  and its other documents now describe eight current friends.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
thirty-three entries. Every script asserted its disposable location and that the main `HEAD` was
still `db5f3fb2`. Main was never modified, and the review created no ref in the reviewed
repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 33 lines, 2,431 B, `aac89bfc…d135` | identical |
| Semantic record: 29 rows, `path<TAB>bytes<TAB>sha256`, excluding request, manifest, history catalog, and `review-manifest-provenance.json` | 3,986 B, `e0f8ff4a…2fa8` | identical |
| Active-freeze record in `review-manifest-provenance.json` (all rows except that fixture) | 4,605 B, `6a270ce2…` | identical |
| All-file content record, 33 rows (reviewer) | — | 4,760 B, `de3d3eb4cbc1bfdc10b05a59ba36f21d7446e653a8164fa31e8ae53ffd0ba584` |
| Simulated checkpoint | tree `4b985e95…` | `4b985e955abcf124cdd47d38c0bfa98ce2a0f16e` from a copy of the live index and from a committed disposable copy; parent `db5f3fb2`; 6 A / 27 M; all blobs equal the raw bytes |

- **Prior chain:**
  - checkpoint `0a99461` has the round-82 approved tree `0eb146f6`;
  - evidence `0837d7c` is its only child and adds the round-82 verdict byte-exact (10,446 B,
    `950a77f7…`);
  - activation `db5f3fb` appends only a review-activated note to task 1.4.
- **Pins:**
  - all 22 Task 7.3 rows and the artifact digest reproduce;
  - so do the Task 8.0 map pin and the validation-artifact pin (`549b9455…`);
  - the maximal current-match refresh reproduces the fixture byte-for-byte.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 231/231 |
| `ExecutableBehaviorExpectedRedGuards` | exactly 14 failures |
| Package fixtures, `Green` / `ExpectedRed` | 8 green / exactly `dag-hosting` red |
| Strict OpenSpec | 19/19 |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 231 passed plus the same 14 expected-red |

## 3. Correct parts of the slice

- **Friend and package edge:**
  - `AssemblyInfo.cs` adds exactly `InternalsVisibleTo("OrcaCore.Dag")`;
  - the exact product-friend fixture adds only `OrcaCore->OrcaCore.Dag`;
  - `OrcaCore.Dag.csproj` references only `OrcaCore`;
  - `OrcaCore.Dag.Hosting` gains no new friend.
- **Hash and diagnostics:**
  - Core now calls `DefinitionFingerprint.ComputeCanonicalHash($"{CodecFormat}|{canonicalStructure}")`,
    the same algorithm and input as before;
  - the Core catalog contract test now checks every catalog code, including the seven DAG codes.
- **API:** the `OrcaCore.Dag` baseline adds exactly the §17.2.6 types and members. `DagNodeRef`,
  `WorkflowDagBuilder`, and `WorkflowDagPlan` have internal constructors only.
- **Behavior, confirmed by tests and my probes:**
  - `Build` and `TryBuild` carry the same sorted diagnostics, and locations sort in authored order;
  - duplicate node, duplicate dependency, self, foreign, cyclic, and missing/duplicate mapping
    faults are reported;
  - dependencies are copied at `DependsOn`, and again into the snapshot;
  - mappers are stored, never invoked;
  - a mapper body change leaves the fingerprint unchanged, but a node rename changes it.
- **Metadata guard, member references:**
  - calling an internal member (G5) or reading an internal interface property (G6) turns it red;
  - a public member reference (G7) stays green;
  - generic `TypeSpec` parents decode, and unsupported shapes fail closed.

## 4. ZZZ-1 (P2): internal type references escape the metadata guard

- **How the guard works:** it enumerates only `MemberReference` rows. A reference to an internal
  `OrcaCore` type that uses no member emits only `TypeRef`, `TypeSpec`, `InterfaceImpl`, or
  signature rows, so the guard never sees it.
- **Probes:** each was compiled into `OrcaCore.Dag` under the friend, and each left
  `DagAssembly_ReferencesOnlyTheApprovedInternalOrcaCoreMembers` green.

  | Probe | Reference |
  |---|---|
  | G1 | `typeof(OrcaCore.WorkflowDiagnosticCatalog)` |
  | G2 | a static field of the internal `WorkflowDiagnosticDescriptor` |
  | G3 | a DAG class implicitly implementing the internal `IWorkflowDefinitionRuntimeMetadata` (the runtime-definition metadata seam) |
  | G4 | `value is OrcaCore.IWorkflowDefinitionRuntimeMetadata` |

- **Conflict with the contract:** the synchronized canonical `developer-facing-surface` requirement
  says a compiled-metadata guard "SHALL reject every other non-public `OrcaCore` type or member
  referenced by `OrcaCore.Dag.dll`". Its scenario fails the guard for "any other non-public
  `OrcaCore` type, member, or overload".
- **Overstated artifact:** the validation artifact's statement that "an unlisted internal type …
  turns the focused guard red" holds only when a member of that type is also referenced.

## 5. ZZZ-2 (P2): `TryBuild` can crash the host instead of returning diagnostics

- **Cause:** `DetectCycles` uses a recursive local function whose depth equals the longest
  dependency path, followed from each node in authored order.
- **Probe:**
  - I authored node builders first and then wired node *i* to depend on node *i+1*. This is valid
    and acyclic, but its dependencies point forward.
  - At 2,000 nodes `TryBuild` succeeded. At 20,000 nodes the xUnit test host died with exit code
    `-1073741571` (`0xC00000FD`, stack overflow), which cannot be caught.
- **Why it matters:** no normative text bounds authored DAG size. A validation API must return a
  `Validation` result, not terminate the process.

## 6. ZZZ-3 (P2): normative text still says the friend is not compiled

This target compiles the friend. It also updates `CLAUDE.md`, Decision 22 (with a new dated
2026-09-28 entry), the solution architecture, the overview, and document 17's product-friend set to
"current eight". But these normative passages still say otherwise:

| Document | Status in target | Text |
|---|---|---|
| 03, lines 329–330 | unchanged, Task 7.3 row 14 | "it is not yet a compiled grant" |
| 08 CP-020, line 129 | unchanged | "The friend is not yet in compiled metadata" |
| 10 PR-005, line 44 | unchanged, Task 7.3 row 18 | "it is not yet a compiled grant or a reverse package reference" |
| 17 §17.2.6, line 1175 | in the target, but this sentence was not updated | "This is an approved contract, not a current friend grant or authorization for Task 8.2 source" |

The request asked the reviewer to sweep for exactly this wording. As frozen, the normative corpus
contradicts both the compiled metadata and the target's own guide and binding documents.

## 7. Non-blocking observations (P3)

- **Self-dependency is reported twice.** It yields both `DAG-AUTH-DEPENDENCY-002` and
  `DAG-AUTH-DEPENDENCY-003` (probe B1). Document 17 lists self-dependency and cycle as distinct
  faults, so either suppress the cycle diagnostic for a self-loop or document the double report.
  The behavior test asserts with `Contain`, not an exact set.
- **The DAG fingerprint omits `TRunInput`.**
  - The fingerprint includes ordinals, node IDs, child identity, version, and fingerprint,
    dependencies, and codec, as the approved design listed. It does not include the DAG's own
    `TRunInput` schema identity.
  - Workflow fingerprints do include their typed-contract schema identities, and canonical requires
    "exactly inspectable authored structure plus codec format".
  - Decide this before Task 8.6 registration-conflict semantics, since a changed run-input type
    under the same version would otherwise not conflict.
- **`OutputOf` rejects a legitimate `null` output.** `output is not TDependencyOutput typed`
  treats a successful `null` output as invalid. Task 8.3 should decide null-output semantics.
- **Behavior coverage is thin.** There are four facts. Add exact diagnostic sets per code, a
  large-graph case in both wiring orders, and a golden Core fingerprint assertion.

## 8. What a superseding freeze needs

1. **ZZZ-1:** also enumerate `TypeReferences` (and walk `TypeSpec` signatures, base types,
   interface implementations, and field and method signatures) that resolve to non-visible
   `OrcaCore` types, and reject them. Add probes G1–G4 as permanent negative controls. Correct the
   artifact wording.
2. **ZZZ-2:** make cycle detection iterative (an explicit stack or Kahn's algorithm). Add a
   large-graph test, for example 100,000 nodes in both wiring orders.
3. **ZZZ-3:**
   - update documents 03, 08 CP-020, and 10 PR-005, and the §17.2.6 sentence, to the candidate or
     compiled status used elsewhere in this target;
   - refresh Task 7.3 rows 14 and 18 and the digest.
4. Refresh the affected pins and the validation artifact, and record this `REJECT`.

The friend attribute, the hash extraction, the catalog, the public API, the fixtures, and the other
documentation can be refrozen unchanged.

## 9. Reviewer hygiene

`HEAD` is `db5f3fb2b0733c06ae052536748f5a445bf52de7`, with the thirty-three frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

## Determination

The authoring slice is structurally faithful to §17.2.6, and its hash extraction is safe. The
enforcement mechanism that justified the friend grant still leaves the approved type-reference
boundary open. `TryBuild` can terminate the host on a valid graph, and the normative documents
contradict the compiled friend state.

**Verdict:** **REJECT**
