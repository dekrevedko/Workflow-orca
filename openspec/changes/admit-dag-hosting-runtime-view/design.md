## Context

Task 8.2 source is independently approved at `a9f835f939d683500ca231c7ba491ab8eae2aaae`, with direct-child verdict evidence `055e7b8e71e8dfe79e76f267f8782b7f6f79f7b8`. The authoring-friend closeout is separately approved: checkpoint `b5fb28e65dbf3fea102ddec1d5fe1cf9d794c659`, evidence `dbc3086da206bde20cc8624816e3f45f1d13f9b8`, and checkbox-only activation `6d49716384b50e2cbaf503fcd782b63c6384bc1e`. All thirteen authoring-amendment tasks are complete. This new proposal does not stack on an incomplete source gate.

The public `WorkflowDagPlan<TRunInput>` exposes only identities and opaque node references. Its node plans, mapper delegates and the `DagNodeInputContext<TRunInput>` constructor are internal. The Hosting project references Dag and Durable.Hosting only; the current exact eight-friend graph has no Dag-to-Hosting grant. Reflection and public executable metadata would contradict the approved contract.

The existing durable runtime already reaches `CoreWorkflowValueCodec` through its reviewed Core friend. `OrcaCore.Dag` must stay an authoring package, not a fixed-codec/runtime participant.

## Goals / Non-Goals

**Goals:** propose one guarded runtime-view friend; preserve the authoring grant; put mapping evaluation on a compile-checked seam; keep codec and child work runtime-owned; preserve every earlier approval; review the process, contract, atomic sync, source, and closeout separately.

**Non-Goals:** no source or friend attribute in this process target; no public factory/compiled structure; no direct Dag-to-Core/engine/protocol dependency; no extra test friend; no changes to mapper code identity, DAG failure policy, admission, persistence or public signatures; no new codec allowlist member.

## Decisions

### 1. Preserve the ordered ownership chain

Only two headings may acquire a third active owner: reshape predecessor -> completed `admit-dag-authoring-friend-boundary` -> proposed `admit-dag-hosting-runtime-view`. The two older blocks retain their source-pinned hashes and stages. Canonical still equals the completed authoring successor. The newest blocks are full `MODIFIED` copies plus the runtime-view proposal, and remain `PendingModification`; semantic approval stays false. A third owner for any other heading, or a fourth owner for either heading, is red.

The process-only gate is not the friend contract review. Tasks 1.1–1.2 follow its own independent checkpoint. Tasks 1.3 and 1.4 are one atomic canonical-sync/ApprovedPending target; the two historical predecessor rows must be superseded only by exact hashes after that approved transition. No source follows until that separate review chain lands.

### 2. One internal runtime view, not raw authoring internals

Dag owns immutable node descriptors and a single mapping-evaluation entry point. Hosting receives only the validated view, typed mapped input and declared input type or `DAG_INPUT_MAPPING_INVALID`; it must not access draft mutation or mapper delegates. Task 1.1 proposes the exact internal type/member signatures in section 6 below; compiled metadata must match them in the separately reviewed source target. Both reference kinds are guarded; generics, attributes, base types and interface implementations cannot bypass the allowlist.

Public metadata/factories would expose executable structure. Moving mapping into Core would break the package graph. Widening the OrcaCore authoring allowlist with the codec would assign runtime work to the wrong package. Those alternatives are excluded.

### 3. Runtime owns values and commit

Before evaluation, the durable bridge decodes successful committed dependency outputs using their declared types and provides detached values. Hosting hands the evaluator's typed input and declared type back to that bridge. The runtime normalizes the mapped input using `orcacore-json-v1`, fingerprints the exact committed bytes, and commits input before child start. Dag neither serializes nor decodes values and never imports runtime protocol. The implementation follows the existing Core -> engines -> durable hosting seam rather than adding codec friends.

