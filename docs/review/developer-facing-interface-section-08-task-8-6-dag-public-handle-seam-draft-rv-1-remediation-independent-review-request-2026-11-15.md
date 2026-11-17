# Independent review: DAG public handle seam, draft contract RV-1 remediation (pre-gate)

Date: 2026-11-15. Parent: da1a4729ea2cdb9474eb219eb507760574f83dcb. Parent tree: d59fbdb4cd1183de9c85a0431b62f5c518442791.
Branch: draft/admit-dag-public-handle-seam. Review the [self-inclusive raw-order manifest](developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-draft-rv-1-remediation-dirty-manifest-2026-11-15.txt) as one target.

This remediates the first pre-gate draft, rejected at projected tree `6fb21010c787cf9962b2970a4c70997d57ccace0`. That packet's [request](developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-draft-independent-review-request-2026-11-15.md) and [manifest](developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-draft-dirty-manifest-2026-11-15.txt) are unchanged (manifest still 10 rows / 894 B / `22cf8890c694fb73e7db255dc5ad9f9cfde56e96dd5a951467717919512a631d`). Its five mutable files are preserved byte-exact under `artifacts/rejected-pre-gate-draft-2026-11-15/`, and its two proof files keep their original names. The reject verdict was delivered as text; no verdict file exists in this target, and the reviewer may supply one for the record.

This is still a **design review of a draft contract before its process gate**. It is not Task 0.2 or 1.2 approval, not canonical synchronization and not source authority for reshape Task 8.6. No product source, canonical spec, numbered document, guard, fixture or registry row changes. An APPROVE means only "carry this contract through Tasks 0.1–1.4 unchanged".

## What changed since the rejected draft

| Finding | Remediation | Where |
|---|---|---|
| P1 run-input contract missing from registration and reopen | The definition binding is now four parts: identity, version, structural fingerprint and declared run-input contract. Registration, start replay, reopen and recovery compare all four, with a typed outcome when only the contract differs. | design §3; friends delta (binding paragraph, three scenarios); both constructor signatures; RV-1 proof |
| P1 D1 not defined against durable facts; wait requirements unreconciled | The pre-fault run status is derived from the checkpoint alone, with the argument for exactness. Wait, snapshot and cancellation outcomes are tabulated. Both canonical wait requirements are now `MODIFIED`. `DAG-RUN-FAULTED` is in the doc 17 catalog scope. | design §5, §9; `management-and-querying` delta; completion-waits delta |
| P2 node failure location and occurrence | `dag:$/dag-node:dddddddd`, `FailureOccurrence.Root`, fixed message per code, empty causes, null exception type. | design §6 |
| P2 gate tasks not executable | 0.1 names the guard edit and its exit test; 1.1 re-pins revised blocks; 1.3/1.4 are one target; 2.0 records the substrate checkpoint and resolves overlap with run-change Task 2.4; closeout review (3.1) is split from activation (3.2). | tasks.md |
| Note: enums have no constructors | Status values are stated as public enumeration members. | friends delta |
| Note: make the projection split explicit | Hosting projects snapshots and statuses from run facts; the durable engine creates the four opaque values. | design §2; tasks 2.2, 2.3 |
| Note: start replay scope | Key owner is resolved on retry and after a competing create; the separate run-id collision check is kept. | design §3; friends delta |

## What to review

1. **Typed run-input binding (new decision D4).** Is the outcome table in design §3 complete and correctly ordered? In particular:
   - `Register` with an equal triple and another `TRunInput` returns a new closed variant `RunInputConflict(existing type, attempted type)`, and `GetHandleOrThrow()` throws `DagRunInputContractMismatchException`. Is a third union variant acceptable, or should this be expressed another way?
   - `StartOrGetAsync` against a key owned by the same triple under another contract **throws** that exception instead of returning `Conflict`, because `StartIdempotencyConflict` would carry equal definition triples and a byte-only input fingerprint. Is a throw from the start path acceptable beside a closed result union?
   - `GetRunAsync` checks definition mismatch first, then run-input contract.
   - Recovery treats a foreign-contract run as locally unavailable and never faults it.
   - The contract key stays durable-owned; folding `TRunInput` into the structural fingerprint was rejected. Is that the right call?
