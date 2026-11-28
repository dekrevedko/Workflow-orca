## MODIFIED Requirements

### Requirement: Hosting registration selects execution mode explicitly
Microsoft hosting SHALL expose exactly `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`, `AddOrcaCoreDurableEngine(DurableEngineHostOptions)`, callback-only `AddOrcaCoreDurableEventIngress()`, development/test `AddOrcaCoreInMemoryDurableProvider()`, production `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`, experimental `AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)`, and `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)` as role-selection entries. The engine entries SHALL return `OrcaCoreEphemeralEngineBuilder` and `OrcaCoreDurableEngineBuilder`; each builder's two resultless/resultful `AddWorkflow` overloads SHALL accept only already-built definitions for that mode and return the same builder for composition. `OrcaCore.Engine.Ephemeral` SHALL own `OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions` and the ephemeral builder; `OrcaCore.Durable.Hosting` SHALL own `OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions`, the durable builder, `IWorkflowEventIngress`, and `IWorkflowEventDispatcher`; provider and DAG extensions SHALL remain in their owning package-specific classes. No public extension class SHALL be split across assemblies. Registration SHALL validate programmatically constructed typed options immediately and freeze an immutable owned copy, SHALL be idempotent only for the same role/options, and SHALL reject conflicting duplicates. Before readiness or any progression loop starts, the engine SHALL preflight the complete staged workflow batch and install it atomically; exact duplicate identity/version/fingerprint entries SHALL be idempotent and any incompatibility/conflict SHALL leave the registry unchanged and start no loop. Host options SHALL NOT claim direct `IConfiguration` binder compatibility and v1 SHALL expose no public binder DTO, converter, configuration-section overload, catch-all `AddOrcaCore`, separate hosted-service toggle, implicit mode selection, serializer replacement hook, workflow/event assembly scan, or active superseded hosting scenario.

#### Scenario: Ephemeral host is configured
- **WHEN** a developer selects ephemeral hosting
- **THEN** the returned builder stages only ephemeral definitions and the role registers the common definition registry, execution services, and configured transient-pool catalog while durable ingress, durable publish/outbox, store, protocol worker, and reconciliation services are absent

#### Scenario: Durable host is configured
- **WHEN** a developer selects durable hosting with one complete certified provider role set
- **THEN** the returned builder stages only durable definitions and the role registers the durable definition registry, self-routing ingress, pending-inbox matching, progression loops, and optional configured external-event outbox dispatch

#### Scenario: Both engines are selected in one service provider
- **WHEN** one `IServiceProvider` is configured with both ephemeral and durable engine roles
- **THEN** registration/startup fails deterministically instead of selecting a mode implicitly or exposing ambiguous registry/event ownership

#### Scenario: Definition names pools absent from the host
- **WHEN** an ephemeral definition names one or more unconfigured transient pools or a durable definition contains one or more unconfigured static lease requests
- **THEN** registration returns the complete sorted `HostIncompatible` missing-pool failure before mutation; selector-created durable names remain runtime validation

#### Scenario: Application module stages workflows
- **WHEN** application configuration calls `AddWorkflow` directly or through an application-owned grouping extension
- **THEN** the exact immutable definitions are explicit in the composition root and no reflection, attribute discovery, registrar class, or environment-dependent graph construction occurs

#### Scenario: Staged definition batch conflicts
- **WHEN** startup preflight finds a wrong-mode definition, missing static host capability, or the same identity/version with another fingerprint
- **THEN** no staged definition is installed and no worker, timer, continuation, inbox, outbox, or DAG loop starts

#### Scenario: In-memory durable host is configured
- **WHEN** a developer selects the durable engine plus `AddOrcaCoreInMemoryDurableProvider`
- **THEN** startup and documentation identify it as development/test-only with no restart-durability claim

#### Scenario: Callback-only ingress is configured
- **WHEN** a definition-less callback host calls `AddOrcaCoreDurableEventIngress` with one complete certified durable provider role set
- **THEN** it receives durable self-routing event and pending-start-intent persistence plus continuation handoff, but no workflow builder, definition registry, reference lookup, execution worker, timer/reconciliation loop, external outbox dispatcher, or DAG coordinator

#### Scenario: Durable ingress is redundantly composed with an engine
- **WHEN** callback-only durable ingress and an engine role are registered in one service provider
- **THEN** startup rejects the conflicting ownership because the durable engine already owns durable ingress and the callback role is definition-less

#### Scenario: DAG hosting is configured
- **WHEN** a durable-engine host calls `AddOrcaCoreDag`
- **THEN** only the DAG coordinator and registry are added through `OrcaCore.Dag.Hosting`, and absence of the durable-engine role fails startup

#### Scenario: Superseded hosting composition is scanned
- **WHEN** active documentation, fixtures, samples, or tests are inspected
- **THEN** none invokes catch-all `AddOrcaCore`, `AddOrcaCoreHostedServices`, a combined engine role, or another superseded registration path

### Requirement: Provider registration names the adapter role
Every provider package SHALL identify whether it supplies a complete durable role set, projection cache, dispatcher, or another documented role and SHALL follow common validation, ownership, replacement, diagnostics, and certification conventions within that role. `OrcaCore.Providers.PostgreSql` and `OrcaCore.Providers.SqlServer` SHALL each supply one complete independently certified durable role set. PostgreSQL is the production target and SQL Server is experimental. PostgreSQL SHALL own `OrcaCore.Providers.PostgreSql.OrcaCorePostgreSqlProviderServiceCollectionExtensions.AddOrcaCorePostgreSqlDurableProvider(IServiceCollection, PostgreSqlDurableProviderOptions)`; SQL Server SHALL own `OrcaCore.Providers.SqlServer.OrcaCoreSqlServerProviderServiceCollectionExtensions.AddOrcaCoreSqlServerDurableProvider(IServiceCollection, SqlServerDurableProviderOptions)`. Each options type SHALL expose only get-only `ConnectionString` and `Schema` fixed by programmatic construction; registration SHALL copy the options and reject null, empty, or whitespace values before registering any provider service. No parallel connection-string overload, configuration-binding overload, partial-role interpretation, shared Relational facade, or cross-provider extension owner SHALL exist.

#### Scenario: PostgreSQL durable provider is registered
- **WHEN** a production host supplies nonblank connection string and schema values
- **THEN** one call registers the complete certified durable provider role set without requiring application registration of individual ports

#### Scenario: PostgreSQL provider options are invalid
- **WHEN** the options object or either required textual value is null, empty, or whitespace
- **THEN** the registration call rejects before any partial provider services are visible

#### Scenario: SQL Server durable provider is registered
- **WHEN** a host supplies nonblank SQL Server connection string and schema values
- **THEN** one call registers the complete independently certified durable provider role set without requiring application registration of individual ports

#### Scenario: SQL Server provider options are invalid
- **WHEN** the options object or either required textual value is null, empty, or whitespace
- **THEN** the registration call rejects before any partial provider services are visible
