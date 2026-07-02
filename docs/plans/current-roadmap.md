# Current Roadmap

Last validated on April 12, 2026.

Validation run:

- `dotnet test OrcaCore.slnx --no-restore`
- `OrcaCore.Tests`: 321 passing
- `OrcaCore.EventDrivenPrototype.Tests`: 15 passing

## Status legend

- `Completed` means implemented in `src/` and covered by current tests.
- `In progress` means partially implemented, implemented but still being hardened, or actively under comparative research.
- `Planned` means documented intent only; no supported implementation exists yet.

## Roadmap summary

| Track | Status | Notes |
|------|--------|-------|
| State-driven regular runtime | `Completed` foundation, `In progress` convergence | Main workflow graph model is implemented and heavily tested. |
| State-driven durable runtime | `Completed` single-host baseline, `In progress` hardening | Durable waits, outbox, rehydration, and management are in place. |
| Event-driven prototype | `Completed` narrow prototype slice, `In progress` evaluation | Separate engine proves append-only resume/buffering/dedup basics. |
| Saga model | `Planned` | Requirements exist; no saga runtime or definition type exists yet. |
| Hosting and transports | `Planned` | No ASP.NET Core host package, local queue host integration, or RabbitMQ adapter exists in source. |
| Production readiness | `Planned` | Versioning policy, security review, samples, packaging, and operational maturity are not done. |

## 1. State-Driven Regular Runtime

### Completed

- Shared abstractions exist for `IStep`, `StepContext`, `StepResult`, `EventEnvelope`, waits, workflow status, and snapshots.
- Ephemeral `WorkflowEngine` exists with in-memory instance storage.
- Code-first builder surface exists for:
  - `Init`
  - `End`
  - business steps
  - `If`
  - `While`
  - `Parallel`
  - `Wait`
- Runtime execution supports:
  - straight-line workflows
  - nested control flow
  - wait registration and correlated resume
  - buffered event consumption when a matching wait appears later
  - failure capture and terminal lifecycle transitions
  - query/list APIs over workflow instances
- Current semantics are test-covered across the main suite in `tests/OrcaCore.Tests`.

### In progress

- Runtime/documentation convergence.
- Clarifying and tightening branch semantics.
- Stabilizing the public surface before broader integrations.

### Planned

- First-class timer model.
- Child workflow orchestration.
- Public retry/timeout policy model.
- Rich step side effects such as send/publish/schedule semantics.
- Broader operational statistics and diagnostics surface.

## 2. State-Driven Durable Runtime

### Completed

- `DurableWorkflowBuilder<TState>` and `DurableWorkflowDefinition<TState>` exist as a separate authoring surface.
- Durable-only `WaitLong` is implemented.
- `IWorkflowStore` defines the durable persistence contract.
- `InMemoryWorkflowStore` implements the current provider baseline.
- `DurableWorkflowEngine` exists and supports:
  - durable start
  - durable event raise/resume
  - restart rehydration
  - cold `WaitLong` eviction and lazy reload
  - persisted correlation lookup
  - durable query APIs across hot and cold instances
- Durable persistence includes:
  - persisted runtime state
  - frames and execution paths
  - waits with explicit `WaitStatus` and `WaitMode`
  - pending events
  - inbox records
  - outbox records
  - history records
- Durable inbox/outbox behavior exists for:
  - duplicate-event suppression
  - buffered unmatched events
  - status transition outbox messages
  - manual outbox dispatch
  - automatic background outbox replay
  - poison/failure hooks and delay strategy hooks
- Durable management APIs exist for:
  - deleting instances
  - purging artifacts by cutoff
  - purging artifacts by retention policy
- Durable behavior is broadly covered in `tests/OrcaCore.Tests/Durable`.

### In progress

- Single-host durable hardening into a more production-shaped baseline.
- Durable API cleanup and behavior clarification.
- Narrowing the gap between durable requirements, docs, and current implementation.

### Planned

- Real durable store adapters beyond the in-memory provider.
- Multi-host execution semantics and leasing.
- Transport-specific dispatch adapters.
- Decorator/policy state persisted per activation scope.
- Archival strategy and richer observability/metrics integration.

## 3. Event-Driven Prototype

### Completed

- Separate prototype project and test project exist.
- `EventDrivenWorkflowEngine` implements a narrow append-only workflow slice.
- Implemented prototype behavior includes:
  - versioned definition registration
  - append-only per-instance event stream
  - materialized checkpoint state
  - summary and active-wait projections
  - inbox dedup state
  - start workflow
  - straight-line step execution
  - `Wait`
  - instance-targeted and correlation-targeted event delivery
  - pending-event buffering
  - duplicate-event suppression
  - restart-safe resume from persisted checkpoint state

### In progress

- Comparative evaluation against the state-driven durable path.
- Deciding whether to evolve the prototype into a supported engine or fold its learnings back into the main runtime.

### Planned

- `If`
- `While`
- `Parallel`
- `WaitLong`
- timers
- outbox
- provider abstraction beyond the in-memory prototype store
- management/query parity with the main runtime
- any meaningful saga slice

## 4. Saga Track

### Completed

- Saga requirements and acceptance documents exist under `docs/requirements/saga/`.

### In progress

- None.

### Planned

- Separate saga definition kind.
- Forward plus compensating actions.
- Compensation scope and reverse compensation order.
- Saga-specific terminal outcomes.
- Explicit timeout/cancellation semantics for sagas.
- Durable saga implementation.

## 5. Hosting, Messaging, and Transport Integration

### Completed

- Messaging abstractions exist at the OrcaCore level:
  - `IMessageDispatcher`
  - `DispatchMessage`
  - `DispatchPayload`
  - `DispatchOutcome`
- Durable outbox records can be dispatched through host-supplied dispatchers.

### In progress

- None.

### Planned

- Host-level `AddOrcaCore()` / `UseOrcaCore()` style integration.
- ASP.NET Core endpoint integration.
- Local worker queue model.
- Scheduled local execution model.
- RabbitMQ transport adapter.
- Other broker adapters such as SQS, Kafka, or similar.

## 6. Production-Readiness Track

### Completed

- Public repository layout, documentation tree, and test suites are in place.
- The project targets .NET 10 and currently builds/tests successfully.

### In progress

- Research and design refinement.

### Planned

- Long-running versioning and upgrade story.
- Breaking-change policy for public APIs.
- Security review.
- Performance targets and benchmarking.
- Sample host applications.
- Published packages and semantic versioning.
- Stronger operational diagnostics and support posture.

## Recommended next roadmap slice

If the goal is to improve the current project without changing its identity, the next roadmap slice should be:

1. Finish state-driven runtime convergence and tighten docs to current behavior.
2. Harden the durable single-host runtime around policies, adapters, and operational seams.
3. Decide whether OrcaCore wants a process-manager saga track before a compensation-heavy saga track.
4. Add hosting and transport integration only after the runtime contracts are stable enough to expose publicly.