2. **Signal encoding.** The two constructors now carry three result signals (design §1): a nullable start result, a `RunInputContractDiffers` flag on the reopen tuple, and nullable wait and cancel results. Is a null result an acceptable typed signal across the friend edge, and is the member count still exactly seventeen with three non-public types?
3. **D1 status derivation.** Verify the exactness argument in design §5 against the committed transitions in `DurableDagRunCancellation`, `DurableDagChildCoordinator.Join`/`Outcome` and `DurableDagAdmission`: no node is `CancellationRequested` or `Cancelled` without a committed run-cancellation request, and every nonterminal run with such a request keeps at least one such node.
4. **D1 outcomes.** Wait throws when the fault is already committed and when it commits after registration; a later wait after reconciliation is ordinary. Cancellation on a faulted run returns `AlreadyRequested` when a request was already committed and otherwise throws without committing. Is returning `AlreadyRequested` from a faulted run correct?
5. **The two added wait deltas.** Verify both headings verbatim, that each changes exactly one canonical clause (design §10), and that the edited text and added scenarios agree with each other and with RC-3.
6. **Node failure values** (design §6): location grammar, `Root` occurrence, and which node states carry a failure.
7. **Task graph.** Is Task 0.1 now executable as written, including the three different predecessor chains across four headings? Is Task 2.0 a sufficient statement of the substrate and Task 2.4 overlap condition?
8. **Delta byte-identity.** Reversing the six listed clause edits and removing the insertions should restore all four parent blocks byte-for-byte.

## Evidence supplied

- **RV-1 proof.** `artifacts/task-1-1-public-handle-seam-rv-1-proof-dag-2026-11-15.cs` and `…-rv-1-proof-hosting-2026-11-15.cs` compiled as two assemblies (`Proof.Dag` with `InternalsVisibleTo("Proof.Dag.Hosting")`, and `Proof.Dag.Hosting`) on net10.0, nullable enabled, warnings as errors, referencing the real `OrcaCore.Dag` project at the parent: 0 warnings, 0 errors. The hosting assembly implements register (all three variants), start (including the null signal), reopen (all four outcomes), snapshot, output, terminal wait and cancellation, and constructs no exception. The bridge is a stand-in and three existing DAG exceptions are stand-ins, so the build verifies signatures only. The scratch projects are not in the target.
- **Structure.** `openspec validate admit-dag-public-handle-seam --strict` reports the change valid. Structural validation only.
- **Delta provenance.** All four deltas are generated by script from the canonical files with a uniqueness assertion on each replaced clause. A line diff of each delta against its canonical block shows exactly one changed canonical line plus insertions.
- **Code facts.** Each row of the design "Context" table names the file and member it was read from at the parent commit.

## Known red and not done

- The two owner-chain guards (`CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners`, `Task53_ReshapeSolelyOwnsFriendTopologyAndDurableGovernanceAggregate`) remain red by design until Task 0.1. They were not re-run for this target; the prior review measured 37/39 on the focused selection with only those two failing, and this target adds two more owned headings, which the same check will also reject.
- No build or test lane was run for this target beyond the proof compile and strict validation. It changes no compiled input.
- Not prepared: registry rows, the guard edit, the provenance refreeze, `review-manifest-provenance.json` and `immutable-document-history.json` entries, and every document reconciliation in design §9.
- No provenance row count, byte count or unresolved-operation table is reported, because the fixture was not refrozen.
- The claim that an integrity-fault commit wakes registered waiters rests on reading the provider's observer-notice rule; it is a Task 2.5 test obligation, not demonstrated here.

## Freeze and disposition

- Manifest: UTF-8 without BOM, LF, final LF; byte-equivalent to raw `git status --porcelain=v1 --untracked-files=all --no-renames` and its NUL form converted to LF. It includes itself, this request, and the unchanged rejected request and manifest.
- Semantic record: exclude `docs/review/**`. Each row is path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF: 16 rows / 2,607 B / 10abadd4b1738c84f0ebf99a54381b2242fe1b326eada3b3a4656655f3180046.
- Active record: XY<TAB>path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF, with no exclusion. Its anchor and the projected tree are in the handoff to avoid self-reference.
- Independent review only. Leave the target uncommitted. A verdict here activates no checkbox. If approved, the next target is Task 0.1, reviewed on its own. If rejected, remediate in a new target and keep both packets byte-exact.
