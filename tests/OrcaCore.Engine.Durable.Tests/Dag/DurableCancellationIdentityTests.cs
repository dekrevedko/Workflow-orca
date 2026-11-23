using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Execution;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Dag;

public sealed class DurableCancellationIdentityTests
{
    [Fact]
    public void PersistedWinningCommandsAreStableAndBoundToTheChild()
    {
        var child = InstanceId.Parse("00112233-4455-6677-8899-aabbccddeeff");
        var independent = new CommandId(Guid.Parse("de772d0f-6b12-8886-ad0d-20481a07064a"));
        var owningDag = new CommandId(Guid.Parse("174785fa-9de7-82b5-a60e-c178c91cb18b"));
        Assert.Equal(independent, DurableCancellationIdentity.For(child, DurableCancellationOrigin.Independent));
        Assert.Equal(owningDag, DurableCancellationIdentity.For(child, DurableCancellationOrigin.OwningDag));
        Assert.Equal(DurableCancellationOrigin.Independent, DurableCancellationIdentity.Read(child, independent));
        Assert.Equal(DurableCancellationOrigin.OwningDag, DurableCancellationIdentity.Read(child, owningDag));
        Assert.Equal(DurableCancellationOrigin.LegacyUnknown, DurableCancellationIdentity.Read(child, CommandId.New()));
        var other = InstanceId.Parse("00112233-4455-6677-8899-aabbccddeefe");
        Assert.Throws<InvalidDataException>(() => DurableCancellationIdentity.Read(other, owningDag));
        Assert.Throws<InvalidDataException>(() => DurableCancellationIdentity.Read(child, new CommandId(Guid.NewGuid())));
    }
}
