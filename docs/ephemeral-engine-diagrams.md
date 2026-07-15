# Ephemeral Engine Runtime Diagrams

Visual reference for the **current implementation in-process ephemeral engine** (`OrcaCore.Engine.Ephemeral`).
All state is process-local; nothing survives restart.

Sources of truth in code:

- `src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs`
- `src/OrcaCore.Engine.Ephemeral/Execution/*`
- `src/OrcaCore.Engine.Ephemeral/Management/EphemeralManagement.cs`
- `src/OrcaCore.Engine.Ephemeral/Governance/ResourceGovernanceCoordinator.cs`
- `src/OrcaCore.Engine.Ephemeral/Timers/EphemeralTimerService.cs`
- `src/OrcaCore.Core/Definitions/*`, `src/OrcaCore.Core/Building/*`
- `src/OrcaCore.Abstractions/*`

See also: [Ephemeral Engine Developer Guide](ephemeral-engine-developer-guide.md)

---

## 1. Solution layers

```mermaid
flowchart TB
  subgraph Host["Host application"]
    H1[Start / RaiseEvent / FireDueTimers]
    H2[Management queries & commands]
    H3[Timer pump scheduler]
  end

  subgraph Abstractions["OrcaCore.Abstractions"]
    A1[IStep / StepContext / StepResult]
    A2[EventEnvelope / IDs]
    A3[WorkflowInstanceSnapshot]
    A4[WorkflowStatus / LifecycleMachine]
  end

  subgraph Core["OrcaCore.Core"]
    C1[WorkflowBuilder]
    C2[WorkflowDefinition]
    C3[Definition node tree]
    C4[SagaBuilder / SagaDefinition]
  end

  subgraph Engine["OrcaCore.Engine.Ephemeral"]
    E1[EphemeralWorkflowEngine]
    E2[Interpreter + node runners]
    E3[InstanceExecutionLane]
    E4[EphemeralRoutingIndex]
    E5[EphemeralTimerService]
    E6[EphemeralManagement]
  end

  Host --> E1
  Host --> E6
  H3 --> E1
  E1 --> Core
  E1 --> Abstractions
  E2 --> Core
  E2 --> Abstractions
```

---

## 2. Engine composition (class diagram)

```mermaid
classDiagram
  direction TB

  class EphemeralWorkflowEngine {
    -definitions: ConcurrentDictionary
    -sagaRuntimeStates: ConcurrentDictionary
    -instanceRegistry: IInstanceRegistry
    -executionLane: InstanceExecutionLane
    -governance: ResourceGovernanceCoordinator
    -routingIndex: EphemeralRoutingIndex
    -timerService: EphemeralTimerService
    -interpreterFactory: InterpreterFactory
    -yieldContinuationScheduler: YieldContinuationScheduler
    +Management: EphemeralManagement
    +RegisterDefinition()
    +StartAsync()
    +RaiseEventAsync()
    +RaiseEventByCorrelationAsync()
    +RaiseEventByDefinitionAsync()
    +FireDueTimersAsync()
    +StartSagaAsync()
  }

  class EphemeralManagement {
    +All()
    +ForDefinition()
    +Instance()
    +Instances()
  }

  class EphemeralManagementQuery {
    +Where()
    +List()
    +Statistics()
    +DetectStuck()
    +RaiseEventAsync()
    +CancelAsync()
    +TerminateAsync()
  }

  class InstanceExecutionLane {
    +RunAsync(instanceId, operation)
  }

  class InMemoryInstanceRegistry {
    +Save()
    +TryGet()
    +GetMany()
    +List()
  }

  class EphemeralRoutingIndex {
    +FindCandidates(envelope)
    +IndexSnapshot(snapshot)
  }

  class EphemeralTimerService {
    +Schedule()
    +Cancel()
    +ClaimDueTimers()
  }

  class ResourceGovernanceCoordinator {
    +EnterAdvancementAsync()
    +EnterStepAsync(poolKey)
  }

  class InterpreterFactory {
    +Create~TState~()
  }

  class YieldContinuationScheduler {
    +Schedule()
    +DrainAsync()
  }

  EphemeralWorkflowEngine --> EphemeralManagement
  EphemeralWorkflowEngine --> InstanceExecutionLane
  EphemeralWorkflowEngine --> InMemoryInstanceRegistry
  EphemeralWorkflowEngine --> EphemeralRoutingIndex
  EphemeralWorkflowEngine --> EphemeralTimerService
  EphemeralWorkflowEngine --> ResourceGovernanceCoordinator
  EphemeralWorkflowEngine --> InterpreterFactory
  EphemeralWorkflowEngine --> YieldContinuationScheduler
  EphemeralManagement --> EphemeralManagementQuery
  EphemeralManagement --> InMemoryInstanceRegistry
  InterpreterFactory --> ResourceGovernanceCoordinator
  InterpreterFactory --> EphemeralTimerService
  YieldContinuationScheduler --> InstanceExecutionLane
  YieldContinuationScheduler --> ResourceGovernanceCoordinator
```

