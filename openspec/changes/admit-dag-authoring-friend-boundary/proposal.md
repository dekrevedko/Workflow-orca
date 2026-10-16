## Why

Reshape Task 8.2 cannot implement the already-approved `OrcaCore.Dag` `Build`/`TryBuild` API: five compiler-created `OrcaCore` value families have internal constructors, but the exact friend graph does not grant `OrcaCore.Dag` access. Making those constructors public would let applications forge diagnostics and fingerprints. The boundary must be amended and independently approved before Task 8.2 product source changes.

## What Changes

- Add the single product friend `OrcaCore -> OrcaCore.Dag` for DAG *authoring* construction only. Keep the public API, package references, durable child bridge, and engine access unchanged.
- Pin the internal `OrcaCore` members consumed by `OrcaCore.Dag` to the five authoring value families (`Validation<T>`, `WorkflowDiagnostic`, `AuthoredLocation`, `DefinitionFingerprint`, and `WorkflowDefinitionException`) and one hashing operation owned by `DefinitionFingerprint`. A metadata guard must reject every other internal `OrcaCore` type/member reference from `OrcaCore.Dag.dll`, including runtime and child-start internals.
- Reuse `Validation<T>`'s existing deterministic diagnostic ordering and one shared `DefinitionFingerprint` canonical UTF-8/SHA-256 operation for both workflow and DAG compilation; register the approved DAG diagnostic codes in the existing `OrcaCore` catalog. Do not copy Core's sorter/hash implementation or add `Core -> Dag`.
- Reconcile the exact friend lists in `CLAUDE.md`, document 17, numbered documents 03, 08 (CP-020), and 10 (PR-005), implementation Decisions 22/solution architecture, the project technical overview, both canonical requirements, the package/friend allowlist, the Task 8.0 map's “sole product bridge” wording, and the post-gate amendment registry. Document 11 is explicitly excluded because its package-closure statements do not restrict internal friend access. The sole *DAG-to-durable runtime* bridge remains `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting`.
- Stage this as a post-gate amendment: independently approve the contract, then synchronize both canonical blocks and move the registry to an approved-pending stage in one frozen review target without falsely claiming final evidence. Review the metadata guard and only then resume Task 8.2 product source. Final implementation evidence, refreeze, and independent approval close the registry record.
- First checkpoint a process-only, proposed-state successor registration for the two exact requirement headings. The existing sole-active-owner guards currently reject these full `MODIFIED` deltas alongside reshape's earlier deltas; the narrow exception is a prerequisite to reviewing this amendment, not approval of its contract or source.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `developer-facing-surface`: add the narrowly used DAG authoring friend to the exact internal-friend boundary and require member-level enforcement.
- `repository-foundation`: add the same edge to the exhaustive package/friend graph without a new package reference or runtime bridge.

## Impact

Planning and verification: this change's `MODIFIED` deltas, `CLAUDE.md`, numbered documents 03, 08, 10, and 17, implementation documents `00-stack-decisions.md` and `01-solution-architecture.md`, `docs/project-technical-overview.md`, the Task 8.0 map, the Task 7.3 documentation pins, the post-gate amendment record, and exact friend/metadata guards. Document 11 is reviewed but excluded for the stated no-conflict reason. After independent amendment approval only: `OrcaCore`'s assembly friend declaration, shared internal diagnostic/fingerprint construction, `OrcaCore.Dag` Task 8.2 authoring implementation, and affected exact baselines. No public constructor, factory, package dependency, child-start interface, or engine friend is approved by this proposal.
