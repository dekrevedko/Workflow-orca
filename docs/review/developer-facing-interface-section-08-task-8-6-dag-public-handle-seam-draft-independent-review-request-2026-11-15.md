# Independent review: DAG public handle seam, draft contract (pre-gate)

Date: 2026-11-15. Parent: da1a4729ea2cdb9474eb219eb507760574f83dcb. Parent tree: d59fbdb4cd1183de9c85a0431b62f5c518442791.
Branch: draft/admit-dag-public-handle-seam. Review the [self-inclusive raw-order manifest](developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-draft-dirty-manifest-2026-11-15.txt) as one target.
Scope is the new change directory `openspec/changes/admit-dag-public-handle-seam/` only: proposal, design, tasks, two requirement deltas and a two-file standalone proof.

This is a **design review of a draft contract before its process gate**. It is not the Task 0.2 process approval, not the Task 1.2 contract approval, not canonical synchronization and not source authority for reshape Task 8.6. No product source, canonical spec, numbered document, guard, fixture, registry row or previously approved packet changes. An APPROVE here means only "this contract is worth carrying through Tasks 0.1–1.4 unchanged".

## Why the change exists

Doc 17 §17.2.6 fixes the public DAG facade, and the run runtime behind it exists on the durable bridge. The facade cannot be written today: both handles are public sealed types in `OrcaCore.Dag` with internal constructors, their behavior must come from `OrcaCore.Dag.Hosting`, and that assembly may reference only the three runtime-view types and fifteen members pinned by `DagRuntimeViewBoundaryGuards`. The canonical friend requirement says a further seam needs its own approved amendment.

## What to review