---

## 3. Interpreter and execution pipeline (class diagram)

```mermaid
classDiagram
  direction LR

  class Interpreter~TState~ {
    -stateAdapter: InMemoryExecutionStateAdapter
    +RunAsync(plan, input, instanceId)
  }

  class InMemoryExecutionStateAdapter~TState~ {
    -execution: StructuredExecutionState
    -waitsByFiber
    -timersByFiber
    +RunAsync()
    +ResumeWaitAsync()
    +ResumeTimerAsync()
  }

  class FiberScheduler {
    +SelectNext(state)
    +CompleteTurn(state, fiberId)
  }

  class ScopeReducer {
    +StartScope(state, parent, plan)
    +CommitChildOutcomes(state, scope, outcomes)
    +ReturnScope(state, scope)
  }

  class ScopeMergeAdapter {
    +Merge(parentState, scopePlan, results)
  }

  class WorkflowInstance~TState~ {
    +State: TState
    +Status: WorkflowStatus
    +EnterWait()
    +EnterDelay()
    +RaiseEventAsync()
    +FireDelayAsync()
    +FireWaitTimeoutAsync()
    +ToSnapshot()
  }

  class FiberRecord {
    +Id: FiberId
    +OwningScopeId
    +InstructionIndex
    +Phase
    +LocalStatePayload
  }

  class ExecutionScopeRecord {
    +Id: ScopeId
    +ParentFiberId
    +ChildFiberIds
    +WinnerFiberId
    +CommittedResults
  }

  Interpreter~TState~ --> InMemoryExecutionStateAdapter~TState~
  InMemoryExecutionStateAdapter~TState~ --> FiberScheduler
  InMemoryExecutionStateAdapter~TState~ --> ScopeReducer
  InMemoryExecutionStateAdapter~TState~ --> ScopeMergeAdapter
  InMemoryExecutionStateAdapter~TState~ --> FiberRecord
  InMemoryExecutionStateAdapter~TState~ --> ExecutionScopeRecord
  Interpreter~TState~ --> WorkflowInstance~TState~
```

---

## 4. Workflow instance — in-memory state (class diagram)

