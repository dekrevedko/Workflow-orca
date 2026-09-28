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

After that process checkpoint, independently approve the normative amendment and reconcile `CLAUDE.md`, document 17, canonical specs, the Task 8.0 map note, and the exact friend guard before Task 8.2 product source. The current completed-amendment validator requires approval, executable evidence, refreeze, and final verdict paths; it must not receive a fictional complete row now. After the contract verdict, extend the provisional registry stage to an approved-pending stage tied to real amendment approval and open Task 8.2 evidence; review that transition before adding the friend and DAG product code. Promote the row to complete only after real implementation evidence and an independently approved refreeze exist. No completed historical gate supplies implicit approval.

## Risks / Trade-offs

- **Broad CLR friend grant** → keep all internals non-public, pin the single friend edge, and reject non-allowlisted DAG member/type references from compiled metadata with mutation tests.
- **Workflow fingerprint drift during hash extraction** → compare existing compiled-plan digests byte-for-byte before/after the move, including fixed-codec and version-bump cases.
- **Duplicated diagnostic semantics** → use the existing `WorkflowDiagnosticCatalog`, `Validation<T>` sorter, and exception constructor; pin exact code/location/order parity.
- **False post-gate completeness** → distinguish a pending registry row from a completed one and require real paths/approval before promotion.

## Migration Plan

There is no released consumer or persisted DAG format to migrate. First review and checkpoint the process-only proposed-successor gate; then independently approve the normative amendment, synchronize canonical/spec and documentation truth, and review the approved-pending registry transition. Only then implement Task 8.2 source and metadata guard, update affected exact API/provenance fixtures, run focused and full lanes, refreeze, and obtain independent approval before its checkpoint. A rejected amendment leaves Task 8.1 intact and Task 8.2 open.

## Open Questions

None for the friend decision. The exact metadata-token reader and DAG canonical-structure byte format are implementation details to be frozen and tested before Task 8.2 checkpoint; they may not expand the allowlist or public API without another amendment.