This contract target revises only open reshape 8.3–8.5 task text and appends a dated re-sequencing note: 8.3 owns mapping validation, 8.4 defines the codec/materialization bridge, and 8.5 implements its round-trip and once-only commit. Existing durable-runtime and workflow-contracts requirements remain unchanged. If bridge design requires changing either requirement, a separate delta and approval must precede source. The actual bridge implementation remains a later reviewed slice.

### 4. Behavioral evidence belongs at the host boundary

No test friend is added. Compiled Dag.Hosting-level behavior tests must exercise successful mapping, invalid foreign/non-direct/unavailable outputs, mapper failure before commit/start, replay and fixed-codec detachment through the durable bridge. Metadata probes test forbidden types and members separately. The current fourteen intentional guard reds remain a separate lane; this process target does not turn any Section 8 scenario green.

### 5. Complete document disposition, not a partial friend list

Contract Task 1.1 owns `CLAUDE.md`; docs 03, 08 CP-020/CP-022, 10 PR-005, 17 §17.2.6, §17.3 and §17.5; binding Decision 22 and solution architecture; project technical overview; and the Task 8.0 map. Document 11 is explicitly excluded because the `OrcaCore` package closure is unchanged. Existing canonical workflow-authoring opacity and durable-runtime DAG progression remain unchanged owners and are checked for consistency. Historical reshape design and dated decisions are linked/excluded, never silently rewritten. Refresh all affected Task 7.3 hashes atomically with their reviewed document edits.

The Task 8.0 map's stale 8.4 status is corrected in this process target: the authoring implementation is already independently approved at the real source checkpoint, while this different runtime view remains proposed. Its sole DAG-to-durable bridge statement remains true.

## Risks / Trade-offs

- Broad CLR friendship -> exact compiled type/member references, public API baselines and mutation probes constrain the consumed seam.
- Another successor accidentally hides pending work -> source-pinned ordered owners and old/new block hashes; two pending operations explicitly remain in the recomputed record.
- Codec work leaks into Dag -> unchanged six-member authoring allowlist and exact package graph; bridge-only codec ownership.
- Reopening historical approvals -> immutable packets and completed registry evidence remain unchanged; new artifacts append status.
- Registry sync order -> Tasks 1.3/1.4 must land atomically; no temporary canonical transition under Proposed.

## Migration Plan

Checkpoint the process target only after independent approval. Then review the complete friend contract and document/ledger dispositions. Synchronize the two approved successors and advance their registry atomically, review and checkpoint. Implement the runtime view and metadata probes as the next independently reviewed source target; build the durable codec/child bridge in its owning 8.4/8.5 slice. Promote the permanent record only with actual source and approval evidence. No archive or runtime source is part of this proposal target.

## 6. Exact proposed runtime-view contract (Task 1.1, 2026-10-02)

The process gate is approved and activated at `2a09b452af067fbd501215a572712ff08cf2bbc9`
(checkpoint `dc8095c5536316cb772c985641e45179fa3c93b5`, evidence
`0f4fafa3c3b3acfdb2c39227bba53f094782bd13`). This section proposes the contract only;
the ninth friend is not approved, canonical, or compiled. The current product graph has eight
friends. The existing six-signature `OrcaCore -> OrcaCore.Dag` authoring grant is unchanged.

All types below are in `OrcaCore.Dag`. This is a signature contract, not source to compile now.
Constructors and backing state are inaccessible to Hosting. A validated plan constructs its own
view; callers cannot mint descriptors or results. Collections are defensive immutable snapshots,
including descriptor dependencies; authored order and exact plan-local node references are retained.
Descriptors contain identities and declared types, not executable workflow/mapper delegates.