```mermaid
classDiagram
  direction TB

  class WorkflowInstance~TState~ {
    +InstanceId
    +DefinitionId
    +DefinitionVersion
    +State: TState
    +Status
    -activeWaits: List~RuntimeWaitRecord~
    -activeTimers: List~RuntimeTimerRecord~
    -pendingEvents: List~EventEnvelope~
    -consumedEventIds: HashSet
    -consumedWaits / timedOutWaits
    -lifecycleEvents
    -compositionOutcomes
    -forEachGroups
    -activeStep
    -yieldContinuation
    +HasUnresolvedRuntimeWork
    +HasActiveWait(eventName, correlation)
  }

  class RuntimeWaitRecord {
    +EventName
    +CorrelationId
    +FiberId
    +ScopeId
    +Matches(envelope)
    +ResumeAsync(envelope)
    +CancelLoser()
  }

  class RuntimeTimerRecord {
    +FiberId
    +ScopeId
    +RegisteredAt
    +SetCancel()
  }

  class EventEnvelope {
    +EventId
    +EventName
    +CorrelationId
    +Payload
    +OccurredAt
  }

  class WorkflowInstanceSnapshot {
    +InstanceId
    +Status
    +ActiveWaits
    +ActiveStep
    +LifecycleEvents
    +CompositionOutcomes
    +ForEachGroups
    +ErrorSummary
    +EndOutcomeName
  }

  WorkflowInstance~TState~ "1" *-- "0..*" RuntimeWaitRecord
  WorkflowInstance~TState~ "1" *-- "0..*" RuntimeTimerRecord
  WorkflowInstance~TState~ "1" o-- "0..*" EventEnvelope : pending mailbox
  WorkflowInstance~TState~ ..> WorkflowInstanceSnapshot : ToSnapshot()
  RuntimeWaitRecord ..> EventEnvelope : matches / resumes
```

---

## 5. Definition model (class diagram)

```mermaid
classDiagram
  direction TB

  class WorkflowBuilder~TState~ {
    +Init~TInput~()
    +Then~TStep~()
    +Wait()
    +Delay()
    +If()
    +While()
    +Parallel()
    +WhenFirst()
    +ForEach()
    +WithRetry()
    +WithTimeout()
    +WithPoolKey()
    +End()
    +Build()
  }

  class WorkflowDefinition~TState~ {
    +DefinitionId
    +DefinitionVersion
    +RootSequence
    +RequiresDurableEngine
  }

  class SequenceNode~TState~ {
    <<abstract>>
    +NodeId
    +Children
  }

  class InitNode~TState~
  class BusinessStepNode~TState~
  class WaitNode~TState~
  class DelayNode~TState~
  class IfNode~TState~
  class WhileNode~TState~
  class ParallelNode~TState~
  class WhenFirstNode~TState~
  class ForEachNode~TState~
  class EndNode~TState~
  class RunChildNode~TState~
  class RunChildrenNode~TState~

  class IStep~TState~ {
    <<interface>>
    +ExecuteAsync(context) StepResult
  }

  class StepResult {
    <<sealed hierarchy>>
    Completed
    Failed
    WaitForEvent
    Yield
  }

  WorkflowBuilder~TState~ ..> WorkflowDefinition~TState~ : Build
  WorkflowDefinition~TState~ --> SequenceNode~TState~
  SequenceNode~TState~ <|-- InitNode~TState~
  SequenceNode~TState~ <|-- BusinessStepNode~TState~
  SequenceNode~TState~ <|-- WaitNode~TState~
  SequenceNode~TState~ <|-- DelayNode~TState~
  SequenceNode~TState~ <|-- IfNode~TState~
  SequenceNode~TState~ <|-- WhileNode~TState~
  SequenceNode~TState~ <|-- ParallelNode~TState~
  SequenceNode~TState~ <|-- WhenFirstNode~TState~
  SequenceNode~TState~ <|-- ForEachNode~TState~
  SequenceNode~TState~ <|-- EndNode~TState~
  SequenceNode~TState~ <|-- RunChildNode~TState~
  SequenceNode~TState~ <|-- RunChildrenNode~TState~
  BusinessStepNode~TState~ ..> IStep~TState~
  IStep~TState~ ..> StepResult
```

---

## 6. Concurrency and serialization model

Every instance is advanced through a **per-instance execution lane**. Global
concurrency is bounded separately by **resource governance**.

