namespace OrcaCore.Abstractions.Durable;

public enum RunChildrenJoinPolicy
{
    WhenAll,
    WhenAny
}

public enum RunChildrenResidualPolicy
{
    CancelRemaining,
    LetRemainingComplete,
    DetachRemaining
}
