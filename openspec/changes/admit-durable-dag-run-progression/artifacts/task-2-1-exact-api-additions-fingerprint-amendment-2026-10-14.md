# Proposed DAG run API: exact baseline additions

Date: 2026-10-14. Fingerprint storage-boundary amendment; PROPOSED, not current exports or approved baseline updates.

This catalog replaces ONLY the six typed-fingerprint constructor/property/Deconstruct keys of the immutable RC-3 catalog. It retains the other 359 keys and the 7 / 193 / 165 = 365 totals. The original comparison base is retained (before RC-3); the seven OrcaCore keys have already landed at c93411e. The new source-boundary rule is in task-2-1-fingerprint-contract-addendum-2026-10-14.md. No public baseline is updated here.

The separate metadata prototype copies only OrcaCore, Runtime.Protocol and Provider.Abstractions
sources into an ignored workspace; production src/** and the fourteen approved API
baselines are untouched. Its internal block includes the unchanged six-call child
contract plus the explicitly proposed CapacityDeferred variant. The proof is NOT
runtime implementation or behavioral evidence: throwing bodies stand in for new ports.
It compiles with the repository global.json/Directory.Build.props, Release
-warnaserror --no-incremental, zero warnings/errors. The formatter is the repository
PublicApiBaseline.FormatAssembly and its helpers, copied byte-exact. Capture the
three resulting assemblies with CaptureAssemblyForTesting; key each formatted line
by its preceding type declaration. Subtract the same type/line keys in the committed
approved baseline. Every existing key must remain. The additions below are exact
baseline-format lines, including compiler-generated record members, not hand summaries.

## OrcaCore

Approved baseline SHA-256: `ef13f660beecb09c88c0d22b09abae7b69332158d8fcea4b7da69ad5e0a32678`. Proposed full extraction
SHA-256: `a8bb583860fd8744b018273911fe65b1c899926d70e501efb773ccfbf5588f3e`. Removed keys: 0. Added keys: 7.

```text
type public enum OrcaCore.Abstractions.Ids.DurableAggregateKind : [System.Private.CoreLib]System.Int32
  field public [System.Private.CoreLib]System.Int32 value__
  field public const [OrcaCore]OrcaCore.Abstractions.Ids.DurableAggregateKind DagRun = 1
  field public const [OrcaCore]OrcaCore.Abstractions.Ids.DurableAggregateKind Workflow = 0
type public readonly struct OrcaCore.Abstractions.Ids.WorkflowStreamId : [System.Private.CoreLib]System.ValueType~ interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore]OrcaCore.Abstractions.Ids.WorkflowStreamId]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.IsReadOnlyAttribute(), [System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  method public static ForDagRun([System.Private.CoreLib]System.Guid runId) -> [OrcaCore]OrcaCore.Abstractions.Ids.WorkflowStreamId
  property [OrcaCore]OrcaCore.Abstractions.Ids.DurableAggregateKind AggregateKind { get:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Guid] DagRunId { get:public }
```

## OrcaCore.Runtime.Protocol

Approved baseline SHA-256: `ed710c565b5e39186b1569949f9a8eb4eea201f215fd9b97cb5961c5c85be5d1`. Proposed full extraction
SHA-256: `703536ec79dd945894ec58ac9593a90cf3b81dca13c5013e12f503e18a223a50`. Removed keys: 0. Added keys: 193.

```text
type public enum OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 : [System.Private.CoreLib]System.Int32
  field public [System.Private.CoreLib]System.Int32 value__
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 CancellationRequested = 3
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 Cancelled = 6
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 DependencyBlocked = 7
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 Failed = 5
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 Pending = 0
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 Ready = 1
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 StartAuthorized = 2
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 Succeeded = 4
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([OrcaCore]OrcaCore.DefinitionId! DefinitionId, [OrcaCore]OrcaCore.DefinitionVersion! DefinitionVersion, [System.Private.CoreLib]System.String! Fingerprint)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1!
  method public Deconstruct(out [OrcaCore]OrcaCore.DefinitionId! DefinitionId, out [OrcaCore]OrcaCore.DefinitionVersion! DefinitionVersion, out [System.Private.CoreLib]System.String! Fingerprint) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1? right) -> [System.Private.CoreLib]System.Boolean
  property [System.Private.CoreLib]System.String! Fingerprint { get:public; init:public }
  property [OrcaCore]OrcaCore.DefinitionId! DefinitionId { get:public; init:public }
  property [OrcaCore]OrcaCore.DefinitionVersion! DefinitionVersion { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Int32 Version, [System.Private.CoreLib]System.Guid RunId, [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion, [System.Private.CoreLib]System.String! ContentType, [System.Private.CoreLib]System.Byte[]! Payload, [System.Private.CoreLib]System.String! Sha256)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Int32 Version, out [System.Private.CoreLib]System.Guid RunId, out [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion, out [System.Private.CoreLib]System.String! ContentType, out [System.Private.CoreLib]System.Byte[]! Payload, out [System.Private.CoreLib]System.String! Sha256) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion { get:public; init:public }
  property [System.Private.CoreLib]System.Byte[]! Payload { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RunId { get:public; init:public }
  property [System.Private.CoreLib]System.Int32 Version { get:public; init:public }
  property [System.Private.CoreLib]System.String! ContentType { get:public; init:public }
  property [System.Private.CoreLib]System.String! Sha256 { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1! Definition, [System.Private.CoreLib]System.String! RunInputContractKey, [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1!]! Nodes)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1!
  method public Deconstruct(out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1! Definition, out [System.Private.CoreLib]System.String! RunInputContractKey, out [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1!]! Nodes) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1! Definition { get:public; init:public }
  property [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1!]! Nodes { get:public; init:public }
  property [System.Private.CoreLib]System.String! RunInputContractKey { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Int32 Ordinal, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 State, [OrcaCore]OrcaCore.InstanceId? ChildInstanceId, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? InputWrite, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? OutputWrite, [System.Private.CoreLib]System.String? FailureCode, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] ReadyAt, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] StartedAt, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Int32 Ordinal, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 State, out [OrcaCore]OrcaCore.InstanceId? ChildInstanceId, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? InputWrite, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? OutputWrite, out [System.Private.CoreLib]System.String? FailureCode, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] ReadyAt, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] StartedAt, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 State { get:public; init:public }
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? InputWrite { get:public; init:public }
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? OutputWrite { get:public; init:public }
  property [OrcaCore]OrcaCore.InstanceId? ChildInstanceId { get:public; init:public }
  property [System.Private.CoreLib]System.Int32 Ordinal { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] ReadyAt { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] StartedAt { get:public; init:public }
  property [System.Private.CoreLib]System.String? FailureCode { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Int32 Ordinal, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 State, [OrcaCore]OrcaCore.InstanceId? ChildInstanceId, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? Input, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? Output, [System.Private.CoreLib]System.String? FailureCode, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] ReadyAt, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] StartedAt, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Int32 Ordinal, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 State, out [OrcaCore]OrcaCore.InstanceId? ChildInstanceId, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? Input, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? Output, out [System.Private.CoreLib]System.String? FailureCode, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] ReadyAt, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] StartedAt, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagNodeStateV1 State { get:public; init:public }
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? Input { get:public; init:public }
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? Output { get:public; init:public }
  property [OrcaCore]OrcaCore.InstanceId? ChildInstanceId { get:public; init:public }
  property [System.Private.CoreLib]System.Int32 Ordinal { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] ReadyAt { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] StartedAt { get:public; init:public }
  property [System.Private.CoreLib]System.String? FailureCode { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Int32 Ordinal, [System.Private.CoreLib]System.String! NodeId, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1! Workflow, [System.Private.CoreLib]System.String! InputContractKey, [System.Private.CoreLib]System.String? OutputContractKey attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)], [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[System.Private.CoreLib]System.Int32]! DirectDependencies)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Int32 Ordinal, out [System.Private.CoreLib]System.String! NodeId, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1! Workflow, out [System.Private.CoreLib]System.String! InputContractKey, out [System.Private.CoreLib]System.String? OutputContractKey attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)], out [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[System.Private.CoreLib]System.Int32]! DirectDependencies) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1! Workflow { get:public; init:public }
  property [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[System.Private.CoreLib]System.Int32]! DirectDependencies { get:public; init:public }
  property [System.Private.CoreLib]System.Int32 Ordinal { get:public; init:public }
  property [System.Private.CoreLib]System.String! InputContractKey { get:public; init:public }
  property [System.Private.CoreLib]System.String! NodeId { get:public; init:public }
  property [System.Private.CoreLib]System.String? OutputContractKey { get:public; init:public } attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)]
type public enum OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 : [System.Private.CoreLib]System.Int32
  field public [System.Private.CoreLib]System.Int32 value__
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 CancellationRequested = 4
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 ChildJoined = 3
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 Created = 0
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 InputAdmitted = 1
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 IntegrityFaulted = 6
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 MappingFailed = 2
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 PoisonReconciled = 7
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 Terminal = 5
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Int32 Version, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 Kind, [System.Private.CoreLib]System.Guid RunId, [System.Private.CoreLib]System.Guid OperationId, [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 State, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1? CreationSnapshot attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)], [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1!]! ChangedNodes, [System.Private.CoreLib]System.DateTimeOffset UpdatedAt, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Int32 Version, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 Kind, out [System.Private.CoreLib]System.Guid RunId, out [System.Private.CoreLib]System.Guid OperationId, out [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 State, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1? CreationSnapshot attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)], out [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1!]! ChangedNodes, out [System.Private.CoreLib]System.DateTimeOffset UpdatedAt, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordKindV1 Kind { get:public; init:public }
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1? CreationSnapshot { get:public; init:public } attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)]
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 State { get:public; init:public }
  property [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion { get:public; init:public }
  property [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeChangeV1!]! ChangedNodes { get:public; init:public }
  property [System.Private.CoreLib]System.DateTimeOffset UpdatedAt { get:public; init:public }
  property [System.Private.CoreLib]System.Guid OperationId { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RunId { get:public; init:public }
  property [System.Private.CoreLib]System.Int32 Version { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Guid RunId, [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1! StartKey, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1! Graph, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1! RunInput, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 State, [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1!]! Nodes, [System.Private.CoreLib]System.DateTimeOffset CreatedAt, [System.Private.CoreLib]System.DateTimeOffset UpdatedAt, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Guid RunId, out [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1! StartKey, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1! Graph, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1! RunInput, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 State, out [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1!]! Nodes, out [System.Private.CoreLib]System.DateTimeOffset CreatedAt, out [System.Private.CoreLib]System.DateTimeOffset UpdatedAt, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunSnapshotV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunGraphV1! Graph { get:public; init:public }
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1! StartKey { get:public; init:public }
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 State { get:public; init:public }
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1! RunInput { get:public; init:public }
  property [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion { get:public; init:public }
  property [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunNodeStateV1!]! Nodes { get:public; init:public }
  property [System.Private.CoreLib]System.DateTimeOffset CreatedAt { get:public; init:public }
  property [System.Private.CoreLib]System.DateTimeOffset UpdatedAt { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RunId { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] CompletedAt { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.String! Value)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1!
  method public Deconstruct(out [System.Private.CoreLib]System.String! Value) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1? right) -> [System.Private.CoreLib]System.Boolean
  property [System.Private.CoreLib]System.String! Value { get:public; init:public }
type public enum OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 : [System.Private.CoreLib]System.Int32
  field public [System.Private.CoreLib]System.Int32 value__
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 CancellationRequested = 1
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 Cancelled = 4
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 Failed = 3
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 IntegrityFault = 5
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 Running = 0
  field public const [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 Succeeded = 2
type public sealed class OrcaCore.Abstractions.Durable.Dag.DagRunValueV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Boolean IsPresent, [System.Private.CoreLib]System.String! ContractKey, [System.Private.CoreLib]System.String! CodecFormat, [System.Private.CoreLib]System.Byte[]! Bytes, [System.Private.CoreLib]System.String! Fingerprint)
  method public <Clone>$() -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Boolean IsPresent, out [System.Private.CoreLib]System.String! ContractKey, out [System.Private.CoreLib]System.String! CodecFormat, out [System.Private.CoreLib]System.Byte[]! Bytes, out [System.Private.CoreLib]System.String! Fingerprint) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? left, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunValueV1? right) -> [System.Private.CoreLib]System.Boolean
  property [System.Private.CoreLib]System.String! Fingerprint { get:public; init:public }
  property [System.Private.CoreLib]System.Boolean IsPresent { get:public; init:public }
  property [System.Private.CoreLib]System.Byte[]! Bytes { get:public; init:public }
  property [System.Private.CoreLib]System.String! CodecFormat { get:public; init:public }
  property [System.Private.CoreLib]System.String! ContractKey { get:public; init:public }
type public static class OrcaCore.Abstractions.Serialization.DagRunRecordCodecV1 attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  method public static Deserialize([System.Private.CoreLib]System.Byte[]! payload) -> [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1!
  method public static Serialize([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1! record) -> [System.Private.CoreLib]System.Byte[]!
```

## OrcaCore.Provider.Abstractions

Approved baseline SHA-256: `d9549da7045c7bfc7ef2e56d56e5c6f12d422ccd10107bfe0edcfa06d057820e`. Proposed full extraction
SHA-256: `225f3481c5cb3f8dcdbe98cad7629d80e9c472e97f262c516cdbcb4bbb28a579`. Removed keys: 0. Added keys: 165.

```text
type public sealed class OrcaCore.Abstractions.Providers.Dag.DagRunAppendV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunAppendV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Guid OperationId, [System.Private.CoreLib]System.String! OperationFingerprint, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? Fence attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)], [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1!]! Records, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1! Checkpoint)
  method public <Clone>$() -> [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunAppendV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Guid OperationId, out [System.Private.CoreLib]System.String! OperationFingerprint, out [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? Fence attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)], out [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1!]! Records, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1! Checkpoint) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunAppendV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunAppendV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunAppendV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunAppendV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunAppendV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? Fence { get:public; init:public } attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)]
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1! Checkpoint { get:public; init:public }
  property [System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1!]! Records { get:public; init:public }
  property [System.Private.CoreLib]System.Guid OperationId { get:public; init:public }
  property [System.Private.CoreLib]System.String! OperationFingerprint { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryRequestV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryRequestV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Int64 AfterCreationSequence, [System.Private.CoreLib]System.Int32 MaxCount)
  method public <Clone>$() -> [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryRequestV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Int64 AfterCreationSequence, out [System.Private.CoreLib]System.Int32 MaxCount) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryRequestV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryRequestV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryRequestV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryRequestV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryRequestV1? right) -> [System.Private.CoreLib]System.Boolean
  property [System.Private.CoreLib]System.Int32 MaxCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 AfterCreationSequence { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Int64 CreationSequence, [System.Private.CoreLib]System.Guid RunId, [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1! Definition, [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 State)
  method public <Clone>$() -> [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Int64 CreationSequence, out [System.Private.CoreLib]System.Guid RunId, out [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1! Definition, out [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 State) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunBindingV1! Definition { get:public; init:public }
  property [OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStateV1 State { get:public; init:public }
  property [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion StreamVersion { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RunId { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 CreationSequence { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Guid RunId, [System.Private.CoreLib]System.Guid OwnerId, [System.Private.CoreLib]System.Int64 Token, [System.Private.CoreLib]System.Guid RequestId)
  method public <Clone>$() -> [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Guid RunId, out [System.Private.CoreLib]System.Guid OwnerId, out [System.Private.CoreLib]System.Int64 Token, out [System.Private.CoreLib]System.Guid RequestId) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? right) -> [System.Private.CoreLib]System.Boolean
  property [System.Private.CoreLib]System.Guid OwnerId { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RequestId { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RunId { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 Token { get:public; init:public }
type public enum OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 : [System.Private.CoreLib]System.Int32
  field public [System.Private.CoreLib]System.Int32 value__
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 Granted = 0
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 NotFound = 4
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 OwnerBusy = 2
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 Queued = 1
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 StaleFence = 3
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 Terminal = 5
type public enum OrcaCore.Abstractions.Providers.Dag.DagRunLeasePurposeV1 : [System.Private.CoreLib]System.Int32
  field public [System.Private.CoreLib]System.Int32 value__
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeasePurposeV1 Admission = 0
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeasePurposeV1 ChildJoin = 1
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeasePurposeV1 IntegrityReconciliation = 3
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeasePurposeV1 RunCancellation = 2
type public sealed class OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Guid RunId, [System.Private.CoreLib]System.Guid OwnerId, [System.Private.CoreLib]System.Guid RequestId, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeasePurposeV1 Purpose, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Int64] ExpectedFence, [System.Private.CoreLib]System.TimeSpan LeaseDuration)
  method public <Clone>$() -> [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Guid RunId, out [System.Private.CoreLib]System.Guid OwnerId, out [System.Private.CoreLib]System.Guid RequestId, out [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeasePurposeV1 Purpose, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Int64] ExpectedFence, out [System.Private.CoreLib]System.TimeSpan LeaseDuration) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeasePurposeV1 Purpose { get:public; init:public }
  property [System.Private.CoreLib]System.Guid OwnerId { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RequestId { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RunId { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Int64] ExpectedFence { get:public; init:public }
  property [System.Private.CoreLib]System.TimeSpan LeaseDuration { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 Outcome, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? Fence, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] ExpiresAt, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] HardDeadline, [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Int64] QueueSequence, [System.Private.CoreLib]System.Nullable`1[[OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion] CurrentVersion)
  method public <Clone>$() -> [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1!
  method public Deconstruct(out [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 Outcome, out [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? Fence, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] ExpiresAt, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] HardDeadline, out [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Int64] QueueSequence, out [System.Private.CoreLib]System.Nullable`1[[OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion] CurrentVersion) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1? Fence { get:public; init:public }
  property [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 Outcome { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion] CurrentVersion { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] ExpiresAt { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.DateTimeOffset] HardDeadline { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Int64] QueueSequence { get:public; init:public }
type public enum OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeOutcomeV1 : [System.Private.CoreLib]System.Int32
  field public [System.Private.CoreLib]System.Int32 value__
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeOutcomeV1 AlreadyChanged = 1
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeOutcomeV1 NotFound = 2
  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeOutcomeV1 Subscribed = 0
type public sealed class OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeResultV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeResultV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeOutcomeV1 Outcome, [System.Private.CoreLib]System.Nullable`1[[OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion] CurrentVersion)
  method public <Clone>$() -> [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeResultV1!
  method public Deconstruct(out [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeOutcomeV1 Outcome, out [System.Private.CoreLib]System.Nullable`1[[OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion] CurrentVersion) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeResultV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeResultV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeResultV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeResultV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeResultV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeOutcomeV1 Outcome { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion] CurrentVersion { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Providers.Dag.DagRunSubscriptionV1 interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscriptionV1~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  ctor public .ctor([System.Private.CoreLib]System.Guid SubscriptionId, [System.Private.CoreLib]System.Guid RecipientHostId, [System.Private.CoreLib]System.Guid RunId, [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion AfterVersion, [System.Private.CoreLib]System.TimeSpan LeaseDuration)
  method public <Clone>$() -> [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscriptionV1!
  method public Deconstruct(out [System.Private.CoreLib]System.Guid SubscriptionId, out [System.Private.CoreLib]System.Guid RecipientHostId, out [System.Private.CoreLib]System.Guid RunId, out [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion AfterVersion, out [System.Private.CoreLib]System.TimeSpan LeaseDuration) -> [System.Private.CoreLib]System.Void
  method public override Equals([System.Private.CoreLib]System.Object? obj) -> [System.Private.CoreLib]System.Boolean
  method public override GetHashCode() -> [System.Private.CoreLib]System.Int32
  method public override ToString() -> [System.Private.CoreLib]System.String!
  method public virtual sealed Equals([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscriptionV1? other) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Equality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscriptionV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscriptionV1? right) -> [System.Private.CoreLib]System.Boolean
  operator public static op_Inequality([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscriptionV1? left, [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscriptionV1? right) -> [System.Private.CoreLib]System.Boolean
  property [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion AfterVersion { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RecipientHostId { get:public; init:public }
  property [System.Private.CoreLib]System.Guid RunId { get:public; init:public }
  property [System.Private.CoreLib]System.Guid SubscriptionId { get:public; init:public }
  property [System.Private.CoreLib]System.TimeSpan LeaseDuration { get:public; init:public }
type public interface OrcaCore.Abstractions.Providers.IWorkflowEventStore
  method public abstract AcquireDagRunLeaseAsync([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1! request, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1!]!
  method public abstract CancelDagRunLeaseRequestAsync([System.Private.CoreLib]System.Guid runId, [System.Private.CoreLib]System.Guid ownerId, [System.Private.CoreLib]System.Guid requestId, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task!
  method public abstract GetDagRunByStartKeyAsync([OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunStartKeyV1! startKey, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task`1[[OrcaCore]OrcaCore.Abstractions.Primitives.Option`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1!]]! return-attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute([1, 0, 1])]
  method public abstract ListRecoverableDagRunsAsync([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryRequestV1! request, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task`1[[System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunDiscoveryV1!]!]!
  method public abstract LoadDagRunCheckpointAsync([OrcaCore]OrcaCore.Abstractions.Ids.WorkflowStreamId streamId, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task`1[[OrcaCore]OrcaCore.Abstractions.Primitives.Option`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunCheckpointV1!]]! return-attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute([1, 0, 1])]
  method public abstract LoadDagRunTailAsync([OrcaCore]OrcaCore.Abstractions.Ids.WorkflowStreamId streamId, [OrcaCore]OrcaCore.Abstractions.Ids.StreamVersion afterVersion, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task`1[[System.Private.CoreLib]System.Collections.Generic.IReadOnlyList`1[[OrcaCore.Runtime.Protocol]OrcaCore.Abstractions.Durable.Dag.DagRunRecordV1!]!]!
  method public abstract PruneExpiredDagRunObserversAsync([System.Private.CoreLib]System.Int32 maxCount, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task`1[[System.Private.CoreLib]System.Int32]!
  method public abstract ReleaseDagRunLeaseAsync([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunFenceV1! fence, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1!]!
  method public abstract RenewDagRunLeaseAsync([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseRequestV1! request, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseResultV1!]!
  method public abstract SubscribeDagRunAsync([OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscriptionV1! subscription, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunSubscribeResultV1!]!
  method public abstract UnsubscribeDagRunAsync([System.Private.CoreLib]System.Guid subscriptionId, [System.Private.CoreLib]System.Guid recipientHostId, [System.Private.CoreLib]System.Threading.CancellationToken cancellationToken) -> [System.Private.CoreLib]System.Threading.Tasks.Task!
type public sealed class OrcaCore.Abstractions.Providers.OutboxClaimRequest interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.OutboxClaimRequest~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Guid] RecipientHostId { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Providers.OutboxWrite interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.OutboxWrite~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Guid] DagRunOwner { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Guid] DagRunSubscriptionId { get:public; init:public }
  property [System.Private.CoreLib]System.Nullable`1[[System.Private.CoreLib]System.Guid] RecipientHostId { get:public; init:public }
type public sealed class OrcaCore.Abstractions.Providers.ProviderCommitBatch interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.ProviderCommitBatch~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0), [System.Private.CoreLib]System.Runtime.CompilerServices.RequiredMemberAttribute()]
  property [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunAppendV1? DagRun { get:public; init:public } attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)]
type public sealed class OrcaCore.Abstractions.Providers.WorkflowOperationalPressure interfaces [[System.Private.CoreLib]System.IEquatable`1[[OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.WorkflowOperationalPressure~]~] attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(0)]
  property [System.Private.CoreLib]System.Int64 ActiveDagObserverCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 ActiveDagRunCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 DagObserverClaimedCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 DagObserverPendingCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 DagObserverPoisonedCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 DagObserverRetryableCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 DagRunCheckpointCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 DagRunCheckpointLag { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 DagRunStreamEventCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 ExpiredDagObserverCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 InternalDagClaimedCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 InternalDagPendingCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 InternalDagPoisonedCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 InternalDagRetryableCount { get:public; init:public }
  property [System.Private.CoreLib]System.Int64 StuckDagRunCount { get:public; init:public }
```

## Exact existing declarations, before / after

WorkflowStreamId is specified before/after in the main contract. ProviderCommitBatch
BEFORE consists of the following public declarations (in their original type):

```csharp
public sealed record ProviderCommitBatch
{
    public required WorkflowStreamId StreamId { get; init; }
    public required StreamVersion ExpectedVersion { get; init; }
    public IReadOnlyList<DurableWorkflowEvent> Events { get; init; }
    public CheckpointWrite? Checkpoint { get; init; }
    public IReadOnlyList<InboxWrite> InboxOperations { get; init; }
    public IReadOnlyList<InboxRouteMutation> InboxRouteMutations { get; init; }
    public IReadOnlyList<InboxTargetPoisonWrite> InboxTargetPoisonOperations { get; init; }
    public IReadOnlyList<OutboxWrite> OutboxRecords { get; init; }
    public IReadOnlyList<ProjectionWrite> ProjectionOperations { get; init; }
    public IReadOnlyList<TimerScheduleRequest> TimerSchedules { get; init; }
    public IReadOnlyList<StartIdempotencyWrite> StartIdempotencyOperations { get; init; }
}
```

AFTER adds exactly `public DagRunAppendV1? DagRun { get; init; }`; all the
above remain. BEFORE OutboxWrite has its existing (OutboxRecordId, string, byte[])
constructor, three get/init positional properties and `int DispatchAttempt { get; init; }`.
AFTER adds exactly `Guid? DagRunOwner { get; init; }` and
`Guid? RecipientHostId { get; init; }` and `Guid? DagRunSubscriptionId { get; init; }`. BEFORE OutboxClaimRequest has its existing
(int MaxCount, DateTimeOffset ClaimedAt, TimeSpan LeaseDuration) constructor, three
get/init positional properties and `OutboxKindSelector? KindSelector { get; init; }`;
AFTER adds exactly `Guid? RecipientHostId { get; init; }`.

IWorkflowEventStore BEFORE has exactly these methods; AFTER retains all three
and adds precisely the eleven methods in main-contract section 3, with the same
parameter names/types/return nullability, all abstract:

```csharp
Task<Option<CheckpointWrite>> LoadCheckpointAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken);

    Task<Result<AppendEventsResult>> AppendAsync(
        ProviderCommitBatch batch,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
        WorkflowStreamId streamId,
        StreamVersion afterVersion,
        CancellationToken cancellationToken);
```

WorkflowOperationalPressure BEFORE (all fourteen remain):

```csharp
public sealed record WorkflowOperationalPressure
{
    public long ActiveInstanceCount { get; init; }

    public long StuckInstanceCount { get; init; }

    public long ActiveWaitCount { get; init; }

    public long StreamEventCount { get; init; }

    public long CheckpointCount { get; init; }

    public long CheckpointLag { get; init; }

    public long ContinuationPendingCount { get; init; }

    public long ContinuationRetryableCount { get; init; }

    public long ContinuationClaimedCount { get; init; }

    public long ContinuationPoisonedCount { get; init; }

    public long ExternalOutboxPendingCount { get; init; }

    public long ExternalOutboxRetryableCount { get; init; }

    public long ExternalOutboxClaimedCount { get; init; }

    public long ExternalOutboxPoisonedCount { get; init; }
}
```

AFTER adds exactly these fifteen properties; operational interface signatures remain unchanged:

```csharp
public long InternalDagPendingCount { get; init; }
public long InternalDagRetryableCount { get; init; }
public long InternalDagClaimedCount { get; init; }
public long InternalDagPoisonedCount { get; init; }
public long DagObserverPendingCount { get; init; }
public long DagObserverRetryableCount { get; init; }
public long DagObserverClaimedCount { get; init; }
public long DagObserverPoisonedCount { get; init; }
public long ActiveDagObserverCount { get; init; }
public long ExpiredDagObserverCount { get; init; }
public long ActiveDagRunCount { get; init; }
public long StuckDagRunCount { get; init; }
public long DagRunStreamEventCount { get; init; }
public long DagRunCheckpointCount { get; init; }
public long DagRunCheckpointLag { get; init; }
```

WorkflowEventCodec BEFORE == AFTER, with EXACTLY these entries (CLR name and
discriminator text are equal); no DAG additions to this table:

```text
WorkflowStartedEvent
WorkflowContinuedAsNewEvent
WorkflowStepCompletedEvent
WorkflowStepFailedEvent
WorkflowWaitRegisteredEvent
WorkflowWaitMatchedEvent
WorkflowWaitCancelledEvent
WorkflowTimerCancelledEvent
WorkflowResumeConsumedEvent
WorkflowParkedEvent
WorkflowUnparkedEvent
WorkflowContinuationAttemptFailedEvent
WorkflowContinuationAttemptResetEvent
WorkflowTimerScheduledEvent
WorkflowTimerFiredEvent
WorkflowResourcePoolAcquiredEvent
WorkflowResourcePoolQueuedEvent
WorkflowResourcePoolReleasedEvent
WorkflowCompletedEvent
WorkflowCancellationRequestedEvent
WorkflowTerminalEvent
```

Its existing public signatures are unchanged:

```csharp
public static string ToEventType(WorkflowEvent workflowEvent);
public static string Serialize(WorkflowEvent workflowEvent);
public static WorkflowEvent Deserialize(string eventType, string payload);
public static IReadOnlyDictionary<string, string> EventTypeNamesByClrTypeName { get; }
```

The separate proposed Runtime.Protocol codec is in OrcaCore.Abstractions.Serialization:

```csharp
public static class DagRunRecordCodecV1
{
    public static byte[] Serialize(DagRunRecordV1 record);
    public static DagRunRecordV1 Deserialize(byte[] payload);
}
```

Total added public API keys: 365. The prototype adds no public members
to Dag, Dag.Hosting, Durable.Hosting or Core, and adds no friend. Task 2.1 must
compare implemented exports against this exact additive catalog before approving
new package baselines; no routine fixture refresh may broaden it.

## Fingerprint amendment scope

DagRunBindingV1.Fingerprint and DagRunValueV1.Fingerprint are non-null System.String. Each constructor, init property and generated Deconstruct parameter changes accordingly. All enclosing graph/snapshot/record/discovery/port type names remain unchanged; there is no direct provider-port signature change. Ordinary workflow protocol, application strong values and the Hosting child receipt remain unchanged. The standalone proof is compiled as OrcaCore.Runtime.Protocol with only shipped OrcaCore.dll, not a friend or a copied internals-enabled OrcaCore assembly. Its scoped round-trip example is not the complete eight-kind DAG codec or a provider implementation.
