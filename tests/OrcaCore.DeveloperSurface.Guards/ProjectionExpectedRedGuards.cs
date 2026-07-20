using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class ProjectionExpectedRedGuards
{
    [Fact]
    public async Task GetStateAsync_UsesConfiguredCopierAndReturnsDetachedCommittedState()
    {
        var snapshotter = new RecordingSnapshotter();
        var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        {
            StateSnapshotter = snapshotter
        });
        var definition = Workflow.Ephemeral<MutableState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new MutableState { Value = value, Items = ["committed"] })
            .End("done")
            .Build();
        engine.RegisterDefinition(definition);
        var started = await engine.StartAsync<string, MutableState>(
            definition.DefinitionId,
            "root",
            TestContext.Current.CancellationToken);
        var handle = engine.Management.Instance(started.InstanceId);

        var synchronousBaseline = handle.GetState<MutableState>();
        synchronousBaseline.Value = "mutated-outside";
        synchronousBaseline.Items.Add("outside");
        var committedAgain = handle.GetState<MutableState>();
        committedAgain.Value.Should().Be("root");
        committedAgain.Items.Should().Equal("committed");

        var asynchronous = await ExpectedPublicApi.InvokeGenericAsync<MutableState>(
            handle,
            "GetStateAsync",
            TestContext.Current.CancellationToken);
        asynchronous.Value = "async-outside";
        asynchronous.Items.Add("async-outside");
        var asynchronousAgain = await ExpectedPublicApi.InvokeGenericAsync<MutableState>(
            handle,
            "GetStateAsync",
            TestContext.Current.CancellationToken);
        asynchronousAgain.Value.Should().Be("root");
        asynchronousAgain.Items.Should().Equal("committed");
        snapshotter.Calls.Should().BeGreaterThanOrEqualTo(6,
            "commits and every management read use the configured copy contract");
    }

    [Fact]
    public async Task ActiveWaits_ProjectStableAuthoredLocationAndMatchingMetadata()
    {
        var definition = Workflow.Ephemeral<MutableState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new MutableState { Value = value })
            .Parallel<int>(
                scope => scope.Branch<MutableState>(
                    "approval-branch",
                    parent => new MutableState { Value = parent.Value.Value },
                    branch => branch
                        .Wait("Approved", state => new CorrelationId(state.Value))
                        .Return(_ => 1)),
                (parent, _) => parent.Value)
            .End("approved")
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);
        var first = await engine.StartAsync<string, MutableState>(
            definition.DefinitionId,
            "same-correlation",
            TestContext.Current.CancellationToken);
        var second = await engine.StartAsync<string, MutableState>(
            definition.DefinitionId,
            "same-correlation",
            TestContext.Current.CancellationToken);

        var firstWait = engine.Management.Instance(first.InstanceId).GetActiveWaits().Should().ContainSingle().Subject;
        var secondWait = engine.Management.Instance(second.InstanceId).GetActiveWaits().Should().ContainSingle().Subject;
        firstWait.EventName.Should().Be("Approved");
        firstWait.CorrelationId.Should().Be(new CorrelationId("same-correlation"));
        firstWait.WaitId.Should().NotBe(secondWait.WaitId, "runtime wait identities are per-instance opaque handles");

        var firstLocation = firstWait.GetType().GetProperty("AuthoredLocation");
        firstLocation.Should().NotBeNull("application waits identify the stable authored branch/path");
        var secondLocation = secondWait.GetType().GetProperty("AuthoredLocation")!.GetValue(secondWait);
        firstLocation!.GetValue(firstWait).Should().Be(secondLocation,
            "unchanged authored graphs have the same location across instances");
    }

    [Fact]
    public async Task DurableGetStateAsync_ReadsDetachedCommittedRootSlot()
    {
        var definitionId = DefinitionId.New();
        var definition = Workflow.Durable<DurableBehaviorHarness.GuardState>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<object?>(_ => new DurableBehaviorHarness.GuardState
            {
                Key = "durable-root",
                Log = ["committed"]
            })
            .End("done")
            .Build();
        var host = DurableBehaviorHarness.CreateHost(definition: definition);
        var instanceId = await DurableBehaviorHarness.StartAndPumpAsync(
            host,
            definitionId,
            TestContext.Current.CancellationToken);
        (await DurableBehaviorHarness.SnapshotAsync(host, instanceId, TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowStatus.Completed);
        var handle = host.Runtime.Management.Instance(instanceId);

        var first = await ExpectedPublicApi.InvokeGenericAsync<DurableBehaviorHarness.GuardState>(
            handle,
            "GetStateAsync",
            TestContext.Current.CancellationToken);
        first.Key = "outside";
        first.Log.Add("outside");
        var second = await ExpectedPublicApi.InvokeGenericAsync<DurableBehaviorHarness.GuardState>(
            handle,
            "GetStateAsync",
            TestContext.Current.CancellationToken);

        second.Key.Should().Be("durable-root");
        second.Log.Should().Equal("committed");
    }

    public sealed class MutableState
    {
        public string Value { get; set; } = string.Empty;

        public List<string> Items { get; set; } = [];
    }

    private sealed class RecordingSnapshotter : IEphemeralStateSnapshotter
    {
        internal int Calls { get; private set; }

        public TState Snapshot<TState>(TState state)
        {
            Calls++;
            if (state is MutableState mutable)
            {
                return (TState)(object)new MutableState
                {
                    Value = mutable.Value,
                    Items = [.. mutable.Items]
                };
            }

            throw new NotSupportedException($"Unexpected state type '{typeof(TState)}'.");
        }
    }
}
