using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// In-memory seam for storing live instances by <see cref="InstanceId"/> (CR-021: never
/// exposed publicly — the engine facade reads through it and returns snapshots only).
/// </summary>
internal interface IInstanceRegistry
{
    void Add<TState>(WorkflowInstance<TState> instance);
}
