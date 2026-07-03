using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// In-memory seam for storing live instances by <see cref="InstanceId"/> (CR-021: never
/// exposed publicly — the engine facade reads through it and returns snapshots only).
/// </summary>
internal interface IInstanceRegistry
{
    void Add<TState>(WorkflowInstance<TState> instance);

    /// <summary>Looks up a live instance by id, typed as <typeparamref name="TState"/>.</summary>
    WorkflowInstance<TState>? TryGet<TState>(InstanceId instanceId);

    /// <summary>
    /// Returns every currently-registered instance as its non-generic metadata view (T1-13,
    /// EV-013/AC-115): a single bulk enumeration so management queries (List/Count/
    /// GetActiveWaits/Statistics) never loop N per-instance <see cref="TryGet{TState}"/> calls.
    /// </summary>
    IReadOnlyList<IWorkflowInstance> GetAll();
}
