using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Retention;

[Trait("AC", "AC-533")]
public sealed class TerminalRetentionPolicyTests
{
    [Theory]
    [InlineData(0, 0, 0, "maxRetainedInstances")]
    [InlineData(-1, 1, 1, "maxRetainedInstances")]
    [InlineData(1, 0, 0, "maxAge")]
    [InlineData(1, -1, -1, "maxAge")]
    [InlineData(1, 100, 0, "sweepInterval")]
    [InlineData(1, 100, -1, "sweepInterval")]
    [InlineData(1, 100, 101, "sweepInterval")]
    [InlineData(1, long.MaxValue, 864000000001, "sweepInterval")]
    public void Factory_RejectsFirstInvalidParameterInSignatureOrder(int count, long age, long interval, string parameter)
    {
        var action = () => EphemeralTerminalRetentionPolicy.Create(count, TimeSpan.FromTicks(age), TimeSpan.FromTicks(interval));
        action.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(parameter);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(long.MaxValue, 864000000000)]
    public void Factory_PreservesEveryValidPositiveInterval(long age, long interval)
    {
        var policy = EphemeralTerminalRetentionPolicy.Create(2, TimeSpan.FromTicks(age), TimeSpan.FromTicks(interval));
        policy.MaxRetainedInstances.Should().Be(2);
        policy.MaxAge.Ticks.Should().Be(age);
        policy.SweepInterval.Ticks.Should().Be(interval);
    }

    [Fact]
    public void Omission_IsTheNullCompatibilityDefault() => new EphemeralEngineHostOptions().TerminalRetention.Should().BeNull();

    [Theory]
    [InlineData("count")]
    [InlineData("age")]
    [InlineData("interval")]
    [InlineData("disabled")]
    public void Registration_EqualCopiesCompose_AnyPolicyDifferenceRejectsBeforeMutation(string change)
    {
        var services = new ServiceCollection();
        EphemeralEngineHostOptions Options(EphemeralTerminalRetentionPolicy? policy) => new()
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            TransientPools = [], TerminalRetention = policy
        };
        EphemeralTerminalRetentionPolicy Policy(int count = 2, int age = 10, int interval = 1) =>
            EphemeralTerminalRetentionPolicy.Create(count, TimeSpan.FromSeconds(age), TimeSpan.FromSeconds(interval));
        services.AddOrcaCoreEphemeralEngine(Options(Policy()));
        var count = services.Count;
        services.AddOrcaCoreEphemeralEngine(Options(Policy()));
        services.Count.Should().Be(count);
        var different = change switch { "count" => Policy(count: 3), "age" => Policy(age: 11), "interval" => Policy(interval: 2), _ => null };
        var action = () => services.AddOrcaCoreEphemeralEngine(Options(different));
        action.Should().Throw<InvalidOperationException>();
        services.Count.Should().Be(count);
    }
}
