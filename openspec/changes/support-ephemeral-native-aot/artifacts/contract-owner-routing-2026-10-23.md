# Issue #13 normative and source routing

Contract-only target; all listed transitions are owned by the later task 2 target.
Seven additions supplement existing rules; none replaces the certified codec or
the historical assessment. The metadata method changes acquisition only, so
the fixed-codec and no-replacement-hook requirements remain in force unchanged.

| Owner | Required later transition |
|---|---|
| openspec/specs/workflow-contracts/spec.md | Append two blocks for generated metadata parity and missing-metadata failures. Preserve existing codec rules, unions and fingerprint composition. |
| openspec/specs/developer-facing-surface/spec.md | Append two blocks for exact API/process seal and static bootstrap/ownership. |
| openspec/specs/structured-fiber-execution/spec.md | Append one closed typed dispatch block, preserving all composition/failure/commit semantics. |
| openspec/specs/quality-and-verification/spec.md | Append two native support/diagnostic gates. Retain four earlier assessment requirements; support and assessment are distinct protocols. |
| docs/specs/03-domain-model-and-glossary.md | Metadata prerequisites, process registration/seal vocabulary and early missing-contract diagnostic. |
| docs/specs/04-requirements-core-runtime.md | Static closed dispatch and unchanged ownership semantics. |
| docs/specs/10-provider-model-and-extensibility.md | Clarify metadata acquisition under PR-016, no alternate serializer/ordinary replacement hook, same selected closure and friend boundary. |
| docs/specs/11-non-functional-requirements.md | Add NF-034 for the finite qualified support boundary; preserve NF-033 assessment history. |
| docs/specs/12-acceptance-criteria.md | Add AC-563–574 aligned one-to-one with NS-01–12; retain AC-555–562 as original assessment requirements. |
| docs/specs/13-phasing-and-open-questions.md | New issue #13 gate and actual source/native status, no implicit broader-engine support. |
| docs/specs/17-selected-mode-capability-matrix.md | Add the exact new signature, startup order, finite native type/RID boundary and status; guard the signature, including TerminalRetention and shutdown entries already present. |
| docs/specs/17-public-authoring-contract.cs | Add only the exact one-type/one-method catalogue when source ships; preserve all other declarations. |
| docs/normative-source-map.md | Crosswalk the seven blocks to numbered owners, declaration/API baseline and NS acceptance IDs. |
| README.md; docs/ephemeral-engine-developer-guide.md | Exact role/metadata/authoring recipe, one process context and seal, default-disabled JSON, finite closure and failure remedy. Keep implementation pending until reviewed native proof. |
| docs/production-readiness.md; docs/project-technical-overview.md | New dated qualified source/package/profile only; unchanged historical unsupported release results and no broad provider/DAG badge. |
| docs/packages.md; docs/github-publishing-status.md; docs/nuget-publishing.md | Distinguish existing preview.2 from new qualified candidate/release; retain registry authentication and exact package provenance. |
| tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.api.txt; Fixtures/v1-public-contract.json; Fixtures/package-consumer-fixtures.json; PackageFixtures/ | Reconcile actual API, declaration/compile and external consumer fixture owners together with source. No baseline-only advance. Guard doc 17 metadata method and existing public signature entries. |
| tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json and owning validator | Add exact approval binding/stage under task 2, preserving every old record and source approval boundary. New change has no activation record in this contract. |
| tests/OrcaCore.DeveloperSurface.Guards/DagContractGuards.cs and NormativeContractGuards.cs | Review any inherited source-freeze exemption needed for the selected Core/Abstractions/Ephemeral edits explicitly, scope it to this approved contract and exact source files, and retain unrelated DAG/public boundaries. Do not replace an exact guard with a broad source exemption. |

At task 2, inspect every occurrence of AOT/trimming/fixed codec/bootstrap/source
generation across active docs individually against its actual owner and version.
Do not stamp a template disposition or edit archive/legacy material. Record edited
and retained owners compactly; update any newly discovered affected owner explicitly.
The existing issue #1 change archival and Core lane consolidation are separate.

## Product implementation audit after approval

- OrcaCore.Abstractions: fixed codec (including Core-linked copy), failure/strong
  value converters, input/event/fingerprint paths, typed-authoring boundary and
  exact new public class. Shared authoritative metadata state uses existing friends.
- Core: DefinitionCompilerOptions.cs:58, TypedAuthoringOperations/bootstrap,
  StructuredInvocationCache, StepResultWaitAccessor, ForEachSnapshotMaterializer,
  structured branch builders and FailureProvenance; statically close all callback,
  snapshot/context and collection/failure operations.
- Ephemeral: definition catalog/staging, RegisterCore/start, queries/event router,
  FacadeValueFactory, engine value construction and later runtime paths. Preserve
  all role options, dependencies, start admission, retention and host lifetime.
- Tests/consumer/automation: add new protocol and diagnostic/golden tests; retain
  original AOT-01–16 assertions and immutable raw assessment archives. No product
  fix is present in this contract; test runner migration is excluded.
