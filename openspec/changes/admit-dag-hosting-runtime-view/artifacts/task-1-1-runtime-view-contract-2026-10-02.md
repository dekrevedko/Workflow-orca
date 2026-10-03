# DAG hosting runtime-view contract and complete disposition

Date: 2026-10-02
Change: `admit-dag-hosting-runtime-view`, Task 1.1
State: **Proposed; awaiting independent contract approval**

## Approved process base, not runtime authority

The process-only checkpoint is `dc8095c5536316cb772c985641e45179fa3c93b5`, tree
`d8f7d84fe751c323c11e2190b7c03a39f1d8cc5e`, parent
`6d49716384b50e2cbaf503fcd782b63c6384bc1e`. Its direct-child evidence
`0f4fafa3c3b3acfdb2c39227bba53f094782bd13` adds the 11,549-byte 2026-09-30 APPROVE
verdict unchanged (`bc72cd0eca94e77a1c09744e18aea6bb0133cfe5429ddf5a50fdfb4e0c70cd4c`),
catalogs it, and clears the active freeze. Checkbox-only activation and this target's base is
`2a09b452af067fbd501215a572712ff08cf2bbc9`. The committed evidence state passed 240/240
Infrastructure. No product source changed in that chain.

Task 1.1 prepares this contract; 1.2–3.2 remain open. Both runtime-view registry rows stay
Proposed, both newest blocks stay pending, semantic approval is false, canonical is untouched,
and the compiled product graph still has exactly eight friends. The ninth friend below is a
proposal, not an approved or compiled edge. No friend attribute, codec access or Task 8.3 source
is in this target. Every older dated request, manifest, verdict and provenance artifact is retained.

## Exact future boundary

All types are in `OrcaCore.Dag`. The only non-public type references allowed for Hosting are
`DagRuntimeView<TRunInput>`, `DagRuntimeNodeDescriptor`, and `DagMappedInputResult`.
The same signature contract is in design §6; neither is compiled source in this target.

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

The fifteen consumed member signatures are:

1. `WorkflowDagPlan<TRunInput>.GetRuntimeView() : DagRuntimeView<TRunInput>`
2. `DagRuntimeView<TRunInput>.get_Nodes() : IReadOnlyList<DagRuntimeNodeDescriptor>`
3. `DagRuntimeView<TRunInput>.EvaluateMapping(DagNodeRef, TRunInput, IReadOnlyDictionary<DagNodeRef, object?>) : DagMappedInputResult`
4. `DagRuntimeNodeDescriptor.get_Reference() : DagNodeRef`
5. `DagRuntimeNodeDescriptor.get_AuthoredOrdinal() : int`
6. `DagRuntimeNodeDescriptor.get_ChildDefinitionId() : DefinitionId`
7. `DagRuntimeNodeDescriptor.get_ChildDefinitionVersion() : DefinitionVersion`
8. `DagRuntimeNodeDescriptor.get_ChildFingerprint() : DefinitionFingerprint`
9. `DagRuntimeNodeDescriptor.get_InputType() : Type`
10. `DagRuntimeNodeDescriptor.get_OutputType() : Type?`
11. `DagRuntimeNodeDescriptor.get_Dependencies() : IReadOnlyList<DagNodeRef>`
12. `DagMappedInputResult.get_IsValid() : bool`
13. `DagMappedInputResult.get_Input() : object?`
14. `DagMappedInputResult.get_InputType() : Type?`
15. `DagMappedInputResult.get_FailureCode() : string?`

Decoded CLR signatures pin generic arity, parameter and return types; nullable-reference annotations
do not create distinct CLR overloads. No constructor, setter, draft, raw node plan, context
constructor, internal reference coordinate, mapper delegate, extra overload or additional
non-public type is allowed. Nodes and dependencies are defensive immutable snapshots. The plan
creates its own view; Hosting cannot fabricate one. Descriptor identities/types enable the
existing internal durable bridge to resolve the committed registered child contract; they confer
no application-visible raw-string definition selection or new child-management authority.

## Evaluation and null policy

The durable bridge decodes successful committed resultful dependency outputs using their declared
types **before** evaluation. Resultless dependencies have no output entry; every dependency must
have succeeded before invocation. The evaluator validates the exact selected plan-local node and
the same `OutputOf` plan/direct/success/type rules, then returns typed input and its declared type.
Ordinary mapper/access/type failure returns only `DAG_INPUT_MAPPING_INVALID`, never commits input,
starts a child, or converts a failed dependency to a successful output.

Success: `IsValid` true, exact declared `InputType`, typed `Input` (possibly null), null
`FailureCode`. Failure: `IsValid` false, null `Input`, null `InputType`, and
`FailureCode == "DAG_INPUT_MAPPING_INVALID"`. Mapper code is not invoked by Build/TryBuild.

The proposed null clarification explicitly amends doc 17 §17.2.6 and is repeated in CP-022:
a present successful output can be null only for a reference or nullable-value declared type;
`OutputOf<T>` returns that null. Missing entries, wrong types and nonnullable-value null stay
invalid. The same rule governs null mapped input. C# nullable-reference annotations are not a
runtime discriminator. This target does not claim the present all-null-rejecting implementation
complies: source Task 2.2 must implement and test the approved policy separately. No public
signature or canonical fixed-codec/durable progression rule is changed here.

Dag does not decode, serialize, normalize, hash committed bytes, persist, start or join children.
The unchanged six-member `OrcaCore -> OrcaCore.Dag` authoring allowlist cannot gain codec access.
The `OrcaCore.Dag -> OrcaCore.Dag.Hosting` runtime-view proposal and the existing
`OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` durable bridge are distinct grants.
No new test friend: Hosting-level compiled behavior must prove valid/invalid direct access,
ordinary mapper failure, null policy, copied descriptors, and no commit/start on mapping failure.
The later bridge slice proves codec detachment, once-only commit and restart reuse.

