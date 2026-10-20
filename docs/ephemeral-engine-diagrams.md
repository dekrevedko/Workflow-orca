# OrcaCore Ephemeral Engine Runtime Diagrams

These diagrams show the selected v1 flow at application-contract level. Internal scheduling and
compiler types are intentionally omitted. See the
[ephemeral engine developer guide](ephemeral-engine-developer-guide.md) and the
[selected-mode capability matrix](specs/17-selected-mode-capability-matrix.md) for exact semantics.

## Author, register, and start

```mermaid
flowchart LR
    A["Workflow.Ephemeral<TState>"] --> B["Build or TryBuild"]
    B --> C["IWorkflowDefinitionRegistry.Register"]
    C --> D{"Closed registration result"}
    D -->|Registered| E["Typed ephemeral definition handle"]
    D -->|Host incompatible| F["Inspect missing capability facts"]
    D -->|Fingerprint conflict| G["Inspect existing binding"]
    E --> H["StartOrGetAsync"]
    H --> I["WorkflowInstanceHandle"]
```

Shutdown contract addition (implementation pending): the existing role owns one
lifetime singleton; no extra registration or option is introduced. Local wait-token
cancellation remains separate from host cancellation. An initialization admitted
before close can finish and become Accepted while Closed; queued pre-Init rejects.

```mermaid
flowchart LR
    O[Open] -->|ApplicationStopping; no cancellation| C[Closed]
    C --> D[Initialized pending starts and accepted work drain]
    C -->|Engine hosted or public StopAsync| S[Stopping]
    O -->|Direct StopAsync closes first| S
    S --> J[Cancel remaining work; seal and join]
    J -->|Stable zero| F[Completed]
    J -->|Budget or fault| I[Incomplete; retain actual owner]
    I --> J
```

Closed permits events, waits, timers and retries. Acceptance/abort elects at Stopping,
not at the Closed edge. Completed permits no new live engine work; detached values
and cached stop results are separate from runtime queries. Required source tests
and candidate-package drain consumers remain pending.

## One serialized instance lane

```mermaid
flowchart TD
    Q["Instance work queue"] --> L["Serialized instance lane"]
    L --> S["Run one bounded scheduler quantum"]
    S --> C{"Workflow or in-memory suspension boundary reached?"}
    C -->|No| S
    C -->|Wait, delay, join, or terminal| P["Publish committed in-memory detached snapshot"]
    P --> R["Release path token"]
    R --> Q
```

Terminal retention (MG-054–058; implemented and qualified with fresh local consumer packages) does not treat a
published terminal snapshot as proof that every physical step has settled. Accepted active,
waiting, delayed and terminal-but-unsettled instances remain protected. An aborted provisional
start is never published; internal fencing precedes settlement cleanup even with retention off.
An accepted start also does not pin a later output wait: retirement can win before admission.

The ephemeral engine keeps one mutation lane per instance. Structured branch and item work may be
interleaved cooperatively, but user step bodies for one instance do not execute concurrently.

## Fixed fan-out and merge

```mermaid
flowchart TD
    P["Parent detached state"] --> A["Authored branch 0 copy"]
    P --> B["Authored branch 1 copy"]
    A --> RA["Typed branch result 0"]
    B --> RB["Typed branch result 1"]
    RA --> O["Authored-order result set"]
    RB --> O
    O --> M["Explicit parent merge"]
    M --> N["One parent continuation"]
```

Branches cannot mutate the parent or one another. Failure, cancellation, or deadline termination
suppresses the merge.

## Wait and durable ingress boundary

```mermaid
sequenceDiagram
    participant E as Ephemeral workflow
    participant W as Process-local wait registry
    participant I as Durable IWorkflowEventIngress
    participant D as Durable inbox/continuation
    E->>W: Register EventName + CorrelationId
    E-->>E: Park and release execution path
    Note over E,W: Ephemeral wait has no durable acknowledgement promise
    I->>D: Accept direct/correlation/fanout/start-or-deliver event
    D-->>I: Accepted or Duplicate after durable ownership
    D-->>D: Claim when the matching wait becomes available
```

## Item context

```mermaid
flowchart LR
    A["ForEach item admitted by index"] --> B["Detached item state"]
    B --> C["StepContext.ForEachItem.Index"]
    C --> D["Typed item result"]
    D --> E["Deterministic join input"]
```

Outside an admitted item body, `StepContext.ForEachItem` is `null`.

The superseded pre-v1 diagrams are preserved, unchanged, at
[`archive/plans/ephemeral-engine-diagrams.md`](archive/plans/ephemeral-engine-diagrams.md).