```mermaid
flowchart TB
  subgraph Process["Process"]
    subgraph Governance["ResourceGovernanceCoordinator"]
      GA[MaxConcurrentAdvancements semaphore]
      GS[MaxConcurrentSteps semaphore]
      GP[Named pool semaphores]
    end

    subgraph Lanes["InstanceExecutionLane"]
      L1[Instance A queue]
      L2[Instance B queue]
      L3[Instance C queue]
    end

    GA --> L1
    GA --> L2
    GA --> L3
    GS --> StepExec[StepExecutor]
    GP --> StepExec
  end

  Call1[StartAsync A] --> GA
  Call2[RaiseEvent A] --> GA
  Call3[RaiseEvent B] --> GA
  GA --> L1
  GA --> L2

  note1["Same instance: strictly serialized"]
  note2["Different instances: may run concurrently up to advancement limit"]
  L1 --- note1
  L2 --- note2
```

---

## 7. Lifecycle state machine

```mermaid
stateDiagram-v2
  [*] --> Running: Init / resume after wait or timer

  Running --> Waiting: EnterWait (Wait / Delay / step WaitForEvent)
  Waiting --> Running: MatchWait (event matched or timer fired)

  Running --> Completed: Complete
  Running --> Failed: Fail
  Running --> Cancelled: Cancel
  Running --> Terminated: Terminate

  Running --> Compensated: Compensate (saga)
  Running --> CompensationFailed: FailCompensation (saga)

  Completed --> [*]
  Failed --> [*]
  Cancelled --> [*]
  Terminated --> [*]
  Compensated --> [*]
  CompensationFailed --> [*]

  note right of Waiting
    Paused is defined in shared contracts
    but never produced by the ephemeral engine
  end note
```

---

## 8. Start workflow (sequence)

```mermaid
sequenceDiagram
  participant Host
  participant Engine as EphemeralWorkflowEngine
  participant Gov as ResourceGovernanceCoordinator
  participant Lane as InstanceExecutionLane
  participant IF as InterpreterFactory
  participant Int as Interpreter
  participant Inst as WorkflowInstance
  participant Reg as InMemoryInstanceRegistry
  participant Yield as YieldContinuationScheduler
  participant Route as EphemeralRoutingIndex

  Host->>Engine: StartAsync(definitionId, input)
  Engine->>Gov: EnterAdvancementAsync()
  Engine->>Lane: RunAsync(instanceId, ...)
  Lane->>IF: Create~TState~()
  IF-->>Int: new Interpreter
  Lane->>Int: RunAsync(definition, input, instanceId)
  loop Run sequence nodes
    Int->>Inst: Init / steps / control flow
  end
  Int-->>Lane: WorkflowInstance
  Lane->>Reg: Save(instance)
  Lane-->>Engine: snapshot
  Engine->>Gov: release advancement
  Engine->>Yield: DrainAsync (process Yield continuations)
  Engine->>Route: IndexSnapshot(snapshot)
  Engine-->>Host: WorkflowInstanceSnapshot
```

---

## 9. Raise event — instance targeted (sequence)

```mermaid
sequenceDiagram
  participant Host
  participant Engine as EphemeralWorkflowEngine
  participant Gov as ResourceGovernanceCoordinator
  participant Lane as InstanceExecutionLane
  participant Inst as WorkflowInstance
  participant Int as Interpreter (via resume delegate)
  participant Yield as YieldContinuationScheduler
  participant Route as EphemeralRoutingIndex

  Host->>Engine: RaiseEventAsync(instanceId, envelope)
  Engine->>Gov: EnterAdvancementAsync()
  Engine->>Lane: RunAsync(instanceId, ...)
  Lane->>Inst: RaiseEventAsync(envelope)

  alt Duplicate EventId
    Inst-->>Lane: ToSnapshot (no-op)
  else No matching active wait
    Inst->>Inst: pendingEvents.Add(envelope)
    Inst-->>Lane: ToSnapshot (buffered)
  else Wait matched
    Inst->>Inst: remove wait, Running
    Inst->>Int: ContinueSequenceAsync(resumeEvent)
    Note over Int: Steps after wait execute until next suspension or terminal
    Inst-->>Lane: ToSnapshot
  end

  Lane-->>Engine: snapshot
  Engine->>Gov: release
  Engine->>Yield: DrainAsync
  Engine->>Route: IndexSnapshot
  Engine-->>Host: WorkflowInstanceSnapshot
```

