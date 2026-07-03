using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Benchmarks.Fixtures;

internal static class DeterministicIds
{
    internal static InstanceId Instance(int seed)
    {
        return new InstanceId(GuidFrom(seed));
    }

    internal static DefinitionId Definition(int seed)
    {
        return new DefinitionId(GuidFrom(seed));
    }

    internal static EventId Event(int seed)
    {
        return new EventId(GuidFrom(seed));
    }

    internal static CommandId Command(int seed)
    {
        return new CommandId(GuidFrom(seed));
    }

    internal static CausationId Causation(int seed)
    {
        return new CausationId(GuidFrom(seed));
    }

    internal static OutboxRecordId Outbox(int seed)
    {
        return new OutboxRecordId(GuidFrom(seed));
    }

    internal static TimerId Timer(int seed)
    {
        return new TimerId(GuidFrom(seed));
    }

    private static Guid GuidFrom(int seed)
    {
        return new Guid(seed, 0, 0, [0, 0, 0, 0, 0, 0, 0, 1]);
    }
}
