## MODIFIED Requirements

### Requirement: Repository has explicit solution topology
The repository SHALL organize source and tests around application contracts/authoring, ephemeral and durable runtime facades, hosting integrations, provider-authoring contracts, runtime-protocol contracts, provider Adapters, and internal implementations, with a root `OrcaCore.slnx` tying the workspace together.

#### Scenario: Fresh repository is reconstructed
- **WHEN** the OrcaCore repository is recreated from the specification baseline
- **THEN** the project topology makes the application, provider-authoring, runtime-protocol, and implementation tiers identifiable from project references and public packages

### Requirement: Dependency direction remains one-way
Application contract and authoring projects SHALL NOT depend on provider-authoring, runtime-protocol, hosting, provider Adapter, or engine implementation projects. `OrcaCore.Runtime.Protocol` SHALL own commands, facts, checkpoint records, and envelope records and SHALL NOT depend on provider authoring. `OrcaCore.Provider.Abstractions` SHALL own provider ports, provider commit DTOs, and certification contracts and MAY depend on `OrcaCore.Runtime.Protocol` because providers persist protocol facts and checkpoints. Provider Adapters and runtime implementations MAY depend on the advanced packages appropriate to their role; no advanced package SHALL depend on an engine implementation. Automated architecture checks SHALL encode this complete permitted-edge list and reject every other cross-tier edge and application public-signature leak.

#### Scenario: New project reference is added
- **WHEN** a contributor wires a new project dependency
- **THEN** verification rejects reverse dependencies or application public signatures that cross into an advanced or implementation tier

#### Scenario: Provider author persists protocol facts
- **WHEN** a provider-author fixture references `OrcaCore.Provider.Abstractions` and its declared `OrcaCore.Runtime.Protocol` dependency
- **THEN** it can implement persistence ports and compile without referencing either engine implementation

#### Scenario: Protocol project is inspected
- **WHEN** the architecture guard evaluates `OrcaCore.Runtime.Protocol`
- **THEN** it rejects any reverse reference to `OrcaCore.Provider.Abstractions`

## ADDED Requirements

### Requirement: Documented application package is sufficient
The repository SHALL maintain a package-consumer project for each documented application golden path and SHALL prove that only the documented main/hosting/provider package references are required.

#### Scenario: Durable consumer project builds
- **WHEN** a fresh consumer references the documented durable hosting package and one durable store Adapter
- **THEN** it can author, register, start, signal, complete external work, and inspect a workflow without direct project references to source internals or advanced protocol packages

### Requirement: Package topology is explicit with a small application meta-package
The repository SHALL pack contracts, authoring/core, ephemeral engine, durable engine, hosting, provider-authoring, and runtime-protocol as separate packages and SHALL provide a small `OrcaCore` meta-package for the documented default application path.

#### Scenario: Package graph is inspected
- **WHEN** packed artifacts are restored into clean consumer fixtures
- **THEN** package dependencies preserve the project-tier rules and the meta-package does not make advanced types part of application signatures

### Requirement: Optional integrations are isolated
Optional observability exporters and provider-native dependencies SHALL live in focused integration projects or provider projects and SHALL NOT be pulled into base contracts, authoring, engines, or hosting unless selected.

#### Scenario: Minimal ephemeral application restores dependencies
- **WHEN** a consumer restores the documented minimal ephemeral project
- **THEN** durable provider drivers, messaging clients, and unused OpenTelemetry exporters are absent from its dependency closure

### Requirement: Executable compiler IR remains implementation-only
Compiled workflow plans, instructions, scope/branch/policy models, compiler identity indexes, and engine accessors SHALL remain implementation details even when multiple engine assemblies consume them. Cross-assembly access SHALL use internal/friend boundaries or an implementation-only project and SHALL NOT make those types transitive application contracts.

#### Scenario: Application package graph is packed
- **WHEN** the authoring/core and engine packages are packed for a clean consumer
- **THEN** application definitions compile and register without exposing executable compiled-plan types in their public signatures
