# Engine runtime diagrams

This document illustrates how the **in-memory event-driven** runtime (`WorkflowEngine`), the **durable** runtime (`DurableWorkflowEngine`), and the **event-driven prototype** (`EventDrivenWorkflowEngine`) relate to storage, routing, and execution.

Sources of truth in code:

- `OrcaCore.Runtime/Engine/WorkflowEngine.cs`, `Querying/InstanceScope.cs`, `Execution/WorkflowRuntime.cs`
- `OrcaCore.Runtime/Durable/Engine/DurableWorkflowEngine.cs`, `Durable/Routing/DurableEventRouter.cs`, `Durable/Outbox/DurableOutboxPump.cs`
- `OrcaCore.EventDrivenPrototype/Engine/EventDrivenWorkflowEngine.cs`

---

## 1. Three engines at a glance

```mermaid
flowchart TB
  subgraph Mem["WorkflowEngine (in-memory)"]
    M1[InMemoryInstanceStore]
    M2[CorrelationIndex in process]
    M3[WorkflowRuntime.ExecuteAsync]
    M1 --- M2
    M2 --- M3
  end

  subgraph Dur["DurableWorkflowEngine"]
    D1[IWorkflowStore]
    D2[DurableEventRouter]
    D3[DurableOutboxPump optional]
    D4[WorkflowRuntime.ExecuteAsync durableMode]
    D1 --- D2
    D2 --- D4
    D1 --- D3
  end

  subgraph Proto["EventDrivenWorkflowEngine (prototype)"]
    P1[InMemoryPrototypeStore]
    P2[PrototypeInstanceCommandLane per instance]
    P3[RunToSuspensionAsync + stream records]
    P1 --- P2
    P2 --- P3
  end
```

All three ultimately advance a `WorkflowInstance` by running nodes until completion, suspension on `WaitForEvent`, or failure. The durable path persists each transition; the prototype records an append-only style stream per commit.

---

## 2. In-memory engine — components

```mermaid
flowchart LR
  WE[WorkflowEngine]
  Store[InMemoryInstanceStore]
  CI[CorrelationIndex]
  WET["WorkflowEngine TState"]

  WE --> Store
  WE --> CI
  WET --> WE
  WET -->|Start| RT[WorkflowRuntime]
  IS[InstanceScope] --> Store
  IS -->|RaiseEvent| Q[EnqueueAsync serialized per instance]
  Q --> RT
```

---

## 3. In-memory — starting a workflow

```mermaid
sequenceDiagram
  participant Caller
  participant WET as WorkflowEngine of TState
  participant Store as InMemoryInstanceStore
  participant Inst as WorkflowInstance
  participant RT as WorkflowRuntime

  Caller->>WET: Start(initialState)
  WET->>Store: Add(instance)
  WET->>Inst: ExecutionLock.Wait
  WET->>RT: ExecuteAsync(... durableMode false)
  RT-->>Inst: advances until Completed / Waiting / Failed
  WET->>Inst: ExecutionLock.Release
  WET-->>Caller: WorkflowInstanceSnapshot
```

---

## 4. In-memory — raising an event to one instance

`WorkflowEngine.RaiseEvent` resolves the instance via `(EventName, CorrelationId)` on the in-memory correlation index, then `InstanceScope.RaiseEvent` runs the handler under the instance command queue.

```mermaid
sequenceDiagram
  participant Caller
  participant WE as WorkflowEngine
  participant CI as CorrelationIndex
  participant IS as InstanceScope
  participant Store as InMemoryInstanceStore
  participant RT as Resume delegate

  Caller->>WE: RaiseEvent(envelope)
  WE->>CI: ResolveExactlyOne
  CI-->>WE: instanceId
  WE->>IS: RaiseEvent(envelope)
  IS->>Store: EnqueueAsync(instanceId, handler)

  Note over Store: Dedup by EventId consumed or pending

  alt No matching ActiveWait
    IS->>Store: PendingEvents.Add (buffer)
  else Match found
    IS->>Store: Mark wait Matched, CI.Remove
    IS->>Store: Status Running
    IS->>RT: resume(instance, envelope, match)
    Note over RT: WorkflowRuntime continues until next wait or terminal
  end
```

---

## 5. Durable engine — components

```mermaid
flowchart TB
  DWE[DurableWorkflowEngine]
  Store[(IWorkflowStore)]
  Reg[DurableDefinitionRegistry]
  IM[DurableInstanceManager]
  RegIndex[CorrelationIndex process + store lookup]
  Router[DurableEventRouter]
  Pump[DurableOutboxPump]
  Disp[IMessageDispatcher optional]

  DWE --> Store
  DWE --> Reg
  DWE --> IM
  DWE --> Router
  DWE --> Pump
  Router --> Store
  Router --> IM
  IM --> RegIndex
  Pump --> Store
  Pump --> Disp
```

---

## 6. Durable — starting a workflow (persisted commit)

```mermaid
sequenceDiagram
  participant Caller
  participant DWET as DurableWorkflowEngine of TState
  participant DWE as DurableWorkflowEngine
  participant IM as DurableInstanceManager
  participant Inst as WorkflowInstance
  participant RT as WorkflowRuntime
  participant Mapper as StateMapper
  participant Store as IWorkflowStore
  participant ER as DurableEventRouter helpers

  Caller->>DWET: Start(initialState)
  DWET->>DWE: StartAsync(definition, state)
  DWE->>IM: RegisterInstance in registry
  DWE->>Inst: ExecutionLock.Wait
  DWE->>RT: ExecuteAsync(... durableMode true)
  RT-->>Inst: Running / Waiting / Completed / Failed
  DWE->>Mapper: ToPersistedState
  DWE->>ER: CreateTransitionOutboxRecords
  DWE->>Store: CreateAsync(WorkflowCommit persisted + outbox + history)
  DWE->>Inst: ExecutionLock.Release
  DWE-->>Caller: DurableInstanceSnapshot
```

