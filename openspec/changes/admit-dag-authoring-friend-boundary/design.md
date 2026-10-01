## Context

`OrcaCore.Dag` is the approved owner of typed DAG authoring and references only `OrcaCore`. Task 8.2 must return `Validation<WorkflowDagPlan<T>>`, emit `WorkflowDiagnostic` with `AuthoredLocation`, retain a `DefinitionFingerprint`, and make `Build` throw `WorkflowDefinitionException` carrying the same ordered diagnostics. Those five public types deliberately expose internal constructors. The seven existing product friends omit `OrcaCore -> OrcaCore.Dag`, and the current metadata guard rightly rejects an eighth. The closed Task 8.0 gate does not authorize a silent exception.

## Goals / Non-Goals

**Goals:** Admit exactly one authoring friend; preserve the public signatures and one-way package graph; make actual DAG use of `OrcaCore` internals narrower than the CLR friend grant through a compiled-metadata guard; reuse the current diagnostic ordering and fingerprint hash implementation; require a post-gate approval before Task 8.2 product source.

**Non-Goals:** Public constructors/factories, DAG-owned replacements for the five values, a `Core -> Dag` friend or `Dag -> Core` reference, DAG runtime/child-start privileges, a new durable bridge, or credit for tasks 8.2–8.10 before their separate reviews.

## Decisions

### One additional product friend, with a member-level consumer rule

Amend the exact product list with `OrcaCore -> OrcaCore.Dag`. This is the same assembly-level mechanism already used by Core and the engines. The grant is broad at CLR level, so a must-green guard SHALL inspect the compiled `OrcaCore.Dag.dll` metadata and resolve every reference to a non-public `OrcaCore` type or member. The only allowed internal references are constructors of `OrcaCore.Validation<T>`, `OrcaCore.WorkflowDiagnostic`, `OrcaCore.AuthoredLocation`, `OrcaCore.DefinitionFingerprint`, and `OrcaCore.WorkflowDefinitionException`, plus the one internal canonical-hash operation on `DefinitionFingerprint` described below. Signatures and arity are part of the allowlist; an unrelated overload on one of these types is not approved. The guard SHALL separately assert the complete friend-attribute set on `OrcaCore.dll` and reject any internal `OrcaCore` runtime, hosting, child-start, engine, or protocol access by `OrcaCore.Dag`.

The guard reads metadata rather than searching C# strings. Negative controls must prove that an extra internal method, an internal type, or an unlisted overload referenced from DAG turns the guard red, while ordinary public `DurableWorkflowRef`/identity references remain legal. The exact public-API baseline remains exhaustive and unchanged by the friend grant.

### Share construction behavior without a second friend

`Validation<T>` already sorts diagnostics by ordinal `AuthoredLocation.Value` and then code; `WorkflowDiagnostic` already validates codes against `WorkflowDiagnosticCatalog`. DAG constructs those same values directly and adds its seven approved `DAG-AUTH-*` descriptors to that existing catalog. `Build` throws one `WorkflowDefinitionException` over the `TryBuild` diagnostic sequence. There is no copy of Core's diagnostic ordering or need for `Core -> Dag`.

Move only the common UTF-8/SHA-256 hash primitive from `OrcaCore.Core`'s compiled-plan helper to an internal operation on `DefinitionFingerprint` in `OrcaCore`. Core and DAG both call it. Core keeps producing its existing uppercase digest over its exact `orcacore-json-v1|<canonical-structure>` bytes; a golden test must prove no workflow fingerprint drift. DAG supplies a separately domain-separated, deterministic structural serialization containing codec format, authored node ordinals and identities, durable child reference identity/version/fingerprint, and copied declared dependency ordinals. Opaque mapper bodies are excluded and require the already-approved definition-version bump. The shared operation hashes bytes; DAG owns only its own canonical structural serialization, not a duplicate hash algorithm.

### Post-gate sequencing

The initial planning draft has two full `MODIFIED` requirement blocks with verbatim canonical headings. Reshape remains active and owns both headings, so the existing no-duplicate corpus guard and Task 5.3's sole-owner assertion correctly reject the draft. Before a contract-approval freeze, checkpoint a process-only change that admits exactly these two ordered predecessor/successor pairs in a source-pinned `Proposed` registry stage. It must verify the prior reshape operation, this change's `MODIFIED` operation and open process task, reject any other duplicate, and refreeze provenance with two explicit pending operations and semantic approval disabled. The process checkpoint changes no canonical requirement, exact product-friend list, or Task 8.2 product source. Its own independent review approves the gate mechanism only, not the friend contract.

After that process checkpoint, independently approve the normative amendment and reconcile every document in the disposition table below, `CLAUDE.md`, document 17, the Task 8.0 map note, and the exact friend guard before Task 8.2 product source. The current completed-amendment validator requires approval, executable evidence, refreeze, and final verdict paths; it must not receive a fictional complete row now. After the contract verdict, synchronize both canonical blocks and extend the provisional registry to an approved-pending stage in one frozen, independently reviewed target. That atomic transition binds real amendment approval, canonical state, and open Task 8.2 evidence; it never leaves a synced successor paired with a `Proposed` registry row or drops the predecessor's historical record. Review and checkpoint it before adding the friend and DAG product code. Promote the row to complete only after real implementation evidence and an independently approved refreeze exist. No completed historical gate supplies implicit approval.

