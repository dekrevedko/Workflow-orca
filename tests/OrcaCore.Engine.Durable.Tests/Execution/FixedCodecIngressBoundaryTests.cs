using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Aggregates;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class FixedCodecIngressBoundaryTests
{
    [Fact]
    public void RawStartCommand_RejectsForeignPayloadBytesBeforeProducingFacts()
    {
        var command = new StartWorkflowCommand
        {
            InstanceId = InstanceId.Parse(Guid.CreateVersion7().ToString()),
            CommandId = CommandId.New(),
            RequestedAt = DateTimeOffset.UtcNow,
            DefinitionId = DefinitionId.New(),
            DefinitionVersion = DefinitionVersion.Initial,
            InputContentType = "application/x-foreign",
            InputPayload = [0xDE, 0xAD, 0xBE, 0xEF]
        };
        var aggregate = DurableWorkflowAggregate.Empty(command.InstanceId);

        var act = () => aggregate.DecideStart(command);

        act.Should().Throw<ArgumentException>()
            .WithParameterName(nameof(command.InputContentType));
    }
}
