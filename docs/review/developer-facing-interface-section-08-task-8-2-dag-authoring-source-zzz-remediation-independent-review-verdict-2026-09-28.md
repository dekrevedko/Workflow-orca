# Task 8.2 DAG authoring source ZZZ remediation independent review verdict

**Date:** 2026-09-28
**Reviewer:** independent review
**Scope reviewed:** the thirty-nine-entry freeze on base
`db5f3fb2b0733c06ae052536748f5a445bf52de7`, named by
`developer-facing-interface-section-08-task-8-2-dag-authoring-source-zzz-remediation-dirty-manifest-2026-09-28.txt`.
This is the complete target: `admit-dag-authoring-friend-boundary` tasks 2.1–2.5 and reshape
Task 8.2, not only the repaired files.
**Authorization requested:** approval and checkpoint of the Task 8.2 DAG authoring source. This
verdict authorizes that checkpoint for the exact frozen target only. It does not authorize Task
8.3, DAG hosting, child start, or the registry's final `Complete` transition.

## Summary

All three blocking findings from the first source review are fixed, and each fix survives
adversarial probing:

- **ZZZ-1: type references.**
  - The metadata guard now rejects every `TypeReference` in `OrcaCore.Dag.dll` that resolves to a
    non-public `OrcaCore` type, and it keeps the six exact internal member signatures.
  - Four permanent probes compile real friend assemblies and require the type check itself to fail;
    removing that check turns the guard red.
  - My probes are all red:
    - `typeof`, a field, an implicit interface implementation, and an `is` test;
    - a generic argument;
    - `typeof` inside a custom-attribute argument, including
      `[JsonConverter(typeof(internal StrongStringValueJsonConverterFactory))]`.
- **ZZZ-2: cycle detection.**
  - It now uses an explicit frame stack.
  - 100,000-node chains pass in both directions, and my 20,000- and 200,000-node forward chains
    validate. The 20,000-node chain overflowed the stack before.
  - Seven isolated theory cases each assert one exact code, and a self-dependency now yields only
    `DAG-AUTH-DEPENDENCY-002`.
- **ZZZ-3: document status.** The four normative passages (documents 03, 08 CP-020, 10 PR-005, and
  17 §17.2.6) now say the friend is compiled in the candidate and awaits independent source
  approval. No active document still claims it is not compiled.

The previously correct parts are unchanged:
- the friend attribute, the byte-identical hash extraction, and the seven catalog codes;
- the §17.2.6 public API, the sole `Dag -> OrcaCore` edge, and the absence of any runtime or
  child-start friend.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
thirty-nine entries. Every script asserted its disposable location and that the main `HEAD` was
still `db5f3fb2`. Main was never modified, and the review created no ref in the reviewed
repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 39 lines, 2,968 B, `c9462505…020a` | identical |
| Semantic record: 32 rows, `path<TAB>bytes<TAB>sha256`, excluding `docs/review/**` and both review fixtures | 4,334 B, `ff924cd5…b110` | identical bytes. The digest reproduces only in raw porcelain order; ordinal path sort gives `85522a3a…` (P3-1) |
| Active-freeze record in `review-manifest-provenance.json` (all except that fixture) | pinned | 38 rows, 5,566 B, `6b30a802…`, equal to the fixture |
| All-file content record, 39 rows (reviewer) | — | 5,721 B, `0781d4ff96e4978aef60530172db51e5a08d81d2ef9ec9ebb6e830d9478a1118` |
| Simulated checkpoint | tree `cf11b3f6732f11250ba3a0255114bcdc4ae00060` | identical from a copy of the live index and from a committed disposable copy; parent `db5f3fb2`; 9 A / 30 M; all blobs equal the raw bytes |

- **Rejected packet:** it is byte-exact and cataloged in `appendOnlyRecords`, together with this
  manifest and request (6,000 B, `c2e58b56…`):
  - manifest 2,431 B, `aac89bfc…`;
  - request 6,524 B, `551d9bec…`;
  - REJECT verdict 12,291 B, `49596470…`.