### Documentation disposition for the authoring friend

The contract review target also reconciles every active document that says DAG consumes only
public OrcaCore contracts or presents a closed product-friend graph. These edits describe the
eighth edge as *proposed*, not compiled or approved; public durable workflow references remain
public, while compiler-created authoring values need the separately guarded internal seam.
Here “active document” means the numbered normative, binding, and guide corpus, not another
change's dated design rationale. Reshape's older Decision 22 text is retained as historical
context; current Decision 22 and the synchronized canonical requirements govern the friend graph.

| Document | Disposition |
| --- | --- |
| `docs/specs/03-domain-model-and-glossary.md` | Amend the layer dependency sentence to distinguish the sole `OrcaCore` package reference from proposed internal authoring construction. |
| `docs/specs/08-requirements-composition.md` CP-020 | Amend the separate-package rule with the same public-reference/internal-authoring distinction. |
| `docs/specs/10-provider-model-and-extensibility.md` PR-005 | Amend the public-contract wording; preserve no reverse package edge and outward integrations. |
| `docs/specs/11-non-functional-requirements.md` | Explicitly excluded: NF-002 says DAG and companions are outside the `OrcaCore` package dependency closure, not that DAG may consume only public members. The proposed friend changes no package edge or closure. |
| `docs/implementation/00-stack-decisions.md` Decision 22 | Correct the current exact seven-product-friend enumeration and name the proposed authoring-only eighth without claiming it has landed. |
| `docs/implementation/01-solution-architecture.md` | Correct both exact friend lists and the public-only DAG sentence; retain the sole DAG-to-durable runtime bridge. |
| `docs/project-technical-overview.md` | Correct the exact friend list and distinguish proposed authoring access from the existing runtime bridge. |

Refresh Task 7.3 documentation rows 6, 7, 12, 14, and 18 plus its source-pinned artifact digest in the
same frozen target. Preserve the prior rejected request, manifest, and verdict byte-for-byte as
append-only review evidence. No numbered document is silently deferred to tasks 1.3/1.4.

## Risks / Trade-offs

- **Broad CLR friend grant** → keep all internals non-public, pin the single friend edge, and reject non-allowlisted DAG member/type references from compiled metadata with mutation tests.
- **Workflow fingerprint drift during hash extraction** → compare existing compiled-plan digests byte-for-byte before/after the move, including fixed-codec and version-bump cases.
- **Duplicated diagnostic semantics** → use the existing `WorkflowDiagnosticCatalog`, `Validation<T>` sorter, and exception constructor; pin exact code/location/order parity.
- **False post-gate completeness** → distinguish a pending registry row from a completed one and require real paths/approval before promotion.

## Migration Plan

There is no released consumer or persisted DAG format to migrate. First review and checkpoint the process-only proposed-successor gate; then independently approve the normative amendment; then synchronize canonical/spec truth and the approved-pending registry transition atomically under one freeze and independent review. Only then implement Task 8.2 source and metadata guard, update affected exact API/provenance fixtures, run focused and full lanes, refreeze, and obtain independent approval before its checkpoint. A rejected amendment leaves Task 8.1 intact and Task 8.2 open.

## Implementation closeout (2026-09-29)

Task 8.2 source was independently approved in the immutable ZZZ-remediation verdict and
checkpointed at `a9f835f939d683500ca231c7ba491ab8eae2aaae` (tree
`cf11b3f6732f11250ba3a0255114bcdc4ae00060`); its direct-child evidence commit is
`055e7b8e71e8dfe79e76f267f8782b7f6f79f7b8`. Schema 5 retains the exact Proposed and
ApprovedPending records as historical transitions and adds `completeEvidence` as the current
implementation disposition. That record binds the real source verdict, reviewed refreeze
manifest/request, executable test paths, five completed implementation tasks and reshape 8.2.
Approval validation requires exactly one final APPROVE line, a single-parent evidence commit
directly after its checkpoint, and an actual verdict addition; merely containing an earlier
verdict is insufficient. Task 3.2 remains open until this closeout has its own review/checkpoint.

The current eight-friend graph and authoring-only six-member seam are unchanged. Temporary
metadata probes now copy the repository SDK pin into their own directory before building;
this adds no product or test friend. Current documentation status is refreshed, while dated
Decision 22 entries and immutable review records are preserved and superseded by an append.
No new runtime-view seam or codec access is admitted. The proposed Dag-to-Dag.Hosting seam
and runtime-owned fixed-codec work require a separate amendment after this closeout checkpoint.

## Open Questions

None for this authoring-friend closeout. The metadata reader and DAG canonical format were
independently reviewed with Task 8.2. A new runtime-view seam is a separate post-gate decision,
not an unresolved implementation detail or authority supplied by this record.
