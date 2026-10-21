using OrcaCore.Hosting;

internal static class CannotConstructShutdownResults
{
    internal static void Probe()
    {
        _ = new EphemeralHostShutdownResult.Completed(); // DENIED: completed
        _ = new EphemeralHostShutdownResult.Incomplete(EphemeralHostShutdownIncompleteReason.DeadlineElapsed, 1); // DENIED: incomplete
        _ = new EphemeralHostStoppingException(); // DENIED: stopping
        _ = new EphemeralHostShutdownIncompleteException(null!); // DENIED: exception
    }
}

internal sealed class CannotDeriveResult : EphemeralHostShutdownResult { } // DENIED: subclass
