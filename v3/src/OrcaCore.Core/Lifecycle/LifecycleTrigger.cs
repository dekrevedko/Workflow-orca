namespace OrcaCore.Core.Lifecycle;

/// <summary>
/// Named lifecycle triggers (CR-030). <see cref="Pause"/>/<see cref="Resume"/> are part of
/// the one shared transition table but are unreachable from the ephemeral engine, which
/// never fires them.
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
