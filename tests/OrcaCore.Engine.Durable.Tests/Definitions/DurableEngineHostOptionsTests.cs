using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Definitions;

public sealed class DurableEngineHostOptionsTests
{
    [Fact]
    public void AddOrcaCoreDurableEngine_RejectsDuplicateDurablePoolNames()
    {
        var pool = ResourcePoolName.Create("database");
        var options = new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 1,
                StepThrottles = []
            },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = ResourceGovernancePartitionId.Create("host"),
                Pools =
                [
                    DurableResourcePoolDefinition.Create(pool, 1, TimeSpan.FromMinutes(5)),
                    DurableResourcePoolDefinition.Create(
                        ResourcePoolName.Create("database"),
                        2,
                        TimeSpan.FromMinutes(5))
                ]
            }
        };

        Action act = () => new ServiceCollection().AddOrcaCoreDurableEngine(options);

        act.Should().Throw<ArgumentException>().WithMessage("*duplicate*database*");
    }
}
