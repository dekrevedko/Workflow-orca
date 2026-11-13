## Why

An application or project should be able to use the complete OrcaCore durable runtime with a local database and no separately operated database server. SQLite is a suitable candidate for this deployment when one active application host owns the database, provided it preserves the same persistence, messaging, fencing, and recovery guarantees as the existing durable providers.

## What Changes

- Propose `OrcaCore.Providers.Sqlite` as a complete durable provider for one application/project, backed by a dedicated local database file and one active provider owner. Multiple workflows, internal workers, and resource-governance participants remain supported within that host.
- Propose the provider-owned `AddOrcaCoreSqliteDurableProvider(SqliteDurableProviderOptions)` registration entry and immutable, path-based options. Existing workflow authoring, durable hosting, ingress, dispatch, management, and DAG APIs remain the application surface.
- Implement every current durable provider port: event/checkpoint storage, inbox and start idempotency, outbox, projections, timers, operational statistics, maintenance, resource pools, and durable resource governance. SQLite is neither an in-memory fallback nor a reduced persistence profile.
- Use provider-native record tables, atomic SQLite transactions, exact payload storage, a bounded database worker, and process-lifetime database ownership. Add real-file certification, abrupt-process recovery, migration-journal checks, contention/failure coverage, package consumers, and measured operating limits.
- Include complete SQLite durable DAG persistence and progression parity in the final completion criteria. That phase depends on the approved implementation and certification of `admit-durable-dag-run-progression`; the existing unfinished runtime bridge is not delivered or approved by this proposal.
- Preserve the supported-mode feature matrix: SQLite supplies storage for features supported by the durable runtime, and does not introduce deferred Saga, race-join, nested fan-out, public child-workflow, or external-job authoring APIs.
- **Approval state: Proposed; implementation pending explicit approval.** Creating these artifacts, passing OpenSpec validation, or an artifact reporting `done` does not approve a contract, public API, canonical-spec synchronization, source changes, dependency updates, task activation, or publication. No implementation task is checked by this proposal.

## Capabilities

### New Capabilities

- `sqlite-durable-provider`: Full durable-provider behavior for one application/project, including exclusive host ownership, local-file lifecycle, transaction and worker semantics, schema integrity, all provider ports, supported runtime/DAG parity, recovery, maintenance, and qualification.

### Modified Capabilities

- `repository-foundation`: Admit the SQLite package and its exact dependency/assembly ownership, isolate SQLite native assets, and require clean SQLite engine, ingress, and DAG consumers.
- `developer-facing-surface`: Add the explicit SQLite provider role and path-based options while retaining complete-role composition and existing authoring/hosting boundaries.
- `durable-persistence-and-outbox`: Extend the exact provider set and first-create schema requirements to SQLite; preserve logical ownership guarantees while distinguishing SQLite's single-host deployment from server-provider multi-host operation.
- `quality-and-verification`: Require SQLite real-file certification, single-owner rejection and sequential host replacement, logical concurrency/replay coverage, fault recovery, and evidence that separates current workflow support from pending DAG support.

## Impact

- Future source owner: `src/OrcaCore.Providers.Sqlite/`; future black-box provider tests: `tests/OrcaCore.Providers.Sqlite.Tests/`. Direct OrcaCore dependencies remain `OrcaCore`, `OrcaCore.Runtime.Protocol`, and `OrcaCore.Provider.Abstractions`; no engine reference or new friend grant is proposed.
- Future dependency: a reviewed, centrally pinned `Microsoft.Data.Sqlite` package and its bundled SQLite native runtime. The actual native version/source identity and required fixes must be verified; a .NET package version alone is insufficient evidence.
- Future integration points: `OrcaCore.slnx`, `Directory.Packages.props`, provider diagnostics, API/package manifests and guards, package-only consumers, integration journeys, CI, packaging/release inventory checks, samples, and support documentation.
- Contract synchronization must reconcile `docs/specs/10-provider-model-and-extensibility.md`, `docs/specs/17-selected-mode-capability-matrix.md`, the relevant acceptance/non-functional requirements, stack decisions, the normative source map, and affected active OpenSpec changes. Historical review artifacts remain immutable.
- One provider-owned file belongs to one application/project. Parallel independent hosts, network-share databases, simultaneous standalone callback hosts sharing the file, database-per-workflow sharding, cross-provider data import, encryption, and SQLite Native AOT qualification are outside this change. Callback-only ingress remains supported as an alternative owning host and can hand off durably to a later engine host.
- Work is phased, independently reviewed, and checkpointed before the next coherent phase. Existing unrelated dirty files and the pending DAG source work remain outside this proposal's write scope. Final SQLite completion requires DAG parity; workflow-only evidence must retain its narrower label until that dependency is satisfied.
