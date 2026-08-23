using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

[Trait("Requirement", "CR-009a")]
public sealed class AuthoringLifecycleTests
{
    [Fact]
    public void EphemeralParallelJoin_SupersedesRootAndReturnsSuccessorEpochFacade()
    {
        var root = NewEphemeral();
        var join = root.Parallel<int>(branches => branches.Branch(
            AuthoredBranchId.Create("only"),
            snapshot => snapshot.Value,
            branch => branch.Return(snapshot => snapshot.Value.Value)));

        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        Action staleMutation = () => root.Delay(TimeSpan.FromMilliseconds(1));

        AssertLifecycle(
            staleMutation,
            "SFE-AUTH-LIFECYCLE-001",
            "workflow:$/n:00000002",
            "workflow:$/n:00000001");
        successor.Should().NotBeSameAs(root);
        successor.End().Build().Mode.Should().Be(WorkflowMode.Ephemeral);
    }

    [Fact]
    public void DurableParallelJoin_SupersedesRootAndReturnsSuccessorEpochFacade()
    {
        var root = NewDurable();
        var join = root.Parallel<int>(branches => branches.Branch(
            AuthoredBranchId.Create("only"),
            snapshot => snapshot.Value,
            branch => branch.Return(snapshot => snapshot.Value.Value)));

        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        Action staleMutation = () => root.Delay(TimeSpan.FromMilliseconds(1));

        AssertLifecycle(
            staleMutation,
            "SFE-AUTH-LIFECYCLE-001",
            "workflow:$/n:00000002",
            "workflow:$/n:00000001");
        successor.Should().NotBeSameAs(root);
        successor.End().Build().Mode.Should().Be(WorkflowMode.Durable);
    }

    [Fact]
    public void DurableJoinSelection_IsSingleUseAndRejectedSelectionDoesNotMutateGraph()
    {
        var join = NewDurable().Parallel<int>(branches => branches.Branch(
            AuthoredBranchId.Create("only"),
            snapshot => snapshot.Value,
            branch => branch.Return(snapshot => snapshot.Value.Value)));
        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        var completion = successor.End();
        var before = completion.Build();

        Action duplicateJoin = () => join.WhenAllOutcomes((snapshot, _) => snapshot.Value);

        AssertLifecycle(
            duplicateJoin,
            "SFE-AUTH-LIFECYCLE-002",
            "workflow:$/n:00000001",
            "workflow:$/n:00000001");
        completion.Build().DefinitionFingerprint.Should().Be(before.DefinitionFingerprint);
    }

    [Fact]
    public void EphemeralJoinSelection_IsSingleUseAndRejectedSelectionDoesNotMutateGraph()
    {
        var join = NewEphemeral().Parallel<int>(branches => branches.Branch(
            AuthoredBranchId.Create("only"),
            snapshot => snapshot.Value,
            branch => branch.Return(snapshot => snapshot.Value.Value)));
        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        var completion = successor.End();
        var before = completion.Build();

        Action duplicateJoin = () => join.WhenAllOutcomes((snapshot, _) => snapshot.Value);

        AssertLifecycle(
            duplicateJoin,
            "SFE-AUTH-LIFECYCLE-002",
            "workflow:$/n:00000001",
            "workflow:$/n:00000001");
        completion.Build().DefinitionFingerprint.Should().Be(before.DefinitionFingerprint);
    }

    [Fact]
    public void EphemeralRootTerminal_FreezesSnapshotAndRepeatedBuildsAreStable()
    {
        var root = NewEphemeral().Then<ProbeStep>();
        var completion = root.End();
        var first = completion.Build();

        Action postTerminalMutation = () => root.Delay(TimeSpan.FromMilliseconds(1));

        AssertLifecycle(
            postTerminalMutation,
            "SFE-AUTH-LIFECYCLE-003",
            "workflow:$/n:00000003",
            "workflow:$/n:00000002");
        var second = completion.Build();
        var firstValidation = completion.TryBuild();
        var secondValidation = completion.TryBuild();

        second.DefinitionFingerprint.Should().Be(first.DefinitionFingerprint);
        secondValidation.Diagnostics.Should().Equal(firstValidation.Diagnostics);
        firstValidation.TryGetValue(out var firstValidated).Should().BeTrue();
        secondValidation.TryGetValue(out var secondValidated).Should().BeTrue();
        secondValidated!.DefinitionFingerprint.Should().Be(firstValidated!.DefinitionFingerprint);
    }

