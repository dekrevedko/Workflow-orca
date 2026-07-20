## MODIFIED Requirements

### Requirement: Repository has explicit solution topology
The repository SHALL organize source and tests around the exact first-release package/project IDs `OrcaCore`, `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions`, `OrcaCore.Engine.Durable`, `OrcaCore.Durable.Hosting`, `OrcaCore.Providers.InMemory`, `OrcaCore.Providers.PostgreSql`, `OrcaCore.Dag`, and `OrcaCore.Dag.Hosting`, plus outward-only companion applications and internal test support, with root `OrcaCore.slnx` tying the workspace together. `OrcaCore` SHALL be the primary application contracts/authoring package, not a dependency-only meta-package.

#### Scenario: Fresh repository is reconstructed
- **WHEN** the repository is recreated from the specification baseline
- **THEN** every manifest package has one matching identifiable owner project/assembly and no second project claims the same public extension type

### Requirement: Dependency direction remains one-way
The allowed direct OrcaCore package references SHALL be exhaustive: `OrcaCore.Core -> OrcaCore`; `OrcaCore.Engine.Ephemeral -> OrcaCore + OrcaCore.Core`; `OrcaCore.Runtime.Protocol -> OrcaCore`; `OrcaCore.Provider.Abstractions -> OrcaCore + OrcaCore.Runtime.Protocol`; `OrcaCore.Engine.Durable -> OrcaCore + OrcaCore.Core + OrcaCore.Runtime.Protocol + OrcaCore.Provider.Abstractions`; `OrcaCore.Durable.Hosting -> OrcaCore + OrcaCore.Engine.Durable + OrcaCore.Provider.Abstractions`; each provider adapter -> `OrcaCore + OrcaCore.Runtime.Protocol + OrcaCore.Provider.Abstractions`; `OrcaCore.Dag -> OrcaCore`; and `OrcaCore.Dag.Hosting -> OrcaCore.Dag + OrcaCore.Durable.Hosting`. `OrcaCore.Dag.Hosting` SHALL be the only DAG-to-durable product bridge and SHALL consume one named versioned internal child-start/join contract through `InternalsVisibleTo("OrcaCore.Dag.Hosting")`, never a public child API. The sole test-only friend SHALL be `OrcaCore.Engine.Durable -> InternalsVisibleTo("OrcaCore.ProviderCertification")` for exactly the four resource-governance post-commit barrier types; it SHALL create no product-package dependency or general internal access. No package SHALL reference an engine implementation except the owning hosting package, and no OrcaCore package SHALL reference a companion integration. Architecture checks SHALL reject every other edge or friend assembly.

#### Scenario: New project reference is added
- **WHEN** a contributor wires a new project dependency
- **THEN** verification rejects any direct edge absent from the exhaustive manifest, including reverse application, protocol-to-provider, provider-to-engine, core-to-advanced, DAG reverse, and companion-integration edges

#### Scenario: Provider author persists protocol facts
- **WHEN** a provider fixture references provider abstractions and their declared runtime-protocol dependency
- **THEN** it implements persistence ports without referencing either engine implementation

#### Scenario: Companion scheduler is in the solution
- **WHEN** scheduler code references Kubernetes or AWS SDK packages
- **THEN** those dependencies remain entirely in outward-dependent companion projects and are absent from OrcaCore package closures

#### Scenario: DAG host starts durable children
- **WHEN** `OrcaCore.Dag.Hosting` coordinates a DAG node child
- **THEN** it uses the single versioned friend bridge from `OrcaCore.Durable.Hosting` and no application, provider, or additional friend package gains child-management access

## ADDED Requirements

### Requirement: Documented application packages are sufficient
The repository SHALL maintain clean `PackageReference`-only consumers for the minimal ephemeral engine, PostgreSQL-backed durable engine, callback-only durable ingress, development/test in-memory durable provider, `OrcaCore.Dag` plus `OrcaCore.Dag.Hosting`, primary `OrcaCore` application package, provider author/custom host, and outward-only Kubernetes scheduler paths. Consumers SHALL NOT use `ProjectReference`, source inclusions, repository-relative binary references, or undeclared package IDs.

#### Scenario: Durable consumer project builds
- **WHEN** a fresh consumer references `OrcaCore`, `OrcaCore.Durable.Hosting`, and `OrcaCore.Providers.PostgreSql`
- **THEN** it can author, register, start, signal, time-bound, and inspect a typed workflow without source references, direct advanced-package references, or advanced types in application signatures

#### Scenario: DAG consumer project builds
- **WHEN** a fresh durable consumer additionally references `OrcaCore.Dag` and `OrcaCore.Dag.Hosting`
- **THEN** it can build a typed DAG from durable workflow references without direct provider-authoring/runtime-protocol references or any Kubernetes/AWS package

#### Scenario: Callback-only consumer builds
- **WHEN** a clean host references `OrcaCore`, `OrcaCore.Durable.Hosting`, and `OrcaCore.Providers.PostgreSql` and selects only `AddOrcaCoreDurableEventIngress` plus the durable provider
- **THEN** durable event persistence and continuation handoff compile without registering a definition registry, execution engine, or DAG coordinator

