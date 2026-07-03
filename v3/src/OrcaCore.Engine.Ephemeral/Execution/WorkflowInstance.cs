using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// The live, mutable runtime state of an executing instance (CR-020/CR-021): engine-owned
/// runtime metadata plus the workflow-owned <typeparamref name="TState"/>. Never exposed
/// publicly — callers only ever see a <see cref="WorkflowInstanceSnapshot"/> (CR-021).
/// Mutations are engine-internal; the execution lane (T1-06) will serialize access.
/// </summary>
internal sealed class WorkflowInstance<TState>
{
    public WorkflowInstance(
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        TState state,
        DateTimeOffset createdAt)
    {
        InstanceId = instanceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        State = state;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        Status = WorkflowStatus.Running;
        Pointer = ExecutionPointer.Empty;
    }

    public InstanceId InstanceId { get; }

    public DefinitionId DefinitionId { get; }

    public DefinitionVersion DefinitionVersion { get; }

    public TState State { get; set; }

    public WorkflowStatus Status { get; set; }

    public ExecutionPointer Pointer { get; set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; set; }

    public string? ErrorSummary { get; set; }

    public string? EndOutcomeName { get; set; }

    /// <summary>The instance's single resident wait (EV-021), or null when not waiting. Instance-targeted only.</summary>
    public ActiveWait? ActiveWait { get; set; }

    /// <summary>
    /// The envelope that resumed the current run, surfaced to the first step's
    /// <see cref="OrcaCore.Abstractions.Steps.StepContext{TState}.ResumedEvent"/> only (EV-022).
    /// Cleared by the interpreter immediately after that one step executes.
    /// </summary>
    public EventEnvelope? PendingResumedEvent { get; set; }
}
