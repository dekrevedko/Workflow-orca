# Independent review: DAG public handle seam, draft contract RV-2 remediation (pre-gate)

Date: 2026-11-15. Parent: da1a4729ea2cdb9474eb219eb507760574f83dcb. Parent tree: d59fbdb4cd1183de9c85a0431b62f5c518442791.
Branch: draft/admit-dag-public-handle-seam. Review the [self-inclusive raw-order manifest](developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-draft-rv-2-remediation-dirty-manifest-2026-11-15.txt) as one target.

This remediates RV-1, rejected at projected tree `bc970f9a9358768c294c11dcebfa7f441ecb5171` with the two-constructor seam upheld. Both earlier packets are unchanged: the first draft's manifest is still 10 rows / 894 B / `22cf8890c694fb73e7db255dc5ad9f9cfde56e96dd5a951467717919512a631d`, and the [RV-1 manifest](developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-draft-rv-1-remediation-dirty-manifest-2026-11-15.txt) is still 20 rows / 2,070 B / `c3cc6523dabc83c42e88dc59198d73d6f57a6df0fb4f85e3527453618bc0502e`. RV-1's six mutable files are preserved byte-exact under `artifacts/rejected-rv-1-draft-2026-11-15/`, and its proof files keep their names. Both reject verdicts were delivered as text; no verdict file exists in this target.

This is still a **design review of a draft contract before its process gate**: not Task 0.2 or 1.2 approval, not canonical synchronization, not source authority. No product source, canonical spec, numbered document, guard, fixture or registry row changes. An APPROVE means only "carry this contract through Tasks 0.1–1.4 unchanged".

## What changed since RV-1

| RV-1 finding | Remediation | Where |
|---|---|---|
| P1 start outcome contradicted the canonical closed-result requirement | `StartOrGetAsync` returns a closed `DagStartResult.RunInputConflict(ExistingRunId, DefinitionId, DefinitionVersion, DefinitionFingerprint, AttemptedRunInputType)`; `GetHandleOrThrow()` throws `DagRunInputContractMismatchException`. The start delegate result is no longer nullable. "Closed registration and start results retain a cast-free success path" is added as a fifth `MODIFIED` heading defining both three-variant unions. | closed-results delta; friends delta; design §1, §3; RV-2 proof |
| P1 equal shape digests can admit incompatible input contracts | An equal key is necessary, not sufficient. Against a stored run the installed plan matches only when the keys are equal **and** the durable runtime has verified that the stored run-input bytes decode with the installed declared type and re-encode to exactly the stored bytes, before reopen and before any mapping. A failed verification is treated as a different key and is never a mapping failure, node failure or integrity fault. Within one host, sameness requires the same CLR `Type` and an equal key. | friends delta (binding paragraph, new scenario); design §3; tasks 2.3, 2.5 |
| P1 a fault notice can arrive after reconciliation | `DagRunFaultedException` is level-triggered: it means an authoritative recheck observed a faulted checkpoint. A fault reconciled before the waiter rechecked is not reported, the wait continues, and committed transitions are not inspected. | both wait deltas; design §5; held-waiter scenario; task 2.5 |
| Note: settlement validator requires exactly two owners and a canonical block | Task 0.1 makes it stage-aware for the Proposed stage; Task 1.4 moves it to its ApprovedPending form in the atomic target. | tasks.md |

Upheld by the RV-1 review and unchanged here: the two constructors, three non-public types and seventeen members; the pre-fault status derivation; `AlreadyRequested` for a faulted run with a committed cancellation barrier; the node failure projection; Task 2.0; the three predecessor chains.

## What to review

