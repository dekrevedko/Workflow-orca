# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Active implementation track

The repository root is the sole active codebase. The superseded root prototype is
preserved under `archive/legacy-poc/`. All build, test, and source work targets
the root `OrcaCore.slnx`, `src/`, and `tests/`.

The former `v3-gpt` workspace was promoted to the repository root in commit
`666bc1e6`. Do not recreate or use a parallel `v3-gpt/` implementation tree.
Preserve agent/run context directories such as `.agents/`, `.claude/`, `.codex/`,
and `.codex-run/` when they exist; they are operational context, not legacy code.

## Build and test commands

All commands run from the repository root so the SDK pin in `global.json` is respected.

```powershell
# Build everything
dotnet build OrcaCore.slnx

# Build release (CI mode — warnings are errors)
dotnet build OrcaCore.slnx -c Release -warnaserror

# Run a single unit test project
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj
dotnet test tests/OrcaCore.Acceptance.Tests/OrcaCore.Acceptance.Tests.csproj

# Run a single test by name filter
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --filter "FullyQualifiedName~MyTestClass"

# Integration gate (requires Docker via Testcontainers)
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build

# Integration smoke gate (E2E + Hosting only — fastest)
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.E2E|FullyQualifiedName~OrcaCore.Integration.Tests.Hosting"

# Run the console examples
dotnet run --project samples/OrcaCore.Examples/OrcaCore.Examples.csproj
```

Before running integration tests after an interrupted run, clear stale build workers:
```powershell
dotnet build-server shutdown
```

Integration counts and intentional skips are target-specific. Use the active OpenSpec task graph
and frozen review request for the exact expected lane; do not copy a count or blocker anchor from a
historical review. Do not delete or unskip a test unless its current named blocker is implemented,
the source is reclassified by the active ledger, and the replacement evidence is verified.

CI enforces 80% line coverage for `OrcaCore.Engine.*` assemblies.

## Architecture

### Two engines, one abstraction surface

OrcaCore provides two runtime engines that share the application authoring and step contracts in
PackageId/assembly `OrcaCore` (project directory `src/OrcaCore.Abstractions`). `OrcaCore.Core` is
their internal compiler/execution kernel and exports no application API:

| Engine | Project | Persistence | Use case |
|--------|---------|-------------|----------|
| Ephemeral | `OrcaCore.Engine.Ephemeral` | In-memory only; lost on restart | Development, testing, short-lived coordination |
| Durable | `OrcaCore.Engine.Durable` | Pluggable split provider ports (PostgreSQL and SQL Server in v1) | Production, long-running, crash-tolerant |

### Project structure (`src/`)

The approved v1 package manifest is exhaustive — twelve packages. Anything in `src/` outside this
list is provisional and slated for removal or relocation; do not build new work on it.

```
OrcaCore                   — application contracts/authoring (project dir: src/OrcaCore.Abstractions,
                             PackageId OrcaCore): IStep, StepContext<TState>, StepResult,
                             identifiers, snapshots, facades
OrcaCore.Core              — internal authoring/compiler/execution support; exports no application API
OrcaCore.Engine.Ephemeral  — in-process execution loop, timer pump; owns AddOrcaCoreEphemeralEngine
OrcaCore.Runtime.Protocol  — durable commands, committed facts, checkpoints, envelopes
OrcaCore.Provider.Abstractions  — split workflow/provider ports, IMessageDispatcher,
                             IDurableResourceGovernanceStore, commit DTOs, certification
OrcaCore.Engine.Durable    — durable aggregates, outbox, checkpoint/replay, routing
OrcaCore.Durable.Hosting   — owns AddOrcaCoreDurableEngine, AddOrcaCoreDurableEventIngress
OrcaCore.Providers.InMemory     — dev/test provider; owns AddOrcaCoreInMemoryDurableProvider
OrcaCore.Providers.PostgreSql   — production provider; owns AddOrcaCorePostgreSqlDurableProvider
OrcaCore.Providers.SqlServer    — production provider; owns AddOrcaCoreSqlServerDurableProvider
OrcaCore.Dag               — typed DAG planning/operation contracts
OrcaCore.Dag.Hosting       — sole DAG-to-durable bridge; owns AddOrcaCoreDag
```

