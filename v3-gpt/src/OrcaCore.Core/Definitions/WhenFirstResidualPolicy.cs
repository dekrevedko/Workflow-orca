namespace OrcaCore.Core.Definitions;

/// <summary>
/// Defines how a WhenFirst composition resolves branches that do not win.
/// </summary>
public enum WhenFirstResidualPolicy
{
    /// <summary>
    /// Cancels runtime-owned work for losing branches before the parent continuation runs.
    /// </summary>
    CancelRemaining,

    /// <summary>
    /// Records losing branches as ignored before the parent continuation runs.
    /// </summary>
    IgnoreRemaining,

    /// <summary>
    /// Records the winner immediately and waits for losing branches to complete before the parent continuation runs.
    /// </summary>
    LetRemainingComplete
}
