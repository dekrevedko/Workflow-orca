using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class EngineRegistrationProfileTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DelimitersInPoolNamesCannotHideAConflictingProfile(bool durable)
    {
        var services = new ServiceCollection();
        if (durable) services.AddOrcaCoreInMemoryDurableProvider();
        Register(services, durable, [("a", 1), ("b", 2)]);
        var original = services.ToArray();

        Assert.Throws<InvalidOperationException>(() => Register(services, durable,
            [(durable ? "a=1:1,b" : "a=1,b", 2)]));
        Assert.Equal(original, services.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EquivalentProfilesAreIdempotentRegardlessOfPoolOrder(bool durable)
    {
        var services = new ServiceCollection();
        if (durable) services.AddOrcaCoreInMemoryDurableProvider();
        Register(services, durable, [("a", 1), ("b", 2)]);
        var original = services.ToArray();
        Register(services, durable, [("b", 2), ("a", 1)]);
        Assert.Equal(original, services.ToArray());
    }

    [Theory]
    [InlineData(false, "paths")]
    [InlineData(true, "paths")]
    [InlineData(false, "throttles")]
    [InlineData(true, "throttles")]
    [InlineData(false, "capacity")]
    [InlineData(true, "capacity")]
    [InlineData(false, "retention")]
    [InlineData(true, "partition")]
    [InlineData(true, "review")]
    public void EveryBehavioralProfileDifferenceIsRejectedBeforeMutation(bool durable, string change)
    {
        var services = new ServiceCollection();
        if (durable) services.AddOrcaCoreInMemoryDurableProvider();
        Register(services, durable, [("a", 1)]);
        var original = services.ToArray();
        Assert.Throws<InvalidOperationException>(() => Register(services, durable,
            [("a", change == "capacity" ? 2 : 1)],
            maxPaths: change == "paths" ? 3 : 2,
            throttles: change == "throttles" ? [StepExecutionThrottle.For<FirstStep>(1)] : [],
            partition: change == "partition" ? "another" : "profile-tests",
            reviewTicks: change == "review" ? 2 : 1,
            retention: change == "retention" ? EphemeralTerminalRetentionPolicy.Create(5, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) : null));
        Assert.Equal(original, services.ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EquivalentThrottleOrderAndRetentionValuesAreIdempotent(bool durable)
    {
        var services = new ServiceCollection();
        if (durable) services.AddOrcaCoreInMemoryDurableProvider();
        Register(services, durable, [], throttles: [StepExecutionThrottle.For<FirstStep>(1), StepExecutionThrottle.For<SecondStep>(2)],
            retention: EphemeralTerminalRetentionPolicy.Create(5, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)));
        var original = services.ToArray();
        Register(services, durable, [], throttles: [StepExecutionThrottle.For<SecondStep>(2), StepExecutionThrottle.For<FirstStep>(1)],
            retention: EphemeralTerminalRetentionPolicy.Create(5, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)));
        Assert.Equal(original, services.ToArray());
    }

    private static void Register(ServiceCollection services, bool durable, (string Name, int Capacity)[] pools,
        int maxPaths = 2, StepExecutionThrottle[]? throttles = null, string partition = "profile-tests",
        long reviewTicks = 1, EphemeralTerminalRetentionPolicy? retention = null)
    {
        var execution = new StructuredExecutionHostOptions
        {
            MaxConcurrentExecutionPathsPerInstance = maxPaths, StepThrottles = throttles ?? []
        };
        if (durable)
            services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
            {
                StructuredExecution = execution,
                ResourcePools = new DurableResourcePoolOptions
                {
                    PartitionId = ResourceGovernancePartitionId.Create(partition),
                    Pools = pools.Select(p => DurableResourcePoolDefinition.Create(
                        ResourcePoolName.Create(p.Name), p.Capacity, TimeSpan.FromTicks(reviewTicks))).ToArray()
                }
            });
        else
            services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
            {
                StructuredExecution = execution,
                TerminalRetention = retention,
                TransientPools = pools.Select(p => TransientPoolDefinition.Create(
                    TransientPoolName.Create(p.Name), p.Capacity)).ToArray()
            });
    }

    private sealed class FirstStep : IStep<int>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class SecondStep : IStep<int>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
