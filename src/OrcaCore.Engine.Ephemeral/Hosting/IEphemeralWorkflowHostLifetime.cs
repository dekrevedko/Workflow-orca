namespace OrcaCore.Hosting;

/// <summary>Closes admission, cancels remaining work and observes physical shutdown.</summary>
public interface IEphemeralWorkflowHostLifetime
{
    ValueTask<EphemeralHostShutdownResult> StopAsync(TimeSpan timeout, CancellationToken cancellationToken = default);
}

/// <summary>A detached observation of this host's shutdown.</summary>
public abstract class EphemeralHostShutdownResult
{
    private protected EphemeralHostShutdownResult() { }

    public sealed class Completed : EphemeralHostShutdownResult
    {
        internal Completed() { }
    }

    public sealed class Incomplete : EphemeralHostShutdownResult
    {
        internal Incomplete(EphemeralHostShutdownIncompleteReason reason, long pendingWorkCount)
        {
            Reason = reason;
            PendingWorkCount = pendingWorkCount;
        }

        public EphemeralHostShutdownIncompleteReason Reason { get; }
        public long PendingWorkCount { get; }
    }
}

public enum EphemeralHostShutdownIncompleteReason
{
    DeadlineElapsed = 0,
    ObservationCancelled = 1,
    ShutdownFaulted = 2
}

public sealed class EphemeralHostStoppingException : global::OrcaCore.OrcaCoreException
{
    internal EphemeralHostStoppingException() : base("WF-EPHEMERAL-HOST-STOPPING", "The ephemeral workflow host is stopping.") { }
}

public sealed class EphemeralHostShutdownIncompleteException : global::OrcaCore.OrcaCoreException
{
    internal EphemeralHostShutdownIncompleteException(EphemeralHostShutdownResult.Incomplete result)
        : base("WF-EPHEMERAL-SHUTDOWN-INCOMPLETE", "Ephemeral workflow shutdown has not completed.") => Result = result;

    public EphemeralHostShutdownResult.Incomplete Result { get; }
}
