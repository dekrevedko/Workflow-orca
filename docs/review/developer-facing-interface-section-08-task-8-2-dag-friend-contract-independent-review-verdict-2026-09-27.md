# DAG authoring friend contract independent review verdict

**Date:** 2026-09-27
**Reviewer:** independent review
**Scope reviewed:** the eleven-entry freeze on base `5adddc3ba0ca7e0f70ee1f3b7317e69c1df7e76a`, named by
`developer-facing-interface-section-08-task-8-2-dag-friend-contract-dirty-manifest-2026-09-27.txt`.
This includes, as semantic inputs, the two committed `MODIFIED` successor blocks of
`admit-dag-authoring-friend-boundary`.
**Authorization requested:** approval of the `OrcaCore -> OrcaCore.Dag` authoring-friend contract
(task 1.2). This verdict does not grant it.

## Summary

The contract itself is sound:
- **The six members are sufficient.**
  - The five constructors exist exactly as described:
    - `Validation<T>(T?, IReadOnlyList<WorkflowDiagnostic>)`;
    - `WorkflowDiagnostic(string, WorkflowDiagnosticSeverity, AuthoredLocation, IReadOnlyList<AuthoredLocation>, string)`;
    - `AuthoredLocation(string)`;
    - `DefinitionFingerprint(string)`;
    - `WorkflowDefinitionException(IReadOnlyList<WorkflowDiagnostic>)`.
  - The seven `DAG-AUTH-*` codes are already approved in document 17.
  - `DurableWorkflowRef` publicly exposes the identity, version, and fingerprint that the DAG
    structural fingerprint needs.
- **No further internal access is implied.** The catalog is reached only inside the
  `WorkflowDiagnostic` constructor. Nothing in the text implies other internal access.
- **The negative questions all answer no.** The text authorizes no public factory
  ("exposes no new public construction path"), and no `Core -> Dag` reference, runtime friend, or
  extra internal member: the exhaustive edge and friend lists plus the metadata guard exclude
  them.
- **Sequencing:** tasks 1.3/1.4 are now explicitly atomic, which resolves round 79's P3-1.
- **Validation:** every lane reproduces.

One blocking defect remains:

- **YYY-1 (P2): the amendment does not enumerate the documents it contradicts.**
  - Numbered requirements in documents 03, 08, and 10 say `OrcaCore.Dag` depends (only) on
    *public* `OrcaCore` contracts.
  - Two BINDING implementation documents and one GUIDE restate an "exact" product-friend graph and
    "only public" DAG contracts. All three are Task 7.3-pinned.
  - The target amends document 17 and `CLAUDE.md` only. None of these other files is amended or
    explicitly excluded.

## Method

Validation ran in a disposable detached worktree that held the same eleven entries. Every script
asserted its disposable location and that the main `HEAD` was still `5adddc3b`. This target adds no
guard logic (only two pin constants), so mutation probes were not needed. Main was never modified,
and the review created no ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 11 lines, byte-identical | 891 B, `56629a6dacde94f960b73999ccc3b9747372a058656d808a15c83881f5f0699e` |
| Content record, all 11 rows (reviewer-computed) | — | 1,668 B, `47e3f9bec64268894bc3784d1890f58b9fb9e009bda9bd49d712d7e12c2142c1` |
| Simulated checkpoint | tree `049e547f…` | `049e547f23bf0aedd3d0a2719587abc3e37f423f` from a copy of the live index and from a committed disposable copy; path set equals the manifest (2 A / 9 M) |

- **Prior chain:**
  - checkpoint `d05dbe3` has tree `d4b0ac65`, the approved process gate, with parent `590df004`;
  - evidence `3895a36` is its only child and adds the round-79 verdict byte-exact (7,848 B,
    `fbfe7ee9…`);
  - activation `5adddc3` flips one checkbox.
- **This freeze's records:**
  - the request (4,422 B, `0307d43e…`) and manifest are cataloged, and `activeFreezeManifestPath`
    names this manifest;
  - the pins are refreshed in this same target: Task 8.0 map `4d4295e5…`, Task 7.3 `CLAUDE.md`
    row `bf378302…`, and digest `da63aa6e…`.
- **Scope:** no `src/**` or canonical `openspec/specs/**` change, and no compiled friend.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 226/226 |
| `ExecutableBehaviorExpectedRedGuards` | exactly 14 failures |
| Strict OpenSpec | 19/19 |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |

