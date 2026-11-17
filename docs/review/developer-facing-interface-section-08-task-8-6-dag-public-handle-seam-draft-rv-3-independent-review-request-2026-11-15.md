# Independent review: DAG public handle seam, draft contract RV-3 after prototype (pre-gate)

Date: 2026-11-15. Parent: da1a4729ea2cdb9474eb219eb507760574f83dcb. Parent tree: d59fbdb4cd1183de9c85a0431b62f5c518442791.
Branch: draft/admit-dag-public-handle-seam. Review the [self-inclusive raw-order manifest](developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-draft-rv-3-dirty-manifest-2026-11-15.txt) as one target.

RV-2 (projected tree `5f1a184d4e6c1667729cf67134e199e4a9b038c5`) was returned with one blocking inconsistency and a recommendation to prototype before freezing. This target folds the prototype findings into the contract as scoped by that review. All three earlier packets are unchanged: manifests `22cf8890…` (first draft), `c3cc6523…` (RV-1) and `71f8809eeecc411baa4076a7a9b8ef6179a1a5488d64997e5789b0298fcfedd9` (RV-2, 30 rows / 3,262 B). RV-2's six mutable files are preserved byte-exact under `artifacts/superseded-rv-2-draft-2026-11-15/`. No verdict file exists in this target; the verdicts were delivered as text.

Still a **design review of a draft contract before its process gate**: not Task 0.2 or 1.2 approval, not canonical synchronization, not source authority. No product source, canonical spec, numbered document, guard, fixture or registry row changes in this target. The prototype is in a separate worktree and is not part of the target.

## What changed since RV-2

| Input from the RV-2 review and the prototype | Change | Where |
|---|---|---|
| RV-2 could accept a new run and an exact-byte replay, then refuse to reopen it | The round-trip check now also runs at submission. Before creating a run or accepting a replay the durable runtime encodes the submitted input with the declared type, decodes those bytes and requires byte-identical re-encoding; a failing value is rejected with `NotSupportedException` before any run is committed or key bound. The RV-2 argument that equal start bytes prove compatibility is removed. | friends delta; design §3 |
| "Every committed DAG value" was too wide | The rule covers the DAG run input only. Mapped node inputs, child outputs and ordinary workflow inputs keep their commit paths and failure outcomes. | friends delta; design §3; task 2.3 |
| Reopen and recovery | The stored input is verified before a handle is issued and before any mapper is evaluated. A well-formed payload that fails under the installed binding makes the run locally unavailable. Corrupt storage (invalid framing, codec format, recorded contract key or fingerprint) keeps its existing integrity classification. | friends delta and two new scenarios; design §3 |
| What an incompatible host still does | It skips only new mapping and admission. Child start, join and cancellation work already committed is role-independent and is still processed by any durable host. | friends delta; design §3 |
| Representation, not meaning | The byte check is stated as a guarantee about the stored representation; a change in meaning still requires a new definition version. | friends delta; design §3 |
| Fault race arranged by timing in the prototype | The level-triggered rule and cancellation outcomes are unchanged. The final regression test must use an explicit barrier. | task 2.5; design §5 |
| Evidence class | Provider certification and a real process restart are listed as open source-task exits. | task 2.5; evidence record |

Unchanged since RV-2: both constructor signatures (so the RV-2 two-assembly proof remains the current proof), the closed `RunInputConflict` variants with the owner's run id, the closed-results and wait deltas, the pre-fault status derivation, the node failure projection, and the gate tasks. Only the friends delta changed among the five.

## What to review