1. **Sufficiency and minimality of the grant.** The proposal adds exactly two internal constructors (on `DagDefinitionHandle<TRunInput>` and `DagRunHandle`) whose parameters are public types and delegates over public types. Is anything in doc 17 §17.2.6 not implementable through them plus public API? Is any parameter unnecessary? The claimed allowlist is three non-public types and seventeen non-public members.
2. **Ownership split.** Typed result construction, foreign-plan node rejection by reference identity, the declared-output type check, and the `DagRunNotFoundException` / `DagRunDefinitionMismatchException` / `DagRunFaultedException` throws stay inside `OrcaCore.Dag`. Hosting signals absence, mismatch and fault only through delegate results. Is any typed throw or check left on the wrong side?
3. **Values with internal constructors.** `DefinitionRegistrationConflict`, `StartIdempotencyConflict`, `WorkflowFailure` and `PayloadFingerprint` are to be created in `OrcaCore.Engine.Durable` and cross the existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` bridge as opaque public values. Confirm this needs no tenth product friend and no widening of the six-signature `OrcaCore -> OrcaCore.Dag` authoring allowlist, and that the friend chain actually permits it.
4. **Delta correctness.** Both deltas are `MODIFIED`. Verify each heading verbatim against canonical at the parent, that every byte outside the listed edits is identical to the canonical block, and that the four edited clauses in design §8 are the complete set of changes to existing text. Judge whether the edited sentences now read without contradiction.
5. **Owner decisions.** The owner accepted D1 and D2 on 2026-11-15; the reviewer may still reject them.
   - D1: a run in the internal `IntegrityFault` state keeps its last committed public status in snapshots; `WaitForTerminalAsync` and `RequestCancellationAsync` throw a new public `DagRunFaultedException`. The signal is a null result from the two delegates. Check this against the approved run contract's statement that `IntegrityFault` is neither a new public enum value nor a false terminal result, and that Task 8.6 freezes the projection.
   - D2: `DagRunDefinitionMismatchException` gains expected/actual version and fingerprint.
   - D3: doc 17's listed internal constructors `DagDefinitionHandle()` and `DagRunHandle(DagRunId)` are replaced by the delegate constructors.
6. **Frozen status projection** (design §4): `StartAuthorized` projects as public `Running`; `OutputAvailable` is true only for a `Succeeded` resultful node; node failures carry the persisted stable code, the node id as location and no child cause. Is the "no child cause in v1" restriction acceptable?
7. **Inherited source obligations** (design §6), in particular the claim that `DurableDagRunStore.FindExistingAsync` contradicts the approved run contract by treating a start-key owner with a different run id as a conflict, and that `Register` must be the plan-install point for restart recovery.
8. **Sequencing.** The draft would let Task 8.6 source start while reshape 8.3–8.5 and `admit-durable-dag-run-progression` 2.4/3.x are unchecked. Is that acceptable, or must those close first?
9. **Task graph** (tasks.md): does it satisfy the post-gate amendment rules for a fourth owner of two headings, or is a step missing?

## Evidence supplied

- **Standalone proof.** `artifacts/task-1-1-public-handle-seam-proof-dag-2026-11-15.cs` and `…-proof-hosting-2026-11-15.cs` were compiled as two assemblies (`Proof.Dag` with `InternalsVisibleTo("Proof.Dag.Hosting")`, and `Proof.Dag.Hosting`) on net10.0 with nullable enabled and warnings as errors, referencing the real `OrcaCore.Dag` project at the parent commit: 0 warnings, 0 errors. The hosting assembly implements register, start, reopen, snapshot, output, terminal wait and cancellation, and the doc 17 application journey compiles cast-free. The proof uses stand-in exceptions where the real DAG exceptions have internal constructors; it proves signature sufficiency, not runtime behavior. The scratch projects are not part of the target; the reviewer should recompile the two files.
- **Structure.** `openspec validate admit-dag-public-handle-seam --strict` reports the change valid. This is structural validation only and is not offered as semantic approval or workflow compliance.
- **Delta provenance.** Both deltas were generated by script from the canonical files with a uniqueness assertion on each replaced clause. A line diff of the `developer-facing-surface` delta against its canonical block shows one changed canonical line (the paragraph holding three edited clauses) plus insertions.

## Known red and not done

- With the directory present and no registry row, `OpenSpecCorpusGuards.CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners` and `OpenSpecCorpusGuards.Task53_ReshapeSolelyOwnsFriendTopologyAndDurableGovernanceAggregate` fail. This is expected until Task 0.1.
- Eleven `ExecutableBehaviorInfrastructureGuards.Section7Scenario_… 3.11d` cases also failed in the draft worktree. They were not checked on a clean copy of the parent commit, so they are neither attributed to nor cleared from this target.
- Only the Guards project was built and run. No product, provider or integration lane was run; the target changes no compiled input.
- Not prepared: the post-gate registry rows, provenance fixture refreeze, `review-manifest-provenance.json` and `immutable-document-history.json` entries, and every document reconciliation listed in design §7. The request and manifest are therefore not yet catalogued by the guards.
- No provenance row count, byte count or unresolved-operation table from `openspec-provenance-checkpoint.json` is reported, because that fixture was not refrozen.

## Freeze and disposition

- Manifest: UTF-8 without BOM, LF, final LF; byte-equivalent to raw `git status --porcelain=v1 --untracked-files=all --no-renames` and its NUL form converted to LF. The manifest includes itself and this request.
- Semantic record: exclude `docs/review/**`. Each row is path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF: 8 rows / 1,178 B / 021586eb945f0cd0f3b7d16731a6fbf6864dda647508236fa201fc9c3abc6842.
- Active record: XY<TAB>path<TAB>raw byte count<TAB>lowercase SHA-256, ordinal-sorted and LF-joined with one final LF, with no exclusion. Its anchor and the projected tree are in the handoff to avoid self-reference.
- Independent review only. Leave the target uncommitted. A verdict on this draft does not activate any checkbox. If approved, the next target is Task 0.1 (registry rows and provenance refreeze), reviewed on its own; if rejected, remediate in a new target and keep this packet byte-exact.