Implementation package boundaries use an exact internal-friend allowlist so compiler/kernel,
concrete engine, provider, and hosted-loop types do not become public merely to cross assemblies.
The current seven product friends are `OrcaCore` to `OrcaCore.Core`,
`OrcaCore.Engine.Ephemeral`, and `OrcaCore.Engine.Durable`; `OrcaCore.Core` to both engines;
`OrcaCore.Engine.Durable` to `OrcaCore.Durable.Hosting`; and `OrcaCore.Durable.Hosting` to
`OrcaCore.Dag.Hosting`. Exact owning unit-test assemblies plus Durable to
ProviderCertification may inspect internals; acceptance, behavior, compile-fixture, and
integration assemblies may not. No other current friend or public reflection bridge is allowed.

The independently approved post-gate `admit-dag-authoring-friend-boundary` contract names one additional *authoring*
friend, `OrcaCore -> OrcaCore.Dag`, for compiler-created validation, diagnostics, locations,
fingerprints, and definition exceptions. It is not in the current compiled friend set and grants
no DAG runtime or child-start access. Its exact member signatures, shared fingerprint operation,
and metadata guard require separate source review before Task 8.2 product source changes.
The existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` edge remains the sole
DAG-to-durable *runtime* bridge.

Not in the v1 manifest: the removed catch-all `OrcaCore.Hosting` package and the provisional
`OrcaCore.Providers.RabbitMq`, `.Redis`, `.ZeroMq`, and `.Relational` packages. Their source/test
roots are absent. Broker SDK mapping belongs to application/companion adapters over
`IWorkflowEventDispatcher`; a durable storage provider beyond PostgreSQL and SQL Server requires a
future amendment plus complete current-port and greenfield-schema certification.

Hosting is **role-specific**. There is no catch-all `AddOrcaCore()`, no separate
`AddOrcaCoreHostedServices()` toggle, no implicit mode selection, no options-binder facade, and no
serializer/codec hook. Ephemeral and durable engine roles are mutually exclusive.

### Durable engine model

The durable engine persists workflow state as an event-sourced aggregate. On each step:
1. The engine loads the current aggregate snapshot through `IWorkflowEventStore`.
2. It applies commands and emits events (append-only).
3. A committed outbox record is dispatched via `IMessageDispatcher` (at-least-once).
4. On restart, the engine replays from the last checkpoint.

Wait residency is a runtime/hosting policy, not an authored distinction: a durable `Wait` is
cold-capable and survives host restarts without a separate authored member.

### Control flow nodes

The complete v1 node set: business steps, `Init`, `End`, `Delay`, event `Wait`, root `While`,
nested `If`, root-only fixed `Parallel`, bounded root-only `ForEach`, `WhenAll`, `WhenAllOutcomes`,
terminal durable-root `ContinueAsNew`, and scoped durable `AcquireResources`.

**Removed — no alias, tombstone, or placeholder:** `WaitLong`, author `Yield`.
**Deferred — absent from v1 public assemblies, but *must stay documented*:** public
`RunExternalJob`, Saga, `WhenFirst`, public `RunChild`/`RunChildren`, nested
`Parallel`/`While`/`ForEach`, durable lambda steps, definition-wide retry, management retry,
pause/resume/archive/purge, and workflow-authored `Cancel`. Durable workflow-authored `Publish`
and definition-targeted event fanout are current Section 7B capabilities, not deferred members.

Deferred and removed get **identical code treatment** (absent) and **opposite documentation
treatment**. A deferred capability keeps its entry, rationale, and re-entry criteria in the registry
at `docs/specs/13-phasing-and-open-questions.md` §13.4; do not erase its mention from docs. A removed
concept must disappear entirely. Do not write docs that teach a deferred capability as usable, and
do not delete the record that it is deferred.

Lambda step bodies are **ephemeral-only**; durable definitions use registered typed steps.

### Provider certification

`OrcaCore.ProviderCertification` contains provider contract tests for every current split
workflow-event, inbox, start-idempotency, outbox, projection, timer, operational, maintenance,
dispatch, and resource-governance port. PostgreSQL and SQL Server each run it plus provider-native
restart, competing-host, schema, migration, retention, poison, and pressure tests against real
storage. Run the shared and owning-provider suites when adding or modifying a provider.

## Key conventions

- Target framework: `net10.0`; SDK pinned in `global.json` (10.0.301, `rollForward: latestFeature`)
- C#: file-scoped namespaces, `Nullable=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true`
- Package versions: centrally managed in `Directory.Packages.props` — never use floating versions
- Test framework: xunit.v3
- Integration tests use Testcontainers — Docker must be running
- CRLF line endings, 4-space indentation (`.editorconfig`)

## Mandatory reviewed checkpoints

- Every implementation phase and every other large coherent change must receive its required
  review and then be committed before the next phase or large change begins.
- A phase is not complete merely because its tasks and validation are green. Completion requires
  an approval for the exact frozen target followed by a Git commit containing that approved target.
- Do not commit while an independent review is pending because changing `HEAD` invalidates the
  frozen provenance. If the review rejects the target, remediate, refreeze, and obtain approval
  before committing.
- Immediately after approval, confirm the reviewed manifest has no drift, stage the exact approved
  target including additions and deletions, create the checkpoint commit, report its SHA, and
  confirm the resulting worktree state before continuing.
- Do not accumulate multiple approved phases in one dirty worktree. The next phase remains blocked
  until the preceding approved phase has its checkpoint commit.
- For a large change without a formal phase gate, run and record proportionate validation and
  review first, then commit the coherent result before starting another large change.

## Specification workflow (mandatory)

Start from [`docs/normative-source-map.md`](docs/normative-source-map.md). It names every source,
classifies it, and carries the capability ↔ requirement-file crosswalk.

### Two normative trees — check both, always

`docs/specs/` (requirement IDs + acceptance criteria) and `openspec/specs/` (capability specs) are
**both** normative, and code is judged against both. Neither is authoritative alone.

- Before changing behavior, read **both** sides for the affected area. Use the crosswalk in the
  source map to find the counterpart — do not guess it.
- When they disagree, the approved change proposal that introduced the semantics wins. Do not
  silently prefer either tree. If no approved change covers it, **that is the finding** — report it.
- Do not assume the OpenSpec tree is more current because it is downstream. On 2026-07-31 it was the
  stale side on four semantics.

### `openspec/specs/` is derived — never hand-edit it

Canonical specs are synchronized from an approved change's deltas. Archive the change only when
the entire change is complete; approval and canonical synchronization may precede archival when
the active task graph explicitly requires later implementation or final-review work.

1. Create `openspec/changes/<id>/specs/<capability>/spec.md` using `## ADDED` / `## MODIFIED` /
   `## REMOVED Requirements`.
