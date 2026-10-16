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
    Park,
    Unpark,
    Timeout
}
