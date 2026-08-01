# Channels And Engine Flow Diagrams

Reviewed on March 30, 2026.

## Purpose

This document explains how per-instance channels are used in:

- the current snapshot-based engine
- the event-driven prototype

It also shows where `TPL Dataflow` could fit later.

## 1. Current Engine Before Per-Instance Channels

```mermaid
flowchart TD
    Caller[Caller] --> Scope[InstanceScope.RaiseEvent]
    Scope --> Load[Load instance]
    Load --> Lock[ExecutionLock.WaitAsync]
    Lock --> Dedup{Duplicate?}
    Dedup -- Yes --> Return1[Return]
    Dedup -- No --> Match{Matching wait?}
    Match -- No --> Buffer[Add PendingEvent]
    Buffer --> Release1[ExecutionLock.Release]
    Match -- Yes --> Mark[Mark wait Matched]
    Mark --> Correlation[Remove correlation]
    Correlation --> Resume[Resume workflow]
    Resume --> Release2[ExecutionLock.Release]
```

## 2. Current Engine After Per-Instance Channels

```mermaid
flowchart TD
    Caller[Caller] --> Scope[InstanceScope.RaiseEvent]
    Scope --> Enqueue[InMemoryInstanceStore.EnqueueAsync]
    Enqueue --> Lane[InstanceCommandLane for InstanceId]
    Lane --> Worker[Single reader processes one command at a time]
    Worker --> Dedup{Duplicate?}
    Dedup -- Yes --> Return1[Return]
    Dedup -- No --> Match{Matching wait?}
    Match -- No --> Buffer[Add PendingEvent]
    Match -- Yes --> Mark[Mark wait Matched]
    Mark --> Correlation[Remove correlation]
    Correlation --> Resume[Resume workflow]
```

Key point:

- dedup did not disappear
- serialization moved earlier
- the channel is now the first in-memory gate for same-instance work

## 3. Why Channels Help The Current Engine

```mermaid
flowchart LR
    A[RaiseEvent caller A] --> Q
    B[RaiseEvent caller B] --> Q
    C[RaiseEvent caller C] --> Q
    Q[Per-instance channel] --> R[Single reader]
    R --> H[Mutate one instance in serial order]
```

Benefits:

- simpler in-process concurrency model
- less direct contention on mutation code
- fewer races around direct concurrent entry

Non-goals:

- channels do not replace runtime dedup
- channels do not replace durable correctness

## 4. Event-Driven Prototype Flow

```mermaid
flowchart TD
    Incoming[Incoming command or event] --> Lane[PrototypeInstanceCommandLane]
    Lane --> Load[Load checkpoint]
    Load --> Decide[Run definition / decide next durable facts]
    Decide --> Commit[Commit stream + checkpoint + inbox + projections]
    Commit --> Projection[Updated summary and active wait projections]
```

## 5. Event-Driven Prototype Resume Path

```mermaid
flowchart TD
    Event[EventEnvelope] --> Route[RaiseEventToInstanceAsync]
    Route --> Lane[Per-instance command lane]
    Lane --> Checkpoint[Load checkpoint]
    Checkpoint --> Dedup{EventId already known?}
    Dedup -- Yes --> Ignore[Commit duplicate ignored]
    Dedup -- No --> Wait{Matching active wait?}
    Wait -- No --> Buffer[Commit EventBuffered]
    Wait -- Yes --> Execute[Run definition from next step]
    Execute --> Commit[Commit WaitMatched / StepCompleted / WorkflowCompleted]
```

## 6. Prototype Durable Truth Boundary

```mermaid
flowchart LR
    C[Command] --> L[Per-instance lane]
    L --> A[Aggregate decision]
    A --> S[(Event stream)]
    A --> K[(Checkpoint)]
    A --> I[(Inbox)]
    A --> P[(Projections)]
```

The important rule is:

- the channel is not durable truth
- the store is durable truth

## 7. Dedup In The Event-Driven Prototype

```mermaid
flowchart TD
    E[Incoming EventId] --> C1{ConsumedEventIds contains it?}
    C1 -- Yes --> D1[Duplicate ignored]
    C1 -- No --> C2{PendingEvents contains it?}
    C2 -- Yes --> D2[Duplicate ignored]
    C2 -- No --> C3{Inbox already has it?}
    C3 -- Yes --> D3[Duplicate ignored]
    C3 -- No --> Process[Process normally]
```

## 8. Where `System.Threading.Channels` Fits Well

Good fit:

- per-instance command mailbox
- engine ingress queue
- outbox dispatch queue
- timer wake-up queue

Bad fit:

- durable event store
- durable inbox/outbox storage
- query source of truth

## 9. Where `TPL Dataflow` Fits Later

`TPL Dataflow` is not needed in the core yet, but it fits if the system grows into real asynchronous pipelines.

Good later candidates:

- outbox dispatch pipeline
- projection update fanout
- projection rebuild pipeline
- timer processing pipeline

Example future shape:

```mermaid
flowchart LR
    Events[(Committed events)] --> B[BroadcastBlock]
    B --> S1[Instance summary projector]
    B --> S2[Active wait projector]
    B --> S3[History projector]
```

Or for outbox:

```mermaid
flowchart LR
    Pending[(Pending outbox records)] --> T[TransformBlock dispatch attempt]
    T --> A[ActionBlock mark dispatched or failed]
```

## 10. Practical Recommendation

Use:

- `System.Threading.Channels` for in-process serialized command lanes
- `TPL Dataflow` only when you truly have a multi-stage pipeline

Do not:

- build the core engine around Rx or Dataflow first
- confuse in-memory queueing with durable truth

The current state of the project matches that recommendation:

- channels are now useful in the current engine and the prototype
- Dataflow remains an optional later optimization for projection/outbox/timer pipelines
