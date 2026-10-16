namespace OrcaCore.EventDrivenPrototype.Engine;

public static class PrototypeEventTypes
{
    public const string WorkflowStarted = "WorkflowStarted";
    public const string StepCompleted = "StepCompleted";
    public const string WaitRegistered = "WaitRegistered";
    public const string EventBuffered = "EventBuffered";
    public const string BufferedEventConsumed = "BufferedEventConsumed";
    public const string WaitMatched = "WaitMatched";
    public const string DuplicateEventIgnored = "DuplicateEventIgnored";
    public const string WorkflowCompleted = "WorkflowCompleted";
    public const string WorkflowFailed = "WorkflowFailed";
}
