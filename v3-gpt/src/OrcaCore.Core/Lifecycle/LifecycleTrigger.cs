namespace OrcaCore.Core.Lifecycle;

internal enum LifecycleTrigger
{
    Start,
    EnterWait,
    MatchWait,
    Complete,
    Fail,
    Cancel,
    Terminate,
    Compensate,
    FailCompensation,
    Pause,
    Resume,
    Park,
    Unpark
}