2. `MODIFIED` and `REMOVED` requirement headings must match the canonical heading **verbatim**.
   Verify each against `openspec/specs/` — a mismatch has already shipped as a P1 here.
3. Every `REMOVED` needs `**Reason**` and `**Migration**`.
4. Get approval, then synchronize.

Deltas carry **requirements only**. A spec's `## Purpose` cannot be expressed as a delta and must be
scheduled as an explicit hand-application step at sync time, or it is silently lost.

`openspec validate --strict` checks **structure, not provenance**. It cannot detect canonical
content that no change describes. Never cite it as evidence of workflow compliance.

Every reviewed checkpoint that includes active OpenSpec changes must also run
`OpenSpecCorpusGuards.CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners`
and record the provenance row count, byte count, SHA-256, unresolved-operation table, and semantic
approval eligibility from `Fixtures/openspec-provenance-checkpoint.json`. When that checkpoint
reports unresolved change-to-canonical operations, structural validation may be reported only as
structural validation; it SHALL NOT be reported as semantic approval.

The checkpoint enumerates canonical capabilities independently of active deltas. Archiving a
completed change may require a reviewed provenance-fixture refreeze, but SHALL NOT require every
canonical capability to retain an active delta forever. The permanent synchronized-removal catalog
must retain archived evidence and require the currently active synchronized-removal set to be a
subset, not an exact replacement. Evidence must embed the exact normalized historical canonical
block and pin its SHA-256. A source commit remains optional corroborating metadata: when its object
is available, the guard verifies it directly without requiring it to be an ancestor of `HEAD`; guard
correctness SHALL NOT depend on that Git object surviving a squash, rebase, or shallow checkout.
CI must execute the complete must-be-green guard lane with
`Disposition=Infrastructure`; intentional expected-red guards remain outside that lane.

