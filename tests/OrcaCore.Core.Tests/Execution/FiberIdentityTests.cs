using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class FiberIdentityTests
{
    [Fact]
    public void RootFiberIdentity_IsDeterministicPerInstanceGeneration()
    {
        var instanceId = InstanceId.New();

        var generationZero = FiberIdentity.CreateRoot(instanceId, generation: 0);
        var replayedGenerationZero = FiberIdentity.CreateRoot(instanceId, generation: 0);
        var generationOne = FiberIdentity.CreateRoot(instanceId, generation: 1);

        replayedGenerationZero.Should().Be(generationZero);
        generationOne.Should().NotBe(generationZero);
    }

    [Fact]
    public void ScopeAndChildIdentity_DeriveFromCommittedPlanAndEntryIdentity()
    {
        var parent = FiberIdentity.CreateRoot(InstanceId.New(), generation: 0);
        var scopePlanId = new ScopePlanId("scope:root/parallel");

        var scope = FiberIdentity.CreateScope(parent, scopePlanId, scopeEntrySequence: 7);
        var replayedScope = FiberIdentity.CreateScope(parent, scopePlanId, scopeEntrySequence: 7);
        var nextLoopEntry = FiberIdentity.CreateScope(parent, scopePlanId, scopeEntrySequence: 8);
        var firstChild = FiberIdentity.CreateChild(scope, new BranchPlanId("branch:first"));
        var replayedFirstChild = FiberIdentity.CreateChild(scope, new BranchPlanId("branch:first"));
        var secondChild = FiberIdentity.CreateChild(scope, new BranchPlanId("branch:second"));

        replayedScope.Should().Be(scope);
        nextLoopEntry.Should().NotBe(scope);
        replayedFirstChild.Should().Be(firstChild);
        secondChild.Should().NotBe(firstChild);
    }

    [Fact]
    public void ForEachItemFiberIdentity_DerivesFromStableItemIndex()
    {
        var parent = FiberIdentity.CreateRoot(InstanceId.New(), generation: 0);
        var scope = FiberIdentity.CreateScope(parent, new ScopePlanId("scope:items"), scopeEntrySequence: 0);

        var first = FiberIdentity.CreateItem(scope, itemIndex: 0);
        var replayedFirst = FiberIdentity.CreateItem(scope, itemIndex: 0);
        var second = FiberIdentity.CreateItem(scope, itemIndex: 1);

        replayedFirst.Should().Be(first);
        second.Should().NotBe(first);
    }
}