    [Fact]
    public void DurableRootTerminal_FreezesSnapshotAndRepeatedBuildsAreStable()
    {
        var root = NewDurable().Then<ProbeStep>();
        var completion = root.End();
        var first = completion.Build();

        Action postTerminalMutation = () => root.Delay(TimeSpan.FromMilliseconds(1));

        AssertLifecycle(
            postTerminalMutation,
            "SFE-AUTH-LIFECYCLE-003",
            "workflow:$/n:00000003",
            "workflow:$/n:00000002");
        var second = completion.Build();
        var firstValidation = completion.TryBuild();
        var secondValidation = completion.TryBuild();

        second.DefinitionFingerprint.Should().Be(first.DefinitionFingerprint);
        secondValidation.Diagnostics.Should().Equal(firstValidation.Diagnostics);
        firstValidation.TryGetValue(out var firstValidated).Should().BeTrue();
        secondValidation.TryGetValue(out var secondValidated).Should().BeTrue();
        secondValidated!.DefinitionFingerprint.Should().Be(firstValidated!.DefinitionFingerprint);
    }

    [Fact]
    public void ContinueAsNew_FreezesSnapshotAndRejectedMutationLeavesGraphUnchanged()
    {
        var root = NewDurable();
        var completion = root.ContinueAsNew(snapshot => snapshot.Value);
        var first = completion.Build();

        Action postTerminalMutation = () => root.Delay(TimeSpan.FromMilliseconds(1));

        AssertLifecycle(
            postTerminalMutation,
            "SFE-AUTH-LIFECYCLE-003",
            "workflow:$/n:00000002",
            "workflow:$/n:00000001");
        var second = completion.Build();
        second.DefinitionFingerprint.Should().Be(first.DefinitionFingerprint);
    }

    [Theory]
    [InlineData("ephemeral-nested")]
    [InlineData("durable-nested")]
    [InlineData("ephemeral-branch")]
    [InlineData("durable-branch")]
    [InlineData("ephemeral-item")]
    [InlineData("durable-item")]
    [InlineData("durable-lease-workflow")]
    [InlineData("durable-lease-nested")]
    [InlineData("durable-lease-branch")]
    [InlineData("durable-lease-item")]
    public void CallbackHandle_ExpiresWhenCallbackReturns(string role)
    {
        EscapedMutation rejected = role switch
        {
            "ephemeral-nested" => EscapedEphemeralNested(),
            "durable-nested" => EscapedDurableNested(),
            "ephemeral-branch" => EscapedEphemeralBranch(),
            "durable-branch" => EscapedDurableBranch(),
            "ephemeral-item" => EscapedEphemeralItem(),
            "durable-item" => EscapedDurableItem(),
            "durable-lease-workflow" => EscapedDurableLeaseWorkflow(),
            "durable-lease-nested" => EscapedDurableLeaseNested(),
            "durable-lease-branch" => EscapedDurableLeaseBranch(),
            "durable-lease-item" => EscapedDurableLeaseItem(),
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };

        AssertLifecycle(rejected.Mutation, "SFE-AUTH-LIFECYCLE-004");
        rejected.BuildFingerprint().Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task EphemeralConcurrentAuthoring_AdmitsOneAtomicWinnerAndRejectsLoserWithoutGraphMutation()
    {
        var definitionId = DefinitionId.New();
        var root = NewEphemeral(definitionId);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var winner = Task.Run(() => root.If(
            _ => true,
            nested =>
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
                nested.Then<ProbeStep>();
            }));

        entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
        Action loser = () => root.Delay(TimeSpan.FromMilliseconds(1));
        try
        {
            AssertLifecycle(
                loser,
                "SFE-AUTH-LIFECYCLE-005",
                "workflow:$/n:00000001",
                "workflow:$/n:00000001");
        }
        finally
        {
            release.Set();
        }
        await winner;

        var actual = root.End().Build();
        var expected = NewEphemeral(definitionId)
            .If(_ => true, nested => nested.Then<ProbeStep>())
            .End()
            .Build();
        actual.DefinitionFingerprint.Should().Be(expected.DefinitionFingerprint);
    }