A decision approved after its original implementation-section or canonical-sync gate has closed
must enter the machine-readable post-gate amendment registry. A proposed successor MAY coexist
temporarily with its active predecessor only when a source-pinned registry row names the exact
change pair, capability, verbatim requirement heading, operations, proposed stage, and open
canonical-sync task. That row is process evidence, not amendment approval: both changes remain
in the active provenance scan, the successor stays pending, semantic approval is false, and no
product source is authorized. After independent contract approval, canonical sync and the
`ApprovedPending` registry transition SHALL land atomically: the reviewed successor becomes
canonical, its retained predecessor is classified as superseded, and product source remains
unauthorized until the separate implementation review. Every other duplicate owner remains prohibited. The process-only
exception requires its own independent review and checkpoint before the contract amendment is
reviewed. Completed amendment records name amendment approval, canonical OpenSpec, numbered
requirements, acceptance criteria, implementation tasks, executable evidence, an exact refreeze,
and independent approval. Task states and evidence paths are checked literally; a completed
historical gate never supplies implicit coverage for a later decision. Records resolve either one
active or one dated archived change so normal archival is allowed. Each completed record embeds
the exact requirement identities from the owning delta and validates them against that active or
archived record rather than requiring the change to remain active. Approval evidence is
line-ending normalized before exact task/verdict checks, implementation-task ranges carry an
explicit count, and active provenance changes still require a reviewed fixture refreeze.

### Synchronization gates must enumerate targets, not inputs

Two independent consistency failures on 2026-07-31 shared one cause: a sync step that ran once, at a
fixed point, and ignored anything approved later or not named in its own inputs.

- A canonical sync must walk **every capability and every mapped file** in the crosswalk — not only
  the deltas the change happens to contain. A capability with no delta is otherwise invisible.
- An amendment approved **after** its section gate closed still needs a path into `docs/specs/`.
  Check the amendment's declared target list against the crosswalk; a file that is neither amended
  nor explicitly excluded is a gap, not a decision.
- Record the exact sync diff.

### Document status

Every doc is NORMATIVE, BINDING, GUIDE, RECORD, or HISTORICAL — see the source map.

- **Historical documentation lives in [`docs/archive/`](docs/archive/README.md)**, never in an active
  directory. Superseded material is moved with `git mv`, not left in place with a banner.
- Nothing under `docs/archive/` is evidence of current behavior. Do not copy its signatures, names,
  or diagrams into source, guards, or new docs without an explicit matrix/spec amendment.
- `docs/review/` records are **frozen**. Never edit a dated record to match a later contract; it is
  provenance. Resolve stale paths through the redirect table in the archive README.
- When you change the contract, update the affected GUIDE docs in the same change.

## Documentation

- [`docs/normative-source-map.md`](docs/normative-source-map.md) — **start here**: source
  classification and the cross-tree crosswalk
- `docs/specs/` — product requirements and acceptance criteria (normative)
- `openspec/specs/` — capability specs (normative, derived)
- `docs/implementation/` — stack decisions, conventions, TDD discipline (binding)
- `docs/ephemeral-engine-developer-guide.md` — complete API walkthrough with examples
- `docs/durable-driver-lane-host.md` — durable segment execution model and multi-host contention
- `docs/orleans-engine/` — planned Orleans-hosted durable engine variant (not a v1 obligation)
- `docs/archive/` — superseded documentation; provenance only, never current
