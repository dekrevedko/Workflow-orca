namespace OrcaCore.Core.Lifecycle;

public enum LifecycleTrigger
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
    Unpark,
    Timeout
}
