## MODIFIED Requirements

### Requirement: Implementation package boundaries use exact internal friends
Compiler, execution-kernel, concrete engine, provider, and hosted-loop implementation types SHALL remain internal even when another first-release implementation package consumes them. Cross-assembly implementation access SHALL use only the exact reviewed product friends `OrcaCore` to `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, and `OrcaCore.Dag` for application-owned internal authoring/runtime contracts; `OrcaCore.Core` to `OrcaCore.Engine.Ephemeral` and `OrcaCore.Engine.Durable`; `OrcaCore.Engine.Durable` to `OrcaCore.Durable.Hosting`; and `OrcaCore.Durable.Hosting` to `OrcaCore.Dag.Hosting`. The `OrcaCore -> OrcaCore.Dag` grant SHALL be used only for the internal constructors of `Validation<T>`, `WorkflowDiagnostic`, `AuthoredLocation`, `DefinitionFingerprint`, and `WorkflowDefinitionException`, plus one internal canonical-structure hash operation owned by `DefinitionFingerprint`; the exact signatures SHALL be pinned. A compiled-metadata guard SHALL reject every other non-public `OrcaCore` type or member referenced by `OrcaCore.Dag.dll`, including runtime, child-start, engine, hosting, and protocol internals. DAG diagnostics SHALL use the existing `WorkflowDiagnosticCatalog` and `Validation<T>` ordering, and DAG fingerprints SHALL use the same internal UTF-8/SHA-256 operation as workflow compilation without changing existing workflow hashes. The new grant SHALL NOT add a package reference or give `OrcaCore.Dag` durable child-start access; `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` remains the sole DAG-to-durable runtime bridge. Exact owning white-box test friends and the durable provider-certification barrier friend MAY inspect internals; acceptance, behavior-scenario, compile-fixture, and integration assemblies SHALL NOT. No other friend, public reflection bridge, or exported test helper SHALL exist. Friend grants SHALL NOT create reverse package references.

#### Scenario: Engine consumes the shared execution kernel
- **WHEN** either engine compiles against `OrcaCore.Core`
- **THEN** it receives type-safe friend access to internal kernel types without making those types externally visible

#### Scenario: Internal application contracts cross implementation packages
- **WHEN** Core or an engine constructs or reads an application-owned internal authoring/runtime contract
- **THEN** it uses compile-checked `OrcaCore` friend access and no reflection, CLR-name lookup, or public compatibility bridge

#### Scenario: Consumer inspects an implementation package
- **WHEN** a fresh-package consumer or exported-API baseline inspects Core, an engine, hosting, or a provider package
- **THEN** only the documented role-specific surface is exported and no compiler IR, concrete loop/store, observer hook, test helper, or reflection bridge is visible

#### Scenario: New friend assembly is proposed
- **WHEN** compiled metadata contains a friend outside the exact product, owning-test, and provider-certification allowlist
- **THEN** the guard fails until a reviewed contract amendment authorizes that boundary

#### Scenario: DAG author constructs compiler-owned values
- **WHEN** `OrcaCore.Dag` builds a typed plan or returns ordered build diagnostics
- **THEN** it constructs only the six allowlisted internal member signatures on the five named authoring value families, reuses the shared catalog, sorter, and canonical hash, and exposes no new public construction path

#### Scenario: DAG references another application internal
- **WHEN** compiled `OrcaCore.Dag.dll` references any other non-public `OrcaCore` type, member, or overload
- **THEN** the compiled-metadata guard fails even though the CLR friend grant would permit the reference
