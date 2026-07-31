using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Execution;
using Xunit;

namespace OrcaCore.Core.Tests.Execution;

public sealed class FiberSchedulerTests
{
    [Fact]
    public void AuthoredStartupAndRepeatedRequeue_UseBoundedRoundRobinOrder()
    {
        var first = new FiberId("fiber:first");
        var second = new FiberId("fiber:second");
        var third = new FiberId("fiber:third");
        var scheduler = FiberScheduler.Create([first, second, third]);
        var selected = new List<FiberId>();

        for (var turn = 0; turn < 6; turn++)
        {
            var next = FiberScheduler.SelectNext(scheduler);
            next.Should().NotBeNull();
            var fiberId = next!.Value;
            selected.Add(fiberId);
            scheduler = FiberScheduler.CompleteTurn(scheduler, fiberId, requeueSelected: true);
        }

        selected.Should().Equal(first, second, third, first, second, third);
        scheduler.NextFiberId.Should().Be(first);
    }

    [Fact]
    public void NewFibersKeepAuthoredOrder_AndSameTransitionResumesUseStableIdentityOrder()
    {
        var parent = new FiberId("fiber:parent");
        var existingSibling = new FiberId("fiber:existing");
        var authoredSecond = new FiberId("fiber:authored-second");
        var authoredFirst = new FiberId("fiber:authored-first");
        var resumedLater = new FiberId("fiber:resumed-z");
        var resumedEarlier = new FiberId("fiber:resumed-a");
        var scheduler = FiberScheduler.Create([parent, existingSibling]);

        scheduler = FiberScheduler.CompleteTurn(
            scheduler,
            parent,
            requeueSelected: false,
            createdInAuthoredOrder: [authoredSecond, authoredFirst],
            resumedTogether: [resumedLater, resumedEarlier]);

        scheduler.RunnableFiberIds.Should().Equal(
            existingSibling,
            authoredSecond,
            authoredFirst,
            resumedEarlier,
            resumedLater);
        FiberScheduler.SelectNext(scheduler).Should().Be(existingSibling);
    }

    [Fact]
    public void PathCeiling_AdmitsAuthoredChildrenAndReusesReleasedToken()
    {
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var initial = StructuredExecutionState.Create(
            instanceId,
            generation: 0,
            new InstructionId("instruction:root"));
        var scopeId = new ScopeId("scope:root");
        var first = new FiberId("fiber:first");
        var second = new FiberId("fiber:second");
        var root = initial.Fibers[initial.RootFiberId] with
        {
            Phase = FiberPhase.Blocked,
            Blocked = new FiberBlock(FiberBlockedReason.Scope, scopeId.Value)
        };
        var fibers = new Dictionary<FiberId, FiberRecord>
        {
            [root.Id] = root,
            [first] = Child(first, scopeId),
            [second] = Child(second, scopeId)
        };
        var scope = new ExecutionScopeRecord(
            scopeId,
            new ScopePlanId("scope-plan:root"),
            ScopeEntrySequence: 0,
            ParentScopeId: null,
            root.Id,
            CompiledScopeKind.WhenAll,
            ExecutionScopePhase.Running,
            [first, second],
            WinnerFiberId: null,
            new Dictionary<FiberId, byte[]?>());
        var state = initial with
        {
            Fibers = fibers,
            Scopes = new Dictionary<ScopeId, ExecutionScopeRecord> { [scopeId] = scope },
            Scheduler = FiberScheduler.Create([first, second])
        };

        state = FiberScheduler.ApplyPathCeiling(state, 1);

        state.Scheduler.RunnableFiberIds.Should().Equal(first);

        fibers = new Dictionary<FiberId, FiberRecord>(state.Fibers)
        {
            [first] = state.Fibers[first] with
            {
                Phase = FiberPhase.Blocked,
                Blocked = new FiberBlock(FiberBlockedReason.Wait, "wait:first")
            }
        };
        state = FiberScheduler.ApplyPathCeiling(state with { Fibers = fibers }, 1);

        state.Scheduler.RunnableFiberIds.Should().Equal(second);

        static FiberRecord Child(FiberId id, ScopeId scopeId) =>
            new(
                id,
                scopeId,
                new InstructionId($"instruction:{id.Value}"),
                FiberPhase.Runnable,
                LoopIteration: 0,
                NextScopeEntrySequence: 0,
                LocalStatePayload: null,
                ResultPayload: null,
                Blocked: null,
                Failure: null,
                CancellationReason: null);
    }
}