---

## 10. Correlation routing (sequence)

```mermaid
sequenceDiagram
  participant Host
  participant Engine as EphemeralWorkflowEngine
  participant Route as EphemeralRoutingIndex
  participant Reg as InMemoryInstanceRegistry
  participant Inst as WorkflowInstance

  Host->>Engine: RaiseEventByCorrelationAsync(envelope)
  Engine->>Route: FindCandidates(envelope)
  Route-->>Engine: candidate InstanceIds

  alt Zero candidates
    Engine-->>Host: WorkflowRoutingException
  end

  Engine->>Reg: GetMany(candidates)
  Engine->>Engine: filter HasActiveWait(name, correlation)

  alt Zero matches
    Engine-->>Host: WorkflowRoutingException
  else Multiple matches
    Engine-->>Host: WorkflowRoutingException (ambiguous)
  else Exactly one match
    Engine->>Engine: RaiseEventAsync(matchedInstanceId, envelope)
    Engine-->>Host: WorkflowInstanceSnapshot
  end

  Note over Route: Index rebuilt from snapshot.ActiveWaits after each transition
```

---

## 11. Wait registration and mailbox (flowchart)

```mermaid
flowchart TD
  A[Step or Wait node reaches suspension] --> B[SuspensionScheduler.RegisterWaitAsync]
  B --> C[instance.EnterWait]
  C --> D{Timeout configured?}
  D -->|yes| E[Schedule timeout timer via EphemeralTimerService]
  D -->|no| F[MatchPendingEventAsync]
  E --> F

  F --> G{Buffered event in mailbox?}
  G -->|yes| H[ResumeWaitAsync immediately]
  G -->|no| I[Status = Waiting, return snapshot]

  J[External RaiseEventAsync] --> K{Duplicate EventId?}
  K -->|yes| L[No-op]
  K -->|no| M{Active wait matches?}
  M -->|no| N[Add to pendingEvents mailbox]
  M -->|yes| H

  H --> O[Interpreter.ContinueSequenceAsync with ResumeEventSlot]
  O --> P[Advance sequence from nextIndex]
```

---

## 12. Timer pump (sequence)

Timers are **transient**. The host must call `FireDueTimersAsync`.

```mermaid
sequenceDiagram
  participant Host
  participant Engine as EphemeralWorkflowEngine
  participant Timer as EphemeralTimerService
  participant Gov as ResourceGovernanceCoordinator
  participant Lane as InstanceExecutionLane
  participant Inst as WorkflowInstance
  participant Int as Interpreter

  Note over Host: Delay or Wait+timeout registered earlier
  Host->>Engine: FireDueTimersAsync()
  Engine->>Timer: ClaimDueTimers()
  Timer-->>Engine: due ScheduledTimer[]

  loop Each due timer
    Engine->>Gov: EnterAdvancementAsync()
    Engine->>Lane: RunAsync(instanceId, timer.FireAsync)
    Lane->>Inst: FireDelayAsync / FireWaitTimeoutAsync
    Inst->>Int: ContinueSequenceAsync (ResumeEvent null on timeout)
    Lane-->>Engine: snapshot
    Engine->>Gov: release
  end

  Engine-->>Host: IReadOnlyList~WorkflowInstanceSnapshot~
```

---

## 13. Yield cooperative scheduling (sequence)