```csharp
// On the existing public WorkflowDagPlan<TRunInput>:
internal DagRuntimeView<TRunInput> GetRuntimeView();

internal sealed class DagRuntimeView<TRunInput>
{
    internal IReadOnlyList<DagRuntimeNodeDescriptor> Nodes { get; }
    internal DagMappedInputResult EvaluateMapping(
        DagNodeRef node,
        TRunInput immutableRunInput,
        IReadOnlyDictionary<DagNodeRef, object?> successfulDirectDependencyOutputs);
}

internal sealed class DagRuntimeNodeDescriptor
{
    internal DagNodeRef Reference { get; }
    internal int AuthoredOrdinal { get; }
    internal DefinitionId ChildDefinitionId { get; }
    internal DefinitionVersion ChildDefinitionVersion { get; }
    internal DefinitionFingerprint ChildFingerprint { get; }
    internal Type InputType { get; }
    internal Type? OutputType { get; }
    internal IReadOnlyList<DagNodeRef> Dependencies { get; }
}

internal sealed class DagMappedInputResult
{
    internal bool IsValid { get; }
    internal object? Input { get; }
    internal Type? InputType { get; }
    internal string? FailureCode { get; }
}
```

Exactly three non-public type families and fifteen internal member signatures may be consumed:
one plan method, one view getter, one evaluator, eight descriptor getters and four result getters.
The metadata allowlist uses decoded signatures, including generic arity and parameter/return types,
not names alone; no constructor, setter, extra overload, draft, `DagNodePlan`, context constructor,
internal reference coordinate, or mapper delegate access is admitted. Public BCL and OrcaCore/Dag
types in these signatures do not grant further internal access. The runtime-view implementation
inside Dag may use its own internals; Hosting may consume only this exact boundary.

The selected node must be the same plan-local reference as a view descriptor. The host invokes
mapping only after every declared direct dependency has committed success. Resultless dependencies
have no output entry; successful resultful outputs are decoded by the durable bridge into a
read-only direct-dependency map before invocation. `OutputOf` must still reject foreign-plan,
undeclared, non-direct, unavailable and wrong-type outputs; ordinary mapper exceptions become
`IsValid == false`, `FailureCode == "DAG_INPUT_MAPPING_INVALID"`, null Input/InputType. Success
has `IsValid == true`, the exact declared InputType, the typed Input (possibly null), and null
FailureCode. A valid null input is not confused with failure. No evaluator path serializes,
fingerprints, persists, starts or joins a child. Build/TryBuild never invokes mapping.



Mapper failure classification is explicit: all catchable mapper exceptions derived from `Exception`, including a mapper-thrown `OperationCanceledException`, produce `DAG_INPUT_MAPPING_INVALID`, except `OutOfMemoryException`, `StackOverflowException` and `AccessViolationException`; those process/resource-integrity failures are not normalized. Host/run cancellation observed outside mapper invocation remains the runtime cancellation outcome, not a fabricated mapping failure. The evaluator has no cancellation-token parameter. An ordinary declared-type decode/materialization failure on the durable bridge produces `DAG_INPUT_MAPPING_INVALID` before mapper invocation, input commit or child start. Protocol-integrity and storage failures remain runtime failures, never successful outputs or disguised mapping failures. Reshape 8.4 defines this distinction and 8.5 implements it; Dag performs no decoding.

## 7. Successful-null policy and normative reconciliation

Doc 17 §17.2.6 is amended in this target, not merely cited. A present successful output whose
declared type is a reference type or `Nullable<T>` may be null. Missing map entries and null for
nonnullable value types remain invalid. C# nullable-reference annotations are not a runtime
discriminator. `OutputOf<T>` returns valid null with the requested declared type; mapped inputs
follow the same null/type rule. The bridge's fixed-codec certification, bytes and detach semantics
still apply, including JSON null. No codec or runtime-protocol operation moves into Dag.

This explicitly changes the current unimplemented runtime `OutputOf` null check, which presently
rejects every null. It changes no public signature and does not claim the candidate code already
implements the policy. Source Task 2.2 must prove valid reference/nullable null, missing and
nonnullable null, incorrect output type, mapper exception and no commit/start on failure through
Hosting-level behavior without a new test friend.