- **Pins:**
  - all 22 Task 7.3 rows (including refreshed rows 14 and 18) and the artifact digest reproduce;
  - so do the Task 8.0 map pin and the validation-artifact pin (`c0cabc50…`);
  - the maximal current-match refresh reproduces the fixture byte-for-byte;
  - the crosswalk moves only by two `[Theory]` declarations (191 active files, 714 active
    declarations).

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 240/240 |
| `ExecutableBehaviorExpectedRedGuards` | exactly 14 failures |
| Package fixtures, `Green` / `ExpectedRed` | 8 green / exactly `dag-hosting` red |
| Strict OpenSpec | 19/19 |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 240 passed plus the same 14 expected-red |

## 3. Probes

Each probe added one source file to `OrcaCore.Dag` in the probe copy, rebuilt, and ran
`DagInternalMemberReferenceGuards`.

| Probe | Reference | Result |
|---|---|---|
| G0 | unmodified (includes the four permanent compiled probes) | green |
| G1 | `typeof(OrcaCore.WorkflowDiagnosticCatalog)` | **red**: forbidden type |
| G2 | static field of `WorkflowDiagnosticDescriptor` | **red** |
| G3 | implicit `IWorkflowDefinitionRuntimeMetadata` implementation | **red** |
| G4 | `is IWorkflowDefinitionRuntimeMetadata` | **red** |
| G8 | internal type only as a generic argument | **red** |
| G10 | `typeof(internal)` only in a custom-attribute argument | **red** |
| G10b | `[JsonConverter(typeof(OrcaCore.Abstractions.Ids.StrongStringValueJsonConverterFactory))]` on a DAG type | **red** |
| G5 | call to internal `WorkflowDiagnosticCatalog.Require` | **red** |
| G7 | public `DefinitionFingerprint.Value` (control) | green |
| M1 | type-reference assertion removed from the guard source | **red**: the permanent probes "expected an exception" |
| B1 | self-dependency | exactly `DAG-AUTH-DEPENDENCY-002` |
| B2 | forward-wired chains of 20,000 and 200,000 nodes | both valid, no crash |
| R0 | restored | green; porcelain equals the manifest |

## 4. Non-blocking observations (P3)

- **P3-1: the stated sort doesn't match the recorded digest.** The request says the semantic
  record sorts paths ordinally, but `ff924cd5…` reproduces only in raw porcelain order. Correct the
  description, or recompute the value with an ordinal sort, in future requests.
- **P3-2: the new status wording will go stale after checkpoint.** "Compiled in the Task 8.2 source
  candidate and awaits independent source approval" becomes stale once this checkpoint lands. It
  appears in documents 03, 08, 10, and 17, `CLAUDE.md`, Decision 22, the solution architecture,
  and the overview. Refresh it in the next reviewed target, not in the activation commit.
- **P3-3: the permanent probes depend on the default SDK.** They run `dotnet build` in a temporary
  directory outside the repository, so the `global.json` SDK pin does not apply. CI needs a default
  SDK that can target `net10.0`.
- **Recorded decisions (no action here):** the DAG fingerprint's exclusion of `TRunInput` is handed
  to Task 8.6 conflict keying, and null `OutputOf` results to Task 8.3. Both are recorded in the
  validation artifact.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `db5f3fb2b0733c06ae052536748f5a445bf52de7`, with the thirty-nine frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 39 manifest paths from the live index;
- confirm the tree is `cf11b3f6732f11250ba3a0255114bcdc4ae00060` with parent `db5f3fb2`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it in `appendOnlyRecords`.

The activation should then change only the review state. Task 8.3 and the post-gate registry's
final `Complete` promotion remain separate reviewed targets.

## Determination

The compiled friend is now narrowed in fact, not just in text: both member and type references to
non-public `OrcaCore` code are rejected, and the guard proves its own negative controls. DAG
authoring validates arbitrarily deep graphs without recursion, reports one exact diagnostic per
fault, and the normative corpus describes the compiled state honestly.

**Verdict:** **APPROVE**