```mermaid
sequenceDiagram
  participant Step as IStep
  participant Int as Interpreter
  participant Inst as WorkflowInstance
  participant Yield as YieldContinuationScheduler
  participant Engine as EphemeralWorkflowEngine
  participant Lane as InstanceExecutionLane

  Step-->>Int: StepResult.Yield
  Int->>Yield: Schedule(instance, continue at same index)
  Yield->>Inst: ScheduleYield(continuation delegate)

  Note over Engine: StartAsync / RaiseEventAsync returns first snapshot

  Engine->>Yield: DrainAsync
  loop While yield continuation exists
    Engine->>Lane: RunAsync(instanceId)
    Lane->>Inst: TryTakeYieldContinuation
    Lane->>Int: ContinueSequenceAsync (same step index)
    Int->>Step: ExecuteAsync again
  end
  Yield-->>Engine: final snapshot
```

---

## 14. Compiled instruction dispatch (flowchart)

```mermaid
flowchart TD
  Start[Scheduler selects runnable fiber] --> Switch{compiled instruction}

  Switch --> Step[ExecuteStep: run against fiber-local state]
  Switch --> Wait[Wait: register ownership by FiberId and ScopeId]
  Switch --> Delay[Delay: register ownership by FiberId and ScopeId]
  Switch --> Jump[Jump or conditional jump]
  Switch --> Enter[ScopeEnter: reducer creates child fibers]
  Switch --> Return[BranchReturn: commit typed result]
  Switch --> Join[ScopeJoin: select ordered outcome or winner]
  Switch --> Merge[ScopeMerge: replace parent state explicitly]
  Switch --> Exit[ScopeExit: unblock parent fiber]
  Switch --> End[End: select canonical root outcome]

  Step --> StepResult{result}
  StepResult -->|Continue| Next
  StepResult -->|Wait or Yield| Suspend[commit suspension and rotate]
  StepResult -->|Failed| Fail[fail fiber and reduce owning scope]

  Wait --> Suspend
  Delay --> Suspend
  Enter --> Suspend
  Return --> Join
  Join --> Merge
  Merge --> Exit
  Exit --> Next
  End --> Done[derive terminal workflow status]

  Next[advance instruction within quantum] --> Start
```

---

## 15. Parallel vs WhenFirst vs ForEach

```mermaid
flowchart LR
  subgraph Parallel["Parallel (WhenAll scope)"]
    P1[Child fiber 0 with private state]
    P2[Child fiber 1 with private state]
    P3[Child fiber N with private state]
    PJ[ScopeJoin: authored-order results]
    PM[Explicit merge replaces parent state]
    PC[Continue parent fiber]
    P1 --> PJ
    P2 --> PJ
    P3 --> PJ
    PJ --> PM --> PC
  end

  subgraph WhenFirst["WhenFirst (single-winner scope)"]
    W1[Child fiber 0 with private state]
    W2[Child fiber 1 with private state]
    WJ[ScopeJoin: deterministic first terminal winner]
    WR[Cancel remaining fibers and owned obligations]
    WM[Explicit merge replaces parent state]
    WC[Continue parent fiber]
    W1 --> WJ
    W2 --> WJ
    WJ --> WR
    WR --> WM --> WC
  end

  subgraph ForEach["ForEach (dynamic isolated-item scope)"]
    F1[Materialize indexed item descriptors]
    F2[Admit bounded item fibers]
    F3[Execute item body against private item state]
    FJ[ScopeJoin: ordered WhenAll or strict WhenAny]
    FM[Explicit merge replaces parent state]
    FC[Continue parent fiber]
    F1 --> F2 --> F3 --> FJ --> FM --> FC
  end
```

Fiber-owned waits and timers carry `FiberId` and `ScopeId` so events resume only
the intended fiber and recursive cancellation removes every obligation owned by
a losing or failed scope.

---

## 16. Ephemeral saga (reduced guarantee)

