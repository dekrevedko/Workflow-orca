using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class LifecycleAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-509")]
    public async Task LifecycleEvents_FollowDocumentedDurabilityGuarantees()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var ephemeral = provider.GetRequiredService<EphemeralWorkflowEngine>();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then(_ => ValueTask.CompletedTask)
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var ephemeralInstance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("lifecycle-events"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var ephemeralLifecycle = ephemeral.Management.Instance(ephemeralInstance.InstanceId).GetLifecycleEvents();

        var instanceId = InstanceIdValue(1);
        var durableStore = new InMemoryWorkflowProvider();
        await durableStore.AppendAsync(
            LifecycleCommit(instanceId),
            TestContext.Current.CancellationToken);
        var outbox = await durableStore.ClaimAsync(10, TestContext.Current.CancellationToken);
        var durableLifecycle = JsonSerializer.Deserialize<LifecycleEventSnapshot>(
            outbox.Single(record => record.Kind == "lifecycle-event").Payload);

        ephemeralLifecycle.Should().ContainSingle(lifecycleEvent =>
            lifecycleEvent.EventName == "InstanceCompleted" && lifecycleEvent.Durable == false);
        durableLifecycle.Should().BeEquivalentTo(new
        {
            EventName = "InstanceCompleted",
            Status = WorkflowStatus.Completed,
            Durable = true
        });
    }

    private static ProviderCommitBatch LifecycleCommit(InstanceId instanceId)
    {
        return new ProviderCommitBatch
        {
            StreamId = new WorkflowStreamId(instanceId),
            ExpectedVersion = StreamVersion.Empty,
            Events =
            [
                new WorkflowTerminalEvent
                {
                    EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                    InstanceId = instanceId,
                    CommandId = CommandIdValue(1),
                    CausationId = CausationId.New(),
                    OccurredAt = Timestamp(1),
                    Status = WorkflowStatus.Completed
                }
            ],
            OutboxRecords =
            [
                new OutboxWrite(
                    OutboxRecordId.New(),
                    "lifecycle-event",
                    JsonSerializer.SerializeToUtf8Bytes(new LifecycleEventSnapshot
                    {
                        InstanceId = instanceId,
                        EventName = "InstanceCompleted",
                        Status = WorkflowStatus.Completed,
                        OccurredAt = Timestamp(1),
                        Durable = true
                    }))
            ]
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return InstanceId.Parse(GuidValue(value).ToString());
    }

    private static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }

    public sealed class TestState;
}
