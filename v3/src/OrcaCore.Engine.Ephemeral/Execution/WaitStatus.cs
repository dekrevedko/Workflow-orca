namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>Lifecycle of a runtime wait record (EV-021). Internal — never exposed publicly (CR-021).</summary>
internal enum WaitStatus
{
    Active,
    Matched,
    Cancelled,
}
