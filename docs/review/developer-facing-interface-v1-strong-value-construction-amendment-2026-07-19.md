# Developer-facing interface v1 strong-value construction amendment (2026-07-19)

Status: **owner decision applied to planning artifacts; no product or guard implementation is
claimed**.

This post-review amendment records one greenfield public-API decision made after the independent
v1 planning review dated 2026-07-19. The 2026-07-18 simplification amendment, its reviewer prompt,
and the independent review remain immutable historical evidence. Constructor-shaped examples in
those dated artifacts describe the baseline that was reviewed; they are not edited in place.

The authoritative signature baseline remains
[`17-selected-mode-capability-matrix.md`](../specs/17-selected-mode-capability-matrix.md), and the
active implementation checklist remains the OpenSpec change
[`reshape-developer-facing-interfaces`](../../openspec/changes/reshape-developer-facing-interfaces/tasks.md).

## 1. Decision

Every caller-created string-backed strong value uses a private constructor and exactly one public
construction member:

```csharp
public sealed class EventName : IEquatable<EventName>
{
    private EventName(string value);
    public string Value { get; }
    public static EventName Create(string value);
}
```

The rule applies uniformly to:

- `EventName`;
- `WorkflowOutcomeName`;
- `AuthoredBranchId`;
- `DagNodeId`;
- `ResourcePoolName`;
- `TransientPoolName`;
- `StartIdempotencyKey`;
- `CorrelationId`;
- `EventId`;
- `StopConfirmationId`;
- `ResourcePoolOperationId`;
- `ResourceGovernancePartitionId`.

These types expose no public constructor, `New`, `Parse`/`TryParse`, implicit conversion,
raw-string overload, or construction alias. `Create` rejects null, empty, all-whitespace, and
leading/trailing-whitespace input. It does not trim, case-fold, or otherwise normalize the value;
equality and hashing remain exact ordinal.

## 2. Runtime and definition identities stay distinct

Runtime-created `InstanceId`, `WaitId`, `StepOperationId`, `LeaseProtectionToken`, and `DagRunId`
retain private construction plus their canonical `Parse`/`TryParse` boundary. They expose no
caller-facing `Create` factory. Internal compiler/runtime projections such as `AuthoredLocation`,
`DefinitionFingerprint`, and `PayloadFingerprint` remain internally constructed.

`DefinitionId` retains `New` plus `Parse`/`TryParse`, because the application asks OrcaCore to
generate that identity and may reconstruct it at a boundary. `DefinitionVersion` retains its
validating integer constructor and `Initial` value.

This split makes intent visible at the call site:

```csharp
var eventName = EventName.Create("JobDone");
var correlationId = CorrelationId.Create(jobKey);
var eventId = EventId.Create(reportId);

var instanceId = InstanceId.Parse(persistedInstanceId);
```

## 3. Guard and implementation consequences

OpenSpec tasks 3.1, 3.5, 4.2, and 10.4 now require signature, source, reflection, and consumer
guards for this exact family split. In particular, `new EventName("JobDone")`, public constructors,
factory aliases, caller-created `Parse` members, and runtime-created `Create` members are rejected.
JSON/protocol converters may reconstruct values internally, but do not widen the public surface.

No compatibility alias or obsolete tombstone is retained: OrcaCore is greenfield and nothing has
shipped.

## 4. Review disposition

This amendment does not overturn or close any finding in the independent
[`2026-07-19 v1 simplification review`](developer-facing-interface-v1-simplification-review-2026-07-19.md).
Its **APPROVE WITH CHANGES** planning verdict and **NOT READY** guard-retarget verdict remain the
current review outcome. The construction-family delta must be included when the outstanding
review findings are remediated and the packet is submitted for re-review.

