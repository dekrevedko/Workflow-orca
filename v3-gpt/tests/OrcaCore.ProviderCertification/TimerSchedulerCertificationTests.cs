using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class TimerSchedulerCertificationTests
{
    protected abstract ITimerScheduler CreateTimerScheduler();

    [Fact]
    [Trait("AC", "EV-050")]
    public async Task ScheduleAsync_DueTimer_IsClaimableOnce()
    {
        var scheduler = CreateTimerScheduler();
        var request = Request(fireAt: Timestamp(10));
        await scheduler.ScheduleAsync(request, TestContext.Current.CancellationToken);

        var firstClaim = await scheduler.ClaimDueAsync(
            Timestamp(10),
            maxCount: 10,
            TestContext.Current.CancellationToken);
        var secondClaim = await scheduler.ClaimDueAsync(
            Timestamp(10),
            maxCount: 10,
            TestContext.Current.CancellationToken);

        firstClaim.Should().ContainSingle().Which.Should().BeEquivalentTo(new FireTimerCommand
        {
            CommandId = request.CommandId,
            InstanceId = request.InstanceId,
            RequestedAt = Timestamp(10),
            TimerId = request.TimerId
        });
        secondClaim.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "EV-050")]
    public async Task ScheduleAsync_NotDueTimer_IsNotClaimed()
    {
        var scheduler = CreateTimerScheduler();
        await scheduler.ScheduleAsync(Request(fireAt: Timestamp(20)), TestContext.Current.CancellationToken);

        var claimed = await scheduler.ClaimDueAsync(
            Timestamp(19),
            maxCount: 10,
            TestContext.Current.CancellationToken);

        claimed.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "EV-050")]
    [Trait("AC", "NF-020")]
    public async Task ClaimDueAsync_ConcurrentWorkers_ClaimTimerOnce()
    {
        var scheduler = CreateTimerScheduler();
        var request = Request(fireAt: Timestamp(10));
        await scheduler.ScheduleAsync(request, TestContext.Current.CancellationToken);

        var first = scheduler.ClaimDueAsync(Timestamp(10), maxCount: 1, TestContext.Current.CancellationToken);
        var second = scheduler.ClaimDueAsync(Timestamp(10), maxCount: 1, TestContext.Current.CancellationToken);
        var claimed = (await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken))
            .SelectMany(commands => commands)
            .ToArray();

        claimed.Should().ContainSingle()
            .Which.TimerId.Should().Be(request.TimerId);
    }

    protected static TimerScheduleRequest Request(DateTimeOffset fireAt)
    {
        return new TimerScheduleRequest
        {
            TimerId = TimerIdValue(1),
            InstanceId = InstanceIdValue(1),
            CommandId = CommandIdValue(1),
            FireAt = fireAt,
            WakeupName = "approval-timeout"
        };
    }

    protected static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    protected static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(GuidValue(value));
    }

    protected static CommandId CommandIdValue(int value)
    {
        return new CommandId(GuidValue(value));
    }

    protected static TimerId TimerIdValue(int value)
    {
        return new TimerId(GuidValue(value));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}

public sealed class InMemoryTimerSchedulerCertificationTests : TimerSchedulerCertificationTests
{
    protected override ITimerScheduler CreateTimerScheduler()
    {
        return new InMemoryWorkflowProvider();
    }
}