---

## 7. Durable — correlation routing and per-instance raise

```mermaid
sequenceDiagram
  participant Caller
  participant DWE as DurableWorkflowEngine
  participant Router as DurableEventRouter
  participant CI as CorrelationIndex
  participant Store as IWorkflowStore
  participant IM as DurableInstanceManager
  participant Inst as WorkflowInstance

  Caller->>DWE: RaiseEvent(envelope)
  DWE->>Router: RouteAsync

  alt Correlation in memory
    Router->>CI: ResolveExactlyOne
  else Not in memory
    Router->>Store: LookupByCorrelationAsync
  end

  Router->>IM: EnsureLoadedRegistrationAsync
  Router->>Inst: ExecutionLock.Wait

  alt Duplicate EventId
    Router-->>Caller: return no-op
  else No wait match non-terminal
    Router->>Inst: PendingEvents.Add
    Router->>Store: CommitAsync inbox unprocessed history EventBuffered
  else Wait matched
    Router->>Inst: mark Matched staged CI remove Running
    Router->>Inst: Resume WorkflowRuntime
    Router->>Store: CommitAsync inbox processed outbox transition history
  end

  Router->>Inst: ExecutionLock.Release
```

---

## 8. Durable — transactional outbox dispatch (optional)

When `AutoDispatchOutbox` is enabled with an `IMessageDispatcher`, the pump leases pending rows, dispatches, then completes or fails them in the store.

```mermaid
sequenceDiagram
  participant Pump as DurableOutboxPump
  participant Store as IWorkflowStore
  participant Disp as IMessageDispatcher

  loop Until disposed
    Pump->>Store: LeaseDispatchableOutboxAsync
    Store-->>Pump: leased records
    loop Each record
      Pump->>Disp: DispatchAsync(DispatchMessage)
      alt Success
        Pump->>Store: CompleteLeasedOutboxAsync
      else Failure
        Pump->>Store: FailLeasedOutboxAsync
      end
    end
    Pump->>Pump: Delay backoff
  end
```

---

## 9. Event-driven prototype — components

The prototype uses a **per-instance command lane** (serialized commands) and commits **checkpoint state** plus **stream records** and **inbox rows** on each transition.

```mermaid
flowchart LR
  ED[EventDrivenWorkflowEngine]
  Lane[PrototypeInstanceCommandLane]
  StoreP[InMemoryPrototypeStore]
  RegP[RegisteredPrototypeDefinition]
  Run[RunToSuspensionAsync]

  ED --> Lane
  Lane --> StoreP
  ED --> RegP
  RegP --> Run
```

---

## 10. Event-driven prototype — start and raise (simplified)

```mermaid
sequenceDiagram
  participant Caller
  participant ED as EventDrivenWorkflowEngine
  participant Lane as CommandLane
  participant Reg as RegisteredPrototypeDefinition
  participant Run as RunToSuspensionAsync
  participant Store as InMemoryPrototypeStore

  Caller->>ED: StartAsync(defId, version, state)
  ED->>Reg: RunToSuspensionAsync(initial, null)
  Reg-->>ED: PrototypeExecutionResult
  ED->>Store: CommitAsync checkpoint + lifecycle stream events + inbox
  ED->>ED: TryConsumeBufferedEventsAsync if Waiting and pending

  Caller->>ED: RaiseEventToInstanceAsync / RaiseEventByCorrelationAsync
  ED->>Lane: EnqueueAsync handler
  Lane->>Store: LoadCheckpointAsync
  alt Duplicate
    Lane->>Store: Commit duplicate ignored stream + inbox
  else Buffer unmatched
    Lane->>Store: Commit buffered checkpoint + EventBuffered
  else Matching wait
    Lane->>Reg: RunToSuspensionAsync(checkpoint, envelope)
    Lane->>Store: Commit next checkpoint + WaitMatched + execution events + inbox processed
    Lane->>ED: TryConsumeBufferedEventsAsync
  end
```

---

## 11. Execution loop (shared concept)

Both production engines delegate stepping to `WorkflowRuntime`: business steps run until `StepResult.Completed`, `Failed`, or `WaitForEvent` (suspends with active waits and correlation registration). Durable mode affects how correlation and buffering interact with persistence (durable commits after each start or event-driven transition).

```mermaid
stateDiagram-v2
  [*] --> Running: Start / Event matched
  Running --> Running: Step completes
  Running --> Waiting: WaitForEvent registered
  Waiting --> Running: Matching event delivered
  Running --> Completed: Main path finished
  Running --> Failed: Exception or StepResult.Failed
  Completed --> [*]
  Failed --> [*]
```

---

## How to read these alongside deeper design docs

- Outbox and provider ports: `docs/architecture/event-driven-outbox-design.md`, `docs/architecture/provider-ports-for-outbox-design.md`
- Durable inbox buffering and idempotency mirror the in-memory `InstanceScope` behavior but add `IWorkflowStore` commits and inbox records.
