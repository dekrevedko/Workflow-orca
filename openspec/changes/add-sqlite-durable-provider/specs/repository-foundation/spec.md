## MODIFIED Requirements

### Requirement: Repository has explicit solution topology
The repository SHALL organize source and tests around the exact first-release package/project IDs `OrcaCore`, `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions`, `OrcaCore.Engine.Durable`, `OrcaCore.Durable.Hosting`, `OrcaCore.Providers.InMemory`, `OrcaCore.Providers.PostgreSql`, `OrcaCore.Providers.SqlServer`, `OrcaCore.Providers.Sqlite`, `OrcaCore.Dag`, and `OrcaCore.Dag.Hosting`, plus outward-only companion applications and internal test support, with root `OrcaCore.slnx` tying the workspace together. `OrcaCore` SHALL be the primary application contracts/authoring package, not a dependency-only meta-package.

#### Scenario: Fresh repository is reconstructed
- **WHEN** the repository is recreated from the specification baseline
- **THEN** every manifest package has one matching identifiable owner project/assembly and no second project claims the same public extension type

### Requirement: Documented application packages are sufficient
The repository SHALL maintain clean `PackageReference`-only consumers for the minimal ephemeral engine, PostgreSQL-backed durable engine, SQL Server-backed durable engine, SQLite-backed single-application durable engine, callback-only durable ingress through each production provider, application-supplied durable event dispatcher, development/test in-memory durable provider, `OrcaCore.Dag` plus `OrcaCore.Dag.Hosting`, primary `OrcaCore` application package, provider author/custom host, and outward-only Kubernetes scheduler paths. Consumers SHALL NOT use `ProjectReference`, source inclusions, repository-relative binary references, or undeclared package IDs.

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

#### Scenario: SQLite durable consumer runs after reopening its file
- **WHEN** a fresh PackageReference-only consumer references `OrcaCore`, `OrcaCore.Durable.Hosting`, and `OrcaCore.Providers.Sqlite` and selects one local database
- **THEN** the typed durable application journey, self-routing ingress, application dispatcher, timers, queries, and restart recovery execute without source references, direct advanced-package references, or PostgreSQL/SQL Server native dependencies

#### Scenario: SQLite callback-only ownership is handed off
- **WHEN** a clean SQLite callback-only consumer persists an event/start intent, stops and disposes its host, and an engine consumer later takes ownership of the same file
- **THEN** the engine progresses the original durable ownership without concurrent file-sharing hosts or a workflow registry inside the callback-only consumer

#### Scenario: SQLite DAG package consumer is qualified
- **WHEN** final SQLite feature support is requested and the approved shared durable DAG implementation is complete
- **THEN** a clean consumer additionally referencing `OrcaCore.Dag` and `OrcaCore.Dag.Hosting` executes and recovers the complete DAG journey on the SQLite file without direct provider-authoring/runtime-protocol references or additional friends

### Requirement: Package topology and ownership are exact
Every manifest project SHALL pack under its exact project/package ID. Public namespaces and assembly owners SHALL match document 17's namespace/assembly table: ordinary workflow definitions/references, event contract descriptors/routes/envelopes, inbound acceptance results/rejections, and durable authoring in `OrcaCore`; DAG contracts in `OrcaCore.Dag`; shared and engine host configuration in `OrcaCore.Hosting` with the listed assembly owners; DAG hosting in `OrcaCore.Dag.Hosting`; durable management interfaces in `OrcaCore.Hosting.ResourceLeases`; their status/snapshot/result values plus governance records in `OrcaCore.Runtime.Protocol.ResourceGovernance`; serialized route/inbox/outbox records in `OrcaCore.Runtime.Protocol`; provider stores/raw commit records in `OrcaCore.Provider.Abstractions`; and the four resource-governance barrier types internally in `OrcaCore.Engine.Durable.ResourceGovernance`. `OrcaCore.Engine.Ephemeral` SHALL own the public ephemeral hosting extension/options and mode-specific workflow composition builder; `OrcaCore.Durable.Hosting` SHALL own the public durable engine/ingress extension, durable workflow composition builder, `IWorkflowEventIngress`, application-shaped `IWorkflowEventDispatcher`, `WorkflowEventDispatchResult`, `WorkflowEventDispatchFailure`, durable host options, hosted pumps, and advanced durable management/recovery facade; `OrcaCore.Providers.InMemory` SHALL own the development/test provider extension; `OrcaCore.Providers.PostgreSql`, `OrcaCore.Providers.SqlServer`, and `OrcaCore.Providers.Sqlite` SHALL each own its production provider extension/options and implement one complete independently certified production durable role set within its approved deployment scope; `OrcaCore.Dag` SHALL own typed DAG authoring without visualization; and `OrcaCore.Dag.Hosting` SHALL own DAG host options, registration, coordinator, and the sole durable product friend bridge. A public extension class SHALL have exactly one assembly owner and SHALL NOT be partial or duplicated across packages.

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
- **WHEN** a production durable consumer references `OrcaCore.Providers.SqlServer`
- **THEN** it receives `AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)` as one complete certified role set with required nonblank `ConnectionString` and `Schema`

#### Scenario: SQLite package is restored
- **WHEN** a single-application durable consumer references `OrcaCore.Providers.Sqlite`
- **THEN** it receives the provider-owned `AddOrcaCoreSqliteDurableProvider(SqliteDurableProviderOptions)` entry and immutable `DatabasePath` profile as one complete certified role without a schema-name option, engine dependency, or new friend grant

### Requirement: Optional integrations are isolated
OpenTelemetry SDK/exporter registration SHALL be host-owned and absent from the exact first-release package manifest; product packages SHALL emit only through BCL diagnostics. Provider-native clients, broker SDKs, and Kubernetes/AWS/scheduler dependencies SHALL live in their exact provider, application adapter, or outward-only companion owner and SHALL NOT be pulled into `OrcaCore`, engines, hosting, or DAG packages unless that manifest entry owns them. In particular, PostgreSQL-native dependencies SHALL remain in `OrcaCore.Providers.PostgreSql`, `Microsoft.Data.SqlClient` SHALL remain in `OrcaCore.Providers.SqlServer`, `Microsoft.Data.Sqlite` and its bundled SQLite native assets SHALL remain in `OrcaCore.Providers.Sqlite` and its selected consumer closure, `Testcontainers.MsSql` SHALL remain test-only, MassTransit/Rebus/AWS messaging dependencies SHALL remain in application adapters, and Kubernetes/AWS scheduler dependencies SHALL remain in the companion.

#### Scenario: Minimal ephemeral application restores dependencies
- **WHEN** a consumer restores the documented minimal ephemeral project
- **THEN** durable drivers, messaging clients, optional exporters, DAG, Kubernetes, AWS, and scheduler packages are absent from its dependency closure

#### Scenario: An application does not select SQLite
- **WHEN** minimal ephemeral or another provider's documented package-only consumer restores its dependencies
- **THEN** Microsoft.Data.Sqlite, SQLitePCLRaw, and SQLite native assets are absent from that consumer's closure
