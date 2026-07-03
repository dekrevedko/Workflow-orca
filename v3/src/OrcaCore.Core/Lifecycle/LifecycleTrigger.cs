namespace OrcaCore.Core.Lifecycle;

/// <summary>
/// Named lifecycle triggers (CR-030). Consumed by both engine projects that reference
/// OrcaCore.Core (01-solution-architecture.md: Core is "engine-agnostic internals shared by
/// both engines") — not part of the authoring-facing public API surface, but public so
/// engine assemblies can drive it without ad-hoc InternalsVisibleTo grants.
/// <see cref="Pause"/>/<see cref="Resume"/> are part of the one shared transition table but
/// are unreachable from the ephemeral engine, which never fires them.
/// </summary>
public enum LifecycleTrigger
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