1. **Submission rule.** Is the statement in the friends delta exact: encode, decode, byte-identical re-encode, `NotSupportedException`, before any run is committed and before any start key is bound, for first start and replay alike?
2. **Consistency with the closed start results.** The delta now says no comparison with an existing key owner is reported by exception and only the round-trip rejection is. Does that agree with the closed-results delta's "returned, never thrown" wording for `RunInputConflict`?
3. **Stored-input verification and classification.** Is the split between a well-formed payload that fails verification (locally unavailable) and corrupt storage (existing integrity classification) stated precisely enough to implement, in particular "recorded contract key" on the stored value versus the graph's run-input contract key compared with the installed plan?
4. **Locally unavailable.** Is "skip new mapping and admission; committed child start, join and cancellation work is still processed by any durable host" complete? Should cancellation requested through another host's handle be said to proceed on an incompatible host's pump as well?
5. **Scope sentence.** Is excluding mapped inputs, child outputs and ordinary workflow inputs stated without implying they are known to be safe?
6. **Delta byte-identity.** Only the friends delta changed since RV-2. Reversing the eight listed clause edits and removing insertions should still restore all five parent blocks byte-for-byte.
7. **Evidence record.** Does `artifacts/task-1-1-public-handle-seam-prototype-evidence-2026-11-15.md` describe the prototype accurately, including its four listed deviations from this contract?

## Evidence supplied

- **Prototype** (separate worktree `X:/ocv303`, branch `prototype/dag-public-facade`, uncommitted; file hashes in the evidence record). Five facade tests passed against the real bridge, scheduler, pumps and InMemory provider, and were independently rerun by the reviewer. They establish: create, replay with the owner's run id, changed-input conflict, reopen, typed output and not-found through the public facade; rejection of a lossy run input before any key is bound; an incompatible host that cannot reopen, does not map, still joins an admitted child, and leaves the run for a compatible host that finishes it; a held waiter that does not throw across fault and reconciliation; and the faulted wait, snapshot and cancellation outcomes in both orders.
- **Control.** With the recovery skip disabled, the incompatible host maps the misread value and the run ends `Succeeded`.
- **Regression in the prototype.** With the stricter normalization applied to every DAG value, the existing hosting suite (58 including the five) and the durable engine test project passed. That is wider than the RV-3 rule and is reported only as "nothing existing depends on lossy values".
- **Proof.** Unchanged RV-2 two-assembly proof, 0 warnings and 0 errors when last built; not rebuilt for this target because no signature changed.
- **Structure.** `openspec validate admit-dag-public-handle-seam --strict` reports the change valid. Structural validation only. Each delta still shows exactly one changed canonical line against its canonical block.

## Known red and not done

- Prototype evidence is in-process and in-memory. PostgreSQL, SQL Server and a real process restart are untested.
- Not exercised by the prototype: registration `Conflict` and `RunInputConflict`, start `RunInputConflict`, a competing create between two hosts, a lossy mapped node input, corrupt stored run input, and the boundary guard with a seventeen-member allowlist.
- The prototype deviates from this contract in four listed ways, including reclassifying corrupt storage as a mismatch and using timing for the fault race.
- The two owner-chain guards remain red by design until Task 0.1 and were not re-run.
- No build or test lane was run in this target's worktree for RV-3.
- Not prepared: registry rows, guard edits, provenance refreeze, review-catalog fixture entries, and the document reconciliation in design §9.
- The owner has not separately confirmed D4 or the `NotSupportedException` outcome; both follow the reviewer's recommendation.

## Freeze and disposition

- Manifest: UTF-8 without BOM, LF, final LF; byte-equivalent to raw `git status --porcelain=v1 --untracked-files=all --no-renames` and its NUL form converted to LF. It includes itself, this request, and the six unchanged files of the three earlier packets.
- Semantic record: exclude `docs/review/**`. Each row is path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF: 31 rows / 5,325 B / 1b620b6571dca6ea5fcdacd0b74afb12451368cc87739631edf81e203394c396.
- Active record: XY<TAB>path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF, with no exclusion. Its anchor and the projected tree are in the handoff to avoid self-reference.
- Independent review only. Leave the target uncommitted. A verdict here activates no checkbox. If approved, the next target is Task 0.1, reviewed on its own. If rejected, remediate in a new target and keep all four packets byte-exact.