## 3. YYY-1 (P2): contradicted documents are neither amended nor excluded

`CLAUDE.md` requires two things of a post-gate amendment:
- check its declared targets against the crosswalk ("a file that is neither amended nor explicitly
  excluded is a gap, not a decision");
- update the affected GUIDE documents in the same change.

The source map requires BINDING documents to match the approved contract. The amendment's
reconciliation list (proposal "Impact", task 1.1) names only:
- `CLAUDE.md` and document 17;
- the Task 8.0 map;
- the canonical deltas, the registry, and the guards.

Task 1.3's "reconcile any affected numbered requirements" names no file.

**Numbered requirements.** The crosswalk maps `developer-facing-surface` to 17 and 10, and
`repository-foundation` to 10 and 11.

| Document | Text | Conflict |
|---|---|---|
| 03 §glossary, line 326 | "`OrcaCore.Dag` depends only on public `OrcaCore` workflow contracts" | Directly contradicted by the friend. |
| 08 CP-020 | "`OrcaCore.Dag` SHALL be a separate project/package that depends on public OrcaCore workflow contracts" | Needs a stated disposition. |
| 10 PR-005 | "`OrcaCore.Dag` SHALL be a separate package that depends on public OrcaCore workflow contracts" | Crosswalk-mapped to both amended capabilities. Needs amending or explicit exclusion. |
| 11 | package-closure statements only | No conflict found. As a crosswalk target, record it as explicitly excluded. |

**BINDING and GUIDE documents.** All three are Task 7.3-pinned.

| Document | Text | Conflict |
|---|---|---|
| `docs/implementation/00-stack-decisions.md` line 49 | Decision 22, "one exact closed friend graph": "Product friends are Core→both engines, Durable Engine→Durable Hosting, and Durable Hosting→DAG Hosting" | Adding a friend amends Decision 22, and the amendment never names it. |
| `docs/implementation/01-solution-architecture.md` line 80 | "`OrcaCore.Dag` depends only on public `OrcaCore` application contracts" | Contradicted. |
| `docs/implementation/01-solution-architecture.md` lines 77 and 131 | "every friend assembly outside the exact closed graph below"; "Product friends are exact: `OrcaCore.Core` grants both engines, …" | Contradicted. |
| `docs/project-technical-overview.md` line 59 | "The exact internal friend graph …: Core grants both engines, Durable Engine grants Durable Hosting, and Durable Hosting grants DAG Hosting" | Contradicted. |

The three friend-graph statements are also already stale. They omit the three canonical
`OrcaCore -> OrcaCore.Core/Engine.Ephemeral/Engine.Durable` friends, which this target corrected in
`CLAUDE.md` only. The documents' "only friend bridge to the child start/join seam" and "no second
DAG-to-durable product bridge" statements remain true, and need only the authoring/runtime
distinction.

**Consequence:** once the contract is synchronized, three active binding and guide sources and at
least one numbered requirement would state a narrower graph than canonical. No planned task names
them.

## 4. What a superseding freeze needs

1. Amend, or explicitly exclude with a stated reason, documents 03 (line 326), 08 CP-020, 10 PR-005,
   and 11.
   - Either do it in this target, as for document 17, or name each file and its intended wording as
     an exact target of the atomic 1.3/1.4 sync.
2. Reconcile `00-stack-decisions.md` Decision 22, `01-solution-architecture.md` (lines 77, 80, and
   131), and `project-technical-overview.md` line 59.
   - Include the current seven friends and the proposed authoring-only eighth.
   - Refresh their Task 7.3 rows 6, 7, and 12 and the artifact digest in the same target.
3. Record this `REJECT` under the reshape convention.

The two `MODIFIED` blocks, design, and proposal need no semantic change. The same applies to the
`CLAUDE.md`, document 17, and Task 8.0 map edits and the atomic 1.3/1.4 plan. They can be refrozen
unchanged; only the enumeration additions above need review.

## 5. Reviewer hygiene

`HEAD` is `5adddc3ba0ca7e0f70ee1f3b7317e69c1df7e76a`, with the eleven frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

## Determination

The friend contract is narrow, sufficient, and correctly sequenced. It cannot yet be approved,
because it leaves numbered, binding, and guide documents that state the old public-only DAG
dependency and the old exact friend graph without an amendment or an exclusion.

**Verdict:** **REJECT**