### Requirement: Package topology and ownership are exact
Every manifest project SHALL pack under its exact project/package ID. Public namespaces and assembly owners SHALL match document 17's namespace/assembly table: ordinary workflow contracts in `OrcaCore`; DAG contracts in `OrcaCore.Dag`; shared and engine host configuration in `OrcaCore.Hosting` with the listed assembly owners; DAG hosting in `OrcaCore.Dag.Hosting`; durable management interfaces in `OrcaCore.Hosting.ResourceLeases`; their status/snapshot/result values plus governance records in `OrcaCore.Runtime.Protocol.ResourceGovernance`; the provider store in `OrcaCore.Provider.Abstractions.ResourceGovernance`; and the four barrier types internally in `OrcaCore.Engine.Durable.ResourceGovernance`. `OrcaCore.Engine.Ephemeral` SHALL own the public ephemeral hosting extension class and options; `OrcaCore.Durable.Hosting` SHALL own the public durable engine/ingress extension class, durable host options, and advanced durable management/recovery facade; `OrcaCore.Providers.InMemory` SHALL own the development/test provider extension; `OrcaCore.Providers.PostgreSql` SHALL own the production provider extension/options and implement one complete certified production durable role set; `OrcaCore.Dag` SHALL own typed DAG authoring without visualization; and `OrcaCore.Dag.Hosting` SHALL own DAG host options, registration, coordinator, and the sole durable product friend bridge. A public extension class SHALL have exactly one assembly owner and SHALL NOT be partial or duplicated across packages.

#### Scenario: Package graph is inspected
- **WHEN** packed artifacts are restored into clean consumer fixtures
- **THEN** package IDs, assembly owners, direct dependencies, extension classes, and optional closures match the exhaustive manifest and no advanced, DAG, provider-native, Kubernetes, AWS, or scheduler type leaks into `OrcaCore` public signatures

#### Scenario: Hosting extensions are reflected
- **WHEN** package baselines inspect `OrcaCore.Engine.Ephemeral` and `OrcaCore.Durable.Hosting`
- **THEN** the ephemeral and durable methods belong to distinct concrete extension classes in their owning assemblies and no cross-assembly partial type or catch-all registration exists

#### Scenario: PostgreSQL package is restored
- **WHEN** a production durable consumer references `OrcaCore.Providers.PostgreSql`
- **THEN** it receives `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)` as one complete certified role set with required nonblank `ConnectionString` and `Schema`

### Requirement: Phase 0 package feed is deterministic
Phase 0 SHALL pack every manifest package to repository-local feed `artifacts/phase0-packages` with exact test version `0.0.0-phase0`. Every clean consumer SHALL restore those exact package IDs/version through `PackageReference` only. The feed/version convention is verification-only and SHALL NOT imply an external publication, signing, SourceLink, or release-version commitment.

#### Scenario: Phase 0 consumer restores packages
- **WHEN** a clean fixture restores from the repository-local feed
- **THEN** every OrcaCore dependency resolves at `0.0.0-phase0`, no source project is referenced, and the resolved transitive closure is recorded for architecture assertions

#### Scenario: Undeclared local artifact is used
- **WHEN** a fixture attempts a project reference, loose DLL, different OrcaCore version, or package ID outside the manifest
- **THEN** package verification fails rather than silently testing a different dependency graph

### Requirement: Infrastructure integrations are separate projects
Kubernetes Job, EKS, AWS, scheduler, cluster, credential, manifest, watcher, and external-result types SHALL live in separate companion projects even when those projects remain in the same solution for v1. Standard Kubernetes access SHALL NOT force an AWS dependency.

#### Scenario: Pure Kubernetes integration is restored
- **WHEN** a companion integration talks directly to the Kubernetes API
- **THEN** it restores only the selected Kubernetes client and OrcaCore application dependencies, without an AWS SDK unless a concrete AWS capability is used

#### Scenario: Companion scheduler is packaged
- **WHEN** the outward companion references Kubernetes or optional AWS SDKs
- **THEN** it depends on `OrcaCore` and only its selected hosting/provider/DAG packages, while no OrcaCore project or package has a reverse dependency on the companion

### Requirement: Optional integrations are isolated
OpenTelemetry SDK/exporter registration SHALL be host-owned and absent from the exact first-release package manifest; product packages SHALL emit only through BCL diagnostics. Provider-native clients and Kubernetes/AWS/scheduler dependencies SHALL live in their exact provider or outward-only companion owner and SHALL NOT be pulled into `OrcaCore`, engines, hosting, or DAG packages unless that manifest entry owns them. In particular, PostgreSQL-native dependencies SHALL remain in `OrcaCore.Providers.PostgreSql`, and Kubernetes/AWS dependencies SHALL remain in the companion.

#### Scenario: Minimal ephemeral application restores dependencies
- **WHEN** a consumer restores the documented minimal ephemeral project
- **THEN** durable drivers, messaging clients, optional exporters, DAG, Kubernetes, AWS, and scheduler packages are absent from its dependency closure

### Requirement: Executable compiler IR remains implementation only
Compiled workflow plans, instructions, scopes, branches, policies, compiler indexes, internal DAG child commands, and engine accessors SHALL remain implementation details. Cross-assembly consumption SHALL use internal/friend boundaries or an implementation-only project and SHALL NOT make executable types transitive application contracts.

#### Scenario: Application package graph is packed
- **WHEN** authoring/core, engine, and DAG packages are packed for clean consumers
- **THEN** typed definitions and DAG plans compile/register without exposing executable compiled-plan or internal child-protocol types