Null-rule ownership: canonical `durable-runtime` requirement “Durable DAG progression is runtime owned” already requires successful declared direct outputs and rejects “otherwise invalid” accesses; it does not declare successful null invalid. This proposal specializes which declared-type values are valid without changing dependency success, readiness, commit order or codec ownership. Canonical `workflow-authoring` requirement “Built definitions are immutable” governs opacity/immutability and has no OutputOf null rule. Therefore neither needs a semantic rewrite for this refinement: the exact package-boundary successor and numbered doc 17 §17.2.6 carry the rule, and 1.3/1.4 must verify both unchanged owners against it. Any discovered contradiction requires a separately reviewed owning-capability delta before source; this contract is not authority to bypass one.

## 8. Planned atomic supersession, archival and withdrawal

These are rules for the later combined 1.3/1.4 target; no lifecycle logic is implemented in 1.1.
After independent contract approval, atomically synchronize the two newest blocks and add
ApprovedPending rows bound to the actual contract checkpoint, direct-child evidence and verdict.
Keep the original Proposed rows, authoring Proposed/ApprovedPending/Complete evidence and both
older deltas byte-exact. Classify both reshape and authoring predecessor blocks as superseded
only for the two exact registered headings and exact before/after hashes. The author's completed
implementation remains a true historical fact; it does not make its old wording current canonical.
No unrelated block or extra owner is excused. Runtime-view source tasks must remain open until the
atomic transition itself is independently approved and checkpointed.

Later normal archival resolves the completed predecessor through exactly one active or dated
archived record, preserves its old block/evidence and refreshes the active inventory; it must not
require an archived predecessor to remain an active owner forever. Active-or-archived predecessor resolution SHALL be implemented in the atomic 1.3/1.4 target.
An actual archive operation still requires a separate reviewed inventory/provenance target. A Proposed runtime-view withdrawal requires one reviewed registry
change that removes both runtime-view rows and the change directory together, restores the exact
two-owner authoring chain, and reconciles docs/provenance. Every dated packet/artifact stays as
immutable history. After canonical synchronization, withdrawal is not deletion: it needs a new
reviewed reverse/successor amendment. Neither withdrawal nor archival is performed in 1.1.

## 9. Complete disposition and authority

See `artifacts/task-1-1-runtime-view-contract-rv-remediation-2026-10-02.md` for every amended or excluded mapped
file, the exact fifteen-member contract and ledger handoff. Active numbered/binding/guide docs call
the ninth friend proposed, not approved or compiled. `CLAUDE.md` retains current eight. Historical
reshape Decision 22 is linked/excluded; earlier dated Decision 22 entries are append-only.
Canonical `openspec/specs/` and both old authoring deltas remain untouched. Task 1.1 may be prepared
and checked; 1.2–3.2 remain open. A subsequent independent APPROVE and exact checkpoint is required
before the atomic sync target; green structural validation is not semantic approval.

## Open Questions

- The descriptor/evaluator and successful-null proposals above require independent contract approval at Task 1.2; no ad hoc test friend is permitted.
- Existing structural DAG fingerprints omit `TRunInput`; this proposal does not amend that approved design. Task 8.6 must settle registration compatibility before changing it.

## 10. Rejected-freeze remediation (2026-10-02)

The original Task 1.1 packet and its REJECT verdict remain byte-exact. RV-1 is corrected by leaving Task 1.2 unpinned in the Proposed-stage guard: its checkbox-only activation may follow real approval, but it grants no canonical/source authority. Atomic 1.3/1.4 must require 1.2 complete and bind its actual checkpoint, direct-child evidence and terminal APPROVE. RV-2 is corrected by the complete signature contract in both proposed deltas and doc 17 §17.2.6; doc 08 points there, not at change-local design. The numbered proposal blocks and reshape 8.3–8.5 handoff are durably source-pinned, independent of the temporary freeze. All five non-blocking notes are addressed in the new remediation artifact.