    [Fact]
    public async Task DurableConcurrentAuthoring_AdmitsOneAtomicWinnerAndRejectsLoserWithoutGraphMutation()
    {
        var definitionId = DefinitionId.New();
        var root = NewDurable(definitionId);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();

        var winner = Task.Run(() => root.If(
            _ => true,
            nested =>
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
                nested.Then<ProbeStep>();
            }));

        entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).Should().BeTrue();
        Action loser = () => root.Delay(TimeSpan.FromMilliseconds(1));
        try
        {
            AssertLifecycle(
                loser,
                "SFE-AUTH-LIFECYCLE-005",
                "workflow:$/n:00000001",
                "workflow:$/n:00000001");
        }
        finally
        {
            release.Set();
        }
        await winner;

        var actual = root.End().Build();
        var expected = NewDurable(definitionId)
            .If(_ => true, nested => nested.Then<ProbeStep>())
            .End()
            .Build();
        actual.DefinitionFingerprint.Should().Be(expected.DefinitionFingerprint);
    }

    [Fact]
    public void WorkflowConfiguration_IsSessionOwnedAcrossSuccessorFacades()
    {
        var root = NewDurable().CompleteWithin(TimeSpan.FromMinutes(1));
        var successor = root
            .Parallel<int>(branches => branches.Branch(
                AuthoredBranchId.Create("only"),
                snapshot => snapshot.Value,
                branch => branch.Return(snapshot => snapshot.Value.Value)))
            .WhenAll((snapshot, _) => snapshot.Value);

        Action duplicateDeadline = () => successor.CompleteWithin(TimeSpan.FromMinutes(2));

        duplicateDeadline.Should().Throw<WorkflowDefinitionException>()
            .Which.Diagnostics.Should().ContainSingle(x => x.Code == "SFE-AUTH-DEADLINE-001");
    }

    private static EphemeralWorkflowBuilder<Input, State> NewEphemeral(
        DefinitionId? definitionId = null) =>
        Workflow.Ephemeral<State>(definitionId ?? DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value));

    private static DurableWorkflowBuilder<Input, State> NewDurable(
        DefinitionId? definitionId = null) =>
        Workflow.Durable<State>(definitionId ?? DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value));

    private static EscapedMutation EscapedEphemeralNested()
    {
        var root = NewEphemeral();
        EphemeralNestedBuilder<Input, State>? escaped = null;
        root.If(_ => true, nested => escaped = nested);
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => root.End().Build().DefinitionFingerprint);
    }

    private static EscapedMutation EscapedDurableNested()
    {
        var root = NewDurable();
        DurableNestedBuilder<Input, State>? escaped = null;
        root.If(_ => true, nested => escaped = nested);
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => root.End().Build().DefinitionFingerprint);
    }

    private static EscapedMutation EscapedEphemeralBranch()
    {
        EphemeralBranchBuilder<State, int>? escaped = null;
        var join = NewEphemeral().Parallel<int>(branches => branches.Branch(
            AuthoredBranchId.Create("only"),
            snapshot => snapshot.Value,
            branch =>
            {
                escaped = branch;
                branch.Return(snapshot => snapshot.Value.Value);
            }));
        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => successor.End().Build().DefinitionFingerprint);
    }

    private static EscapedMutation EscapedDurableBranch()
    {
        DurableBranchBuilder<State, int>? escaped = null;
        var join = NewDurable().Parallel<int>(branches => branches.Branch(
            AuthoredBranchId.Create("only"),
            snapshot => snapshot.Value,
            branch =>
            {
                escaped = branch;
                branch.Return(snapshot => snapshot.Value.Value);
            }));
        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => successor.End().Build().DefinitionFingerprint);
    }

    private static EscapedMutation EscapedEphemeralItem()
    {
        EphemeralItemBuilder<State, int>? escaped = null;
        var join = NewEphemeral().ForEach<int, State, int>(
            snapshot => [snapshot.Value.Value],
            ForEachOptions.Create(1),
            item => new State(item.Item),
            body =>
            {
                escaped = body;
                body.Return(snapshot => snapshot.Value.Value);
            });
        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => successor.End().Build().DefinitionFingerprint);
    }

    private static EscapedMutation EscapedDurableItem()
    {
        DurableItemBuilder<State, int>? escaped = null;
        var join = NewDurable().ForEach<int, State, int>(
            snapshot => [snapshot.Value.Value],
            ForEachOptions.Create(1),
            item => new State(item.Item),
            body =>
            {
                escaped = body;
                body.Return(snapshot => snapshot.Value.Value);
            });
        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => successor.End().Build().DefinitionFingerprint);
    }

    private static EscapedMutation EscapedDurableLeaseWorkflow()
    {
        var root = NewDurable();
        DurableLeaseWorkflowBuilder<Input, State>? escaped = null;
        root.AcquireResources(ResourceRequest(), leased => escaped = leased);
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => root.End().Build().DefinitionFingerprint);
    }

    private static EscapedMutation EscapedDurableLeaseNested()
    {
        var root = NewDurable();
        DurableLeaseNestedBuilder<Input, State>? escaped = null;
        root.If(
            _ => true,
            nested => nested.AcquireResources(ResourceRequest(), leased => escaped = leased));
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => root.End().Build().DefinitionFingerprint);
    }

    private static EscapedMutation EscapedDurableLeaseBranch()
    {
        DurableLeaseBranchBuilder<State, int>? escaped = null;
        var join = NewDurable().Parallel<int>(branches => branches.Branch(
            AuthoredBranchId.Create("only"),
            snapshot => snapshot.Value,
            branch =>
            {
                branch.AcquireResources(ResourceRequest(), leased => escaped = leased);
                branch.Return(snapshot => snapshot.Value.Value);
            }));
        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => successor.End().Build().DefinitionFingerprint);
    }

    private static EscapedMutation EscapedDurableLeaseItem()
    {
        DurableLeaseItemBuilder<State, int>? escaped = null;
        var join = NewDurable().ForEach<int, State, int>(
            snapshot => [snapshot.Value.Value],
            ForEachOptions.Create(1),
            item => new State(item.Item),
            body =>
            {
                body.AcquireResources(ResourceRequest(), leased => escaped = leased);
                body.Return(snapshot => snapshot.Value.Value);
            });
        var successor = join.WhenAll((snapshot, _) => snapshot.Value);
        return new(
            () => escaped!.Then<ProbeStep>(),
            () => successor.End().Build().DefinitionFingerprint);
    }

    private static ResourceLeaseRequest ResourceRequest() =>
        ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("pool")));

    private static void AssertLifecycle(
        Action action,
        string code,
        string? location = null,
        string? relatedLocation = null)
    {
        var exception = action.Should().Throw<WorkflowDefinitionException>().Which;
        var diagnostic = exception.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Code.Should().Be(code);
        if (location is null)
        {
            diagnostic.Location.Value.Should().StartWith("workflow:$");
        }
        else
        {
            diagnostic.Location.Value.Should().Be(location);
        }

        var related = diagnostic.RelatedLocations.Should().ContainSingle().Subject;
        if (relatedLocation is null)
        {
            related.Value.Should().StartWith("workflow:$");
        }
        else
        {
            related.Value.Should().Be(relatedLocation);
        }
    }

    private sealed record Input(int Value);

    private sealed record State(int Value);

    private sealed record EscapedMutation(
        Action Mutation,
        Func<DefinitionFingerprint> BuildFingerprint);

    private sealed class ProbeStep : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
