# Reshape Task 8.1 package and registration reconciliation independent review verdict

**Date:** 2026-09-27
**Reviewer:** independent review
**Authorization requested:** approval and checkpoint of reshape Task 8.1. This verdict authorizes
that checkpoint for the exact target in scope A below only. It does not approve tasks 8.2–8.10.

## What was reviewed

**Scope A: the Task 8.1 target.** These are the seven uncommitted paths on `HEAD`
`f316d36fbbac706e55ddbe2e5d2a4529b98cbc67`, with nothing staged. No author-frozen manifest or
review request existed, so this verdict freezes the target by its own anchors:

| Status | Path | Bytes | SHA-256 |
|---|---|---|---|
| ` M` | `openspec/changes/reshape-developer-facing-interfaces/tasks.md` | 74,831 | `24ae691f7cbdd33cf0f3fd5c8a2805fda3c7ed913f3552bb5d364ecf8db84bda` |
| ` M` | `src/OrcaCore.Dag.Hosting/OrcaCoreDagHostingServiceCollectionExtensions.cs` | 2,500 | `b79ddf0ae0e64e20fa6b9843ff7e974ecfa84ebc85740b0480ec5ee89edaf8f1` |
| ` M` | `tests/OrcaCore.DeveloperSurface.BehaviorScenarios/FacadeHostingScenarioHost.cs` | 42,998 | `8c66bb8c03cdff14563af55f2227fdb6ee5f1591b66fb297a8d7aa32f1a621c5` |
| ` M` | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/package-source-provenance.json` | 2,698 | `936cacfdb1dd4d94fffbb9d21193b79354a38bc3327bc17601665977b1a531b6` |
| ` M` | `tests/OrcaCore.DeveloperSurface.Guards/TaskAccountingGuards.cs` | 3,276 | `1049fe4175a99008f178c1e1299daf6ebf8e828066f93a5b614fb9027fc840ea` |
| `??` | `openspec/changes/reshape-developer-facing-interfaces/artifacts/task-8-1-package-and-registration-reconciliation-2026-09-27.md` | 2,200 | `cb7a7366c89f19deee65acbf03467daa3c998897110d92bb2ee8399ac2808977` |
| `??` | `src/OrcaCore.Dag.Hosting/DagHostingServices.cs` | 404 | `f8148fb20ea64b3e7c3c182d17f845f41cde11a5f1a4423d78918c8c7173ef65` |

| Anchor | Value |
|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 7 lines, 551 B, `1e0ade8e1819dd5d39e2683f19dea0784e0e7cb4a7b60656b6325ac364bc66bc` |
| Content record, all seven rows | 1,042 B, `ebe9428eef90d362f67e38f1e5a2a04f12711f7464ba9b37c6bcb114f54a8059` |
| Simulated checkpoint | tree `a342e5c07055468bdd4909b7828aa905aded345d`, parent `f316d36f`, 2 A / 5 M |

The record uses the series convention: the two status bytes, then TAB, path, TAB, byte length, TAB,
and lowercase SHA-256; rows sorted ordinally, LF-joined, with one final LF. Both anchors were
identical when the review ended.

**Scope B: the committed activation base.**
- `f316d36` is the Task 8.0 activation, whose changes from evidence commit `af0a957` had not been
  reviewed.
- It changes five files:
  - the gate-status wording in `docs/implementation/README.md` and the phased plan;
  - a two-line activation note in the reshape `tasks.md`;
  - Task 7.3 row 9 (`1674c933…`);
  - the Task 7.3 artifact digest in `OpenSpecCorpusGuards.cs` (`a966f1d8…`).

**Not in scope:**
- tasks 8.2–8.10;
- the nine DAG expected-red scenarios, which are still expected-red;
- any review packet or verdict files added later.

## Summary

- **Scope A: approved.**
  - Task 8.1 reconciles the two existing DAG projects in place. There is no new project, package
    ID, reference, or public API.
  - `AddOrcaCoreDag` still validates the positive node limit and the durable-engine role before any
    mutation. It is idempotent for identical options, and it rejects conflicting options without
    mutating the collection.
  - It now registers the internal DAG coordinator and definition registry that canonical
    `developer-facing-surface` ("only the DAG coordinator and registry are added") and document 17
    require. Both are inert until 8.2–8.6 give them behavior.
  - The extended must-green hosting scenarios catch each real regression I injected.
- **Scope B: correct, but outside convention.**
  - Every committed identifier and hash in `f316d36` is accurate, and the clean base is green.
  - Every earlier activation changed only two values in `review-manifest-provenance.json`. This one
    also edited a Task 7.3-pinned source and refreshed its pins outside a reviewed freeze.
  - This review now covers those bytes (P3-1).

No P0–P2 findings.

## Method

Three disposable detached worktrees were used:
- validation of scope A;
- a clean checkout of scope B;
- mutation probes.

Every script asserted its disposable location and that the main `HEAD` was still `f316d36`. Main
was never modified, and the review created no ref in the reviewed repository.

## 1. Validation

| Lane | Scope B: clean base `f316d36` | Scope A: target on `f316d36` |
|---|---|---|
| Release non-incremental `-warnaserror` build | 0 warnings, 0 errors | 0 warnings, 0 errors (Debug as well) |
| Exact package feed | 12 packages | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | — | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | — | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 226/226 | 226/226 |
| `ExecutableBehaviorExpectedRedGuards` | exactly 14 | exactly 14 |
| Strict OpenSpec | — | 18/18 |
| Reshape ledger | 130 / 29 | 131 complete / 28 open / 159 |
| `git diff --check`, worktree and committed simulated checkpoint | clean | clean |
| Guards on the committed simulated checkpoint | — | 226 passed plus the same 14 expected-red |

- **Task 8.0 gate:** it stays green, and no compiled acceptance trait was added.
- **Hosting scenarios:** `six-hosting-entry-owners` and `role-exclusivity-and-dependencies` turn
  green at task 7.10, so they run in the must-green Infrastructure lane.

## 2. Scope A review

- **Packages:**
  - `OrcaCore.Dag -> OrcaCore` and `OrcaCore.Dag.Hosting -> OrcaCore.Dag + OrcaCore.Durable.Hosting`
    are unchanged.
  - There is no provider, protocol, Kubernetes, AWS, or scheduler reference.
  - The public API baseline is unchanged. Package-source provenance changes only
    `OrcaCore.Dag.Hosting`, and the guard verifies the new digest.
- **Registration:**
  - The durable-engine role check matches the canonical scenario "absence of the durable-engine
    role fails startup".
  - The identical-options no-op and the conflicting-options rejection match the canonical
    registration idempotence rule.
  - `DagCoordinator` and `DagDefinitionRegistry` are `internal sealed`, with no public surface and
    no hosted loop.
- **Ledger:**
  - 8.1 is checked once, with accurate wording that grants 8.2–8.10 no acceptance credit.
  - The Task 8.0 ledger sentences the gate pins are intact.

## 3. Mutation probes (scope A)

Each mutation edited only `AddOrcaCoreDag`, was rebuilt, and ran the Section 7 scenario theory.
Eleven unrelated environmental failures appeared identically in the unmodified control: the probe
copy built only the guard project, so ProviderCertification drivers were missing. The full-solution
lane above has none.

| Probe | Mutation | Scenario result |
|---|---|---|
| P0 | unmodified (control) | both hosting scenarios green |
| M1 | an extra DAG-owned registration | **red**: "DAG registration must add exactly its internal coordinator, registry, and immutable profile" |
| M2 | durable-engine role check disabled | **red**: callback-only ingress accepted the DAG role |
| M3 | existing-registration lookup disabled | **red**: identical registration not idempotent |
| M4 | the conflict path adds a descriptor before throwing | **red**: conflicting registration mutated the collection |
| M5 | coordinator factory returns `null` | **red**: DAG service did not resolve |
| M1b | an extra foreign-typed registration (`new object()`) | green (P3-2) |
| R0 | restored | green; porcelain equals the target |

## 4. Non-blocking observations (P3)

- **P3-1: the activation carried reviewable edits.**
  - `f316d36` refreshed a Task 7.3-pinned source and its pins in an activation commit. The Task 7.3
    pin-refresh decision allows that only in a reviewed frozen target.
  - The bytes are correct, and scope B now reviews them.
  - Future activations should keep to the two-value transition. Any status-document refresh should
    go into the next reviewed target.
- **P3-2: the service-set check is scoped to DAG-owned types.**
  - The scenario pins the exact set of service types from `OrcaCore.Dag.Hosting`, but not the total
    number of descriptors added.
  - A foreign-typed registration would pass (M1b), although the ledger says "exact added-service
    set".
  - Also asserting that exactly three descriptors are added would close this. The current code adds
    nothing foreign.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `f316d36fbbac706e55ddbe2e5d2a4529b98cbc67`, with the seven scope-A entries and nothing
staged. When this verdict was written, the repository showed exactly those entries plus this new,
untracked verdict. The disposable worktrees are removed.

This approval covers only the seven files byte-for-byte as listed. To checkpoint:
- confirm the porcelain and content record still match section "What was reviewed";
- stage exactly those seven paths;
- confirm the tree is `a342e5c07055468bdd4909b7828aa905aded345d` with parent `f316d36`;
- commit that tree.

Adding any further file to the checkpoint needs a new review, and that includes a review request or
manifest. This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it in `appendOnlyRecords`.

The activation should then change only the review-state transition.

## Determination

Task 8.1 does exactly what its task and the approved Task 8.0 map assign it. The packages are
reconciled in place, and `AddOrcaCoreDag` is durable-only, validated, and idempotent. It adds only
the internal coordinator and registry, and it claims no DAG behavior that later tasks own. The
committed activation base is accurate and green.

**Verdict:** **APPROVE**