1. **Closed-results delta.** Verify the heading verbatim and that exactly the two clauses in design §10 change, both in one canonical paragraph. Are the two three-variant unions stated completely, and is "returned, never thrown, by `Register` and `StartOrGetAsync`" consistent with the friends delta and the proof?
2. **Start variant shape.** `RunInputConflict` carries the owner's run id, the definition triple and the attempted type, but no existing type, because the owner's declaring type is not knowable across hosts. Is that the right content?
3. **Stored-bytes verification.** Does "decode with the installed type and re-encode to exactly the stored bytes" exclude the reported case (the older DTO's `{}` rejected by the newer DTO) and the reverse deployment order? Is byte-identical re-encoding too strict for any legitimate compatible redeployment under the fixed codec, or too weak for a case where decode succeeds, the bytes round-trip, and the meaning still differs? Is "before reopen and before any mapping" the right boundary, and is a per-run cached verdict sound given the run input is immutable?
4. **No verification on start.** Design §3 argues that equal attempted and stored bytes under an equal key already prove the stored bytes round-trip with this host's type, and that an equal key with different bytes is an ordinary `StartIdempotencyConflict`. Check that argument.
5. **Local versus durable comparison.** One host: same CLR `Type` and equal key, so a different `Type` with an equal key is a `RunInputConflict`. Stored run: key plus verification. Is anything left between the two rules?
6. **Level-triggered fault exception.** Is "an authoritative recheck observed a faulted checkpoint" the right meaning, given that the alternative requires inspecting transitions after the waiter's last observed version? Does the added scenario (waiter held until fault and reconciliation have both committed) pin it, and do both wait deltas now agree?
7. **Task 0.1 and 1.4.** Are the stage-aware requirements for the owner-chain check and for the settlement validator executable as written, including its other eight headings staying untouched?
8. **Delta byte-identity.** Reversing the eight listed clause edits and removing the insertions should restore all five parent blocks byte-for-byte.

## Evidence supplied

- **RV-2 proof.** `artifacts/task-1-1-public-handle-seam-rv-2-proof-dag-2026-11-15.cs` and `…-rv-2-proof-hosting-2026-11-15.cs` compiled as two assemblies on net10.0, nullable enabled, warnings as errors, referencing the real `OrcaCore.Dag` project at the parent: 0 warnings, 0 errors. The hosting assembly returns all three start variants and all three registration variants, reports all four reopen outcomes, and constructs no exception. The bridge and three existing DAG exceptions are stand-ins; the build verifies signatures only.
- **Structure.** `openspec validate admit-dag-public-handle-seam --strict` reports the change valid. Structural validation only.
- **Delta provenance.** All five deltas are script-generated from the canonical files with a uniqueness assertion per replaced clause; a line diff of each against its canonical block shows exactly one changed canonical line plus insertions.

## Known red and not done

- The stored-bytes verification is specified, not exercised. The reviewer's two-DTO experiment was not reproduced here; it is cited as the motivating fact and becomes a Task 2.5 test.
- The claim that a fixed-codec re-encode of decoded bytes is deterministic and canonical is assumed from the existing normalization path, not demonstrated for this target.
- The two owner-chain guards remain red by design until Task 0.1 and were not re-run; this target adds a fifth owned heading, which the same check will also reject.
- No build or test lane was run beyond the proof compile and strict validation.
- Not prepared: registry rows, guard edits, provenance refreeze, review-catalog fixture entries, and the document reconciliation in design §9.
- D4 (the typed run-input binding) has been shaped by two reviews but not yet explicitly accepted by the owner.

## Freeze and disposition

- Manifest: UTF-8 without BOM, LF, final LF; byte-equivalent to raw `git status --porcelain=v1 --untracked-files=all --no-renames` and its NUL form converted to LF. It includes itself, this request, and the four unchanged files of the two rejected packets.
- Semantic record: exclude `docs/review/**`. Each row is path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF: 24 rows / 4,052 B / 63d1029cf5cd118d0f9af29612d0dcd30179f9fa5c0e9a8cc1d1146304987397.
- Active record: XY<TAB>path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF, with no exclusion. Its anchor and the projected tree are in the handoff to avoid self-reference.
- Independent review only. Leave the target uncommitted. A verdict here activates no checkbox. If approved, the next target is Task 0.1, reviewed on its own. If rejected, remediate in a new target and keep all three packets byte-exact.
