## MODIFIED Requirements

### Requirement: Documented application packages are sufficient
The repository SHALL maintain clean `PackageReference`-only consumers for the minimal ephemeral engine, PostgreSQL-backed durable engine, SQL Server-backed durable engine, callback-only durable ingress through each shipped relational provider, application-supplied durable event dispatcher, development/test in-memory durable provider, `OrcaCore.Dag` plus `OrcaCore.Dag.Hosting`, primary `OrcaCore` application package, provider author/custom host, and outward-only Kubernetes scheduler paths. Consumers SHALL NOT use `ProjectReference`, source inclusions, repository-relative binary references, or undeclared package IDs.

#### Scenario: Durable consumer project builds
- **WHEN** a fresh consumer references `OrcaCore`, `OrcaCore.Durable.Hosting`, and `OrcaCore.Providers.PostgreSql`
- **THEN** it can author a static definition, stage it through the durable engine builder, resolve its typed reference, start/reopen it, accept a self-routing event, durably publish through an application dispatcher, time-bound it, and inspect it without source references, direct advanced-package references, or advanced types in application signatures

#### Scenario: SQL Server durable consumer project builds
- **WHEN** a fresh consumer references `OrcaCore`, `OrcaCore.Durable.Hosting`, and `OrcaCore.Providers.SqlServer`
- **THEN** it receives the same typed durable application path through the SQL Server role without source references, direct advanced-package references, PostgreSQL dependencies, or advanced types in application signatures

#### Scenario: DAG consumer project builds
- **WHEN** a fresh durable consumer additionally references `OrcaCore.Dag` and `OrcaCore.Dag.Hosting`
- **THEN** it can build a typed DAG from durable workflow references without direct provider-authoring/runtime-protocol references or any Kubernetes/AWS package

#### Scenario: Callback-only consumer builds
- **WHEN** a clean host references `OrcaCore`, `OrcaCore.Durable.Hosting`, and `OrcaCore.Providers.PostgreSql` and selects only `AddOrcaCoreDurableEventIngress` plus the durable provider
- **THEN** durable self-routing event persistence and continuation handoff compile without a workflow composition builder, definition registry, execution engine, external outbox dispatcher, or DAG coordinator

#### Scenario: Broker adapter consumer builds
- **WHEN** a clean application references its selected MassTransit, Rebus, SNS/SQS, RabbitMQ, or other broker SDK plus documented OrcaCore application/hosting packages
- **THEN** it can implement `IWorkflowEventDispatcher` and call `IWorkflowEventIngress` without referencing provider abstractions or runtime protocol

### Requirement: Package topology and ownership are exact
Every manifest project SHALL pack under its exact project/package ID. Public namespaces and assembly owners SHALL match document 17's namespace/assembly table: ordinary workflow definitions/references, event contract descriptors/routes/envelopes, inbound acceptance results/rejections, and durable authoring in `OrcaCore`; DAG contracts in `OrcaCore.Dag`; shared and engine host configuration in `OrcaCore.Hosting` with the listed assembly owners; DAG hosting in `OrcaCore.Dag.Hosting`; durable management interfaces in `OrcaCore.Hosting.ResourceLeases`; their status/snapshot/result values plus governance records in `OrcaCore.Runtime.Protocol.ResourceGovernance`; serialized route/inbox/outbox records in `OrcaCore.Runtime.Protocol`; provider stores/raw commit records in `OrcaCore.Provider.Abstractions`; and the four resource-governance barrier types internally in `OrcaCore.Engine.Durable.ResourceGovernance`. `OrcaCore.Engine.Ephemeral` SHALL own the public ephemeral hosting extension/options and mode-specific workflow composition builder; `OrcaCore.Durable.Hosting` SHALL own the public durable engine/ingress extension, durable workflow composition builder, `IWorkflowEventIngress`, application-shaped `IWorkflowEventDispatcher`, `WorkflowEventDispatchResult`, `WorkflowEventDispatchFailure`, durable host options, hosted pumps, and advanced durable management/recovery facade; `OrcaCore.Providers.InMemory` SHALL own the development/test provider extension; `OrcaCore.Providers.PostgreSql` and `OrcaCore.Providers.SqlServer` SHALL each own its provider extension/options and implement one complete independently certified durable role set (production PostgreSQL, experimental SQL Server); `OrcaCore.Dag` SHALL own typed DAG authoring without visualization; and `OrcaCore.Dag.Hosting` SHALL own DAG host options, registration, coordinator, and the sole durable product friend bridge. A public extension class SHALL have exactly one assembly owner and SHALL NOT be partial or duplicated across packages.

#### Scenario: Package graph is inspected
- **WHEN** packed artifacts are restored into clean consumer fixtures
- **THEN** package IDs, assembly owners, direct dependencies, extension classes, and optional closures match the exhaustive manifest and no advanced, DAG, provider-native, Kubernetes, AWS, or scheduler type leaks into `OrcaCore` public signatures

#### Scenario: Hosting extensions are reflected
- **WHEN** package baselines inspect `OrcaCore.Engine.Ephemeral` and `OrcaCore.Durable.Hosting`
- **THEN** the ephemeral and durable role methods/builders belong to distinct concrete owners, each builder accepts only its mode's definitions, and no cross-assembly partial type, catch-all registration, or discovery scanner exists

#### Scenario: PostgreSQL package is restored
- **WHEN** a production durable consumer references `OrcaCore.Providers.PostgreSql`
- **THEN** it receives `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)` as one complete certified role set with required nonblank `ConnectionString` and `Schema`

#### Scenario: SQL Server package is restored
- **WHEN** a durable consumer references the experimental `OrcaCore.Providers.SqlServer`
- **THEN** it receives `AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)` as one complete certified role set with required nonblank `ConnectionString` and `Schema`
