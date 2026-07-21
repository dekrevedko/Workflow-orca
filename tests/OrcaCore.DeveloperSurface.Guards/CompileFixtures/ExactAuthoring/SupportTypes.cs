namespace OrcaCore;

public sealed class DefinitionId;
public sealed class DefinitionVersion;
public sealed class DefinitionFingerprint;
public sealed class EventName;
public sealed class CorrelationId;
public sealed class WorkflowOutcomeName;
public sealed class AuthoredBranchId;
public sealed class TransientPoolName;
public sealed class ResourceLeaseRequest;
public sealed class ReadOnlyStateSnapshot<T>;
public sealed class Validation<T>;
public sealed class RetryPolicy;
public sealed class ForEachOptions;
public sealed class ForEachItemInput<T>(T item) { public T Item { get; } = item; }
public sealed class BranchResult<T>;
public sealed class BranchOutcome<T>;
public sealed class ForEachItemResult<T>;
public sealed class ForEachItemOutcome<T>;
public sealed class StepContext<T>;
public interface IStep<T>;
public enum WorkflowMode { Ephemeral, Durable }
