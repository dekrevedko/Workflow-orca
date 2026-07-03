namespace OrcaCore.Core.Lifecycle;

/// <summary>
/// Named lifecycle triggers (CR-030). <see cref="Pause"/>/<see cref="Resume"/> are part of the
/// shared transition table but unreachable from the ephemeral engine.
/// </summary>
internal enum LifecycleTrigger
{
    Start,
    EnterWait,
    MatchWait,
    Complete,
    Fail,
    Cancel,
    Terminate,
    Pause,
    Resume,
}
