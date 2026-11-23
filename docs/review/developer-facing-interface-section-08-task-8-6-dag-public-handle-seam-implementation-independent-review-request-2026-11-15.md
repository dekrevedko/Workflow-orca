# Public DAG facade: final combined implementation review request

Date: 2026-11-15. Worktree `X:/ocv302`, branch `draft/admit-dag-public-handle-seam`. Parent `da1a4729ea2cdb9474eb219eb507760574f83dcb`. Nothing committed or staged. Target manifest: `developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-implementation-dirty-manifest-2026-11-15.txt`.

## Authority and verdict scope

The owner directed continuing past Task 0.1 through complete implementation, permits greenfield API/contract changes, and requested independent self-review after each major step. `openspec/changes/admit-dag-public-handle-seam/artifacts/combined-delivery-owner-direction-and-substrate-2026-11-15.md` records the direction and exact substrate. RV-4 was approved for pre-gate design review; no external final implementation verdict, checkpoint or evidence commit exists. This request asks for final review of the combined contract/canonical/source/validation candidate. It does not ask the reviewer to approve fictional intermediate commits.

Task 0.1's process packet remains byte-exact. Five Proposed registry rows are retained, with a separate source-pinned OwnerAuthorizedReviewCandidate marker. The five new canonical blocks are synchronized; ten predecessor operations are explicitly SupersededByOwnerAuthorizedCandidate. Prior Complete records and external approval evidence remain unchanged. Final review/commit/activation tasks stay open. Do not credit this candidate to the overlapping run-progression Task 2.4 or activate reshape 8.3–8.5.

## Delivered behavior

- Public DAG registry, definition/run handles, snapshots, statuses and closed registration/start/output results; exactly two internal handle constructors using the existing friend.
- Four-part binding, local CLR-type distinction, different-key RunInputConflict, richer definition mismatch and not-found; submitted DAG run-input losslessness before create/replay, stored representation integrity and installed-type verification before reopen/mapping.
- Authoritative start-key replay, including a competing create, with a separate explicit run-id collision check. Equal keys with different validated bytes remain ordinary start conflicts.
- Locally unavailable hosts skip only mapping/admission and still process committed child join/cancellation work. Snapshot and cancellation derive pre-fault state from durable facts. Waits are level-triggered and use subscribe/recheck with an explicit race barrier.
- Fixed failure codes/messages/locations/root occurrence, detached authored-order snapshots, declared-type output, foreign-node rejection; no new public provider-port or lease API and no coordinator reflection.
- PostgreSQL checkpoint lineage correction found by real provider execution: load/insert/update preserve parent/root UUIDs. The initial greenfield schema gains those columns. Existing deployed database upgrades are not claimed.

## Review entry points

1. `src/OrcaCore.Dag/DagFacade.cs` and `DagFailureContracts.cs`: typed validation, closed outcomes and the two constructor signatures.
2. `src/OrcaCore.Dag.Hosting/DagFacadeRegistry.cs` and coordinator: public projection, recovery registration, unavailable-run behavior and the exact seventeen-member boundary.
3. `src/OrcaCore.Durable.Hosting/Dag/DurableDagFacade.cs`, run store/catalog/cancellation/mapping reader, and `src/OrcaCore.Engine.Durable/Dag/DurableDagValues.cs`: corruption versus compatibility, validation-before-effect, owner replay, observer recheck and durable value construction.
4. PostgreSQL store/schema diff: checkpoint lineage preservation and update-to-null semantics.
5. Hosting facade/binding tests, both provider certifications, `tests/OrcaCore.Dag.ProcessProbe`, and provider-port race barriers: inspect both effects and assertions, especially no-mutation/local-unavailability and real process separation.
6. `DagFacadeReviewCandidate`, OpenSpec owner/settlement guards, API/package records and current provenance: ensure exact candidate projections retain historical evidence and reject unowned drift.

The complete source record pins fifteen product files, eleven current documentation/canonical files and the sole changed public API baseline. All other public package APIs and friend topology remain unchanged. Five delta blocks are unchanged from RV-4; all fourteen canonical files reconstruct from parent plus precisely those blocks. Forty-two prior packet/artifact files remain byte-identical.

## Verification

See `openspec/changes/admit-dag-public-handle-seam/artifacts/implementation-validation-2026-11-15.md` and its case-level JSON evidence. Clean Debug/Release solution rebuilds and the unchanged RV-2 two-assembly proof: zero warnings/errors. Hosting 75/75; Durable engine 113/113; full PostgreSQL 121/121; full SQL Server 92/92. Both databases exercise binding, losslessness, level-triggered fault barriers and four separate restart processes. All nine package consumers compile from twelve current-source packages. Strict OpenSpec passes all 27 items structurally. Packed-package API and source-record checks pass 2/2.

Infrastructure: 318/318 plus the separately executed final active manifest/content check 1/1, totaling 319/319. The content check is rerun after this evidence-only update. Fourteen ExpectedRed obligations remain red and are reported without passing credit; they belong to the wider reshape scenario-driver ledger. The earlier eleven Section7Scenario 3.11d draft failures did not recur in this Infrastructure run.

Twelve candidate mutation cases reject ownership, requirement/source drift, missing rows and invented approval; before/after baselines pass. An actual unowned canonical-file edit is also rejected by the whole-file guard. `implementation-self-review-log-2026-11-15.md` records separate review passes after process, source, provider/restart and assembled-candidate steps, including the PostgreSQL defect and the catalog correction. These are self-review evidence, not an external APPROVE.

## Limits and handoff

The round-trip guarantee is representation-only and scoped to DAG run input. Mapped inputs, child outputs and ordinary workflow inputs retain existing behavior; changed meaning requires a definition version. Functional certification does not claim performance or production qualification. The signature proof uses a stand-in; actual behavior evidence comes from production bridge/scheduler/pumps/providers.

The manifest is self-inclusive in raw commit-real Git status order. The active guard content record excludes only its self-referential provenance fixture. Final semantic/active records and the projected tree are calculated with a copied index; the real index remains unstaged. The final chat handoff provides the resulting anchors so the request does not hash itself recursively. X:/ocv301, X:/ocv303 and the main checkout were not modified by this work.