## Document disposition (targets, not only delta inputs)

| Source | Disposition in this Task 1.1 target |
|---|---|
| `CLAUDE.md` | Amended: current eight retained; proposed ninth and bridge-only codec ownership stated. |
| `docs/specs/03-domain-model-and-glossary.md` | Amended: distinguishes compiled authoring grant from proposed runtime-view grant. |
| `docs/specs/08-requirements-composition.md` CP-020/CP-022 | Amended: exact proposed grant; output decoding and successful-null clarification. |
| `docs/specs/10-provider-model-and-extensibility.md` PR-005 | Amended: exact proposed grant and no codec/public child authority; exact package exclusion retained. |
| `docs/specs/17-selected-mode-capability-matrix.md` §§17.2.6/17.3 | Amended: proposed view and successful-null policy; existing diagnostic codes/build rules unchanged. |
| `docs/implementation/00-stack-decisions.md` Decision 22 | Amended current table; appends 2026-10-02 proposal entry. Every earlier dated entry is byte-exact. |
| `docs/implementation/01-solution-architecture.md` | Amended: proposed ninth and bridge-before-evaluator decoding; current eight unchanged. |
| `docs/project-technical-overview.md` | Amended: proposed ninth only, distinct authoring/runtime/durable bridges. |
| `openspec/changes/reshape-developer-facing-interfaces/artifacts/task-8-0-section-8-requirement-gate-2026-09-26.md` | Amended 8.3/8.4/8.5 rows: mapping vs bridge-codec ownership, map pin refreshed. Historical expected-red handoff disclosed, no disposition credit. |
| `openspec/changes/reshape-developer-facing-interfaces/tasks.md` | Only open 8.3–8.5 task text edited; dated re-sequencing note appended. No completed task or dated decision rewritten. |
| `openspec/changes/admit-dag-authoring-friend-boundary/tasks.md` | Current 3.2 completion prose corrected to actual approved checkpoint/evidence/activation. Its checksum-only activation and immutable closeout artifact remain unchanged. |
| `docs/specs/11-non-functional-requirements.md` NF-002 | Explicitly excluded: primary package dependency closure is unchanged; a friend adds no package reference or SDK. |
| `openspec/specs/workflow-authoring/spec.md` Built definitions are immutable | Explicitly unchanged: no public delegate/draft/compiled metadata; the view is internal and immutable. |
| `openspec/specs/durable-runtime/spec.md` Durable DAG progression is runtime owned | Explicitly unchanged: runtime owns codec, committed input and child progression. Ledger-only re-sequencing changes no required behavior. |
| `openspec/specs/workflow-contracts/spec.md` Durable values use one fixed detached codec | Explicitly unchanged: bridge remains subject to fixed-codec certification and detach/commit rules. |
| `docs/specs/17-public-authoring-contract.cs` | Explicitly excluded: workflow-only public companion; no DAG/public signature is amended. |
| `openspec/changes/reshape-developer-facing-interfaces/design.md` Decision 22 | Historical change-local decision explicitly excluded; current friend status is in this amendment and active bindings, not a rewritten old exact list. |
| Canonical two affected specs and both older owning deltas | Untouched until separately approved atomic 1.3/1.4; exact old hashes retained. |
| Other mapped numbered requirements/acceptance references | Checked for boundary restatements; their package/typed-reference/codec/child obligations remain unchanged. |

The reverse sweep covers all `docs/specs`, `docs/implementation`, and root guide Markdown files.
Historical dated entries are identified as history, not rewritten into current-state evidence.
All intentionally edited Task 7.3 documents are rehashed in its 22-row artifact in this same target;
the remaining rows are unchanged. Doc 17 is outside that historical 22-source table but is explicitly
amended and review-bound here.

## Proposed ledger handoff and lifecycle

- 8.3: runtime-view mapping/direct-output validation only; no codec in Dag.
- 8.4: define durable bridge's output decoding, input normalization, declared types, fingerprints,
  deterministic child identity/lineage and cancellation contract.
- 8.5: implement bridge codec round-trip, once-only input commit and restart-safe child start/reattach.

Any canonical durable-runtime/workflow-contracts incompatibility requires a separate approved delta,
not an implicit task-ledger correction. No expected-red scenario or acceptance waiver is relabelled.

Design §8 and task 1.4 specify, but do not implement, the future atomic transition: retain both
older owner blocks and all Complete authoring evidence, exact-hash supersede reshape and authoring
only on these two identities, and resolve archived predecessors once rather than requiring perpetual
active ownership. Proposed withdrawal needs one reviewed change removing both runtime rows and the
change directory together, restoring the authoring chain while preserving immutable history.
Withdrawal after canonical sync needs a reverse/successor amendment. No withdrawal/archival occurs.

## Review observations addressed

- Future handoff publishes the reproduced simulated tree outside the self-inclusive request.
- All three ambiguous grant phrases explicitly name the authoring/runtime-view grants.
- Durable bridge output decoding occurs before evaluator input.
- Authoring Task 3.2 prose now names its real completed chain, without changing dated records.
- Supersession, archival and withdrawal rules are written for 1.3/1.4; no lifecycle shortcut lands.
- Successful-null behavior is explicitly proposed in the numbered normative doc, not hidden in design.

This contract preparation is not its own independent approval. Task 1.2 must receive an immutable
APPROVE followed by its exact checkpoint/evidence/activation before canonical synchronization.
