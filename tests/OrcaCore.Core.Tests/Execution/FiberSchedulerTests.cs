using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
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
}