```mermaid
sequenceDiagram
  participant Host
  participant Engine as EphemeralWorkflowEngine
  participant Lane as InstanceExecutionLane
  participant Inst as WorkflowInstance
  participant Saga as EphemeralSagaRuntimeState

  Host->>Engine: StartSagaAsync(sagaDefinition, input)
  Engine->>Lane: RunAsync(instanceId)
  Lane->>Inst: new WorkflowInstance
  Lane->>Saga: track CompletedActions

  loop Each forward action
    Lane->>Lane: ExecuteSagaStepAsync (direct IStep, no interpreter)
    alt Step failed
      Lane->>Lane: CompensateSagaRuntimeAsync (reverse order)
    end
  end

  alt All forward actions succeeded
    Lane->>Inst: Complete()
  end

  Host->>Engine: RequestSagaCompensationAsync(instanceId)
  Engine->>Lane: CompensateSagaRuntimeAsync (idempotent if already requested)
```

No durable audit, no restart recovery. Use durable saga mode for production
compensation history.

---

## 17. Management API surface

```mermaid
flowchart TB
  M[EphemeralManagement]
  M --> All[All]
  M --> Def[ForDefinition]
  M --> One[Instance]
  M --> Many[Instances]

  All --> Q[EphemeralManagementQuery]
  Def --> Q
  Many --> Q

  Q --> List[List / Statistics / DetectStuck]
  Q --> BulkEvt[RaiseEventAsync on selection]
  Q --> BulkTerm[TerminateAsync with safety token]

  One --> IQ[EphemeralInstanceManagement]
  IQ --> Get[Get / GetState / GetActiveWaits]
  IQ --> Cancel[CancelAsync]
  IQ --> Term[TerminateAsync]
  IQ --> Step[Step path queries]

  Engine[EphemeralWorkflowEngine] -.->|internal| CancelInst
  Engine -.->|internal| TerminateInst
  IQ --> Engine
  Q --> Engine
```

---

## 18. Event delivery modes (comparison)

| Mode | API | Resolution | Use when |
|------|-----|------------|----------|
| Instance-targeted | `RaiseEventAsync(instanceId, …)` | Direct lookup in registry | Caller stores `InstanceId` |
| Correlation-targeted | `RaiseEventByCorrelationAsync(…)` | `EphemeralRoutingIndex` → exactly one active wait | One wait per (eventName, correlation) |
| Definition fanout | `RaiseEventByDefinitionAsync(definitionId, …)` | Scan registry for matching waits | Broadcast to all instances of one definition |
| Selection fanout | `Management…RaiseEventAsync(…)` | Filter then instance-targeted per match | Operator / batch tooling |

---

## 19. What ephemeral mode does not do

```mermaid
flowchart LR
  subgraph Supported["Ephemeral engine"]
    S1[In-process execution]
    S2[Wait / Delay / timers transient]
    S3[Parallel / WhenFirst / ForEach]
    S4[Management queries]
    S5[Limited in-process saga]
  end

  subgraph DurableOnly["Durable engine only"]
    D1[Restart recovery]
    D2[Long waits / durable timers]
    D3[RunChild / RunChildren]
    D4[Inbox / outbox / history]
    D5[Pause / Resume / Archive / Purge]
    D6[Durable saga audit]
  end

  Supported -.-x DurableOnly
```

---

## How to read these alongside other docs

- Usage and API examples: [ephemeral-engine-developer-guide.md](ephemeral-engine-developer-guide.md)
- Legacy state-driven engine diagrams: [architecture/engine-runtime-diagrams.md](architecture/engine-runtime-diagrams.md)
- Ephemeral vs durable orchestration split: [architecture/child-workflow-orchestration-design-v3.md](architecture/child-workflow-orchestration-design-v3.md)
- Implementation phase tasks: [implementation/phases/phase-1-ephemeral-core/README.md](implementation/phases/phase-1-ephemeral-core/README.md)
