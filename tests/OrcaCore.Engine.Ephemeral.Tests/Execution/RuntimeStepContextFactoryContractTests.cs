using System.Reflection;
using AwesomeAssertions;
using OrcaCore.Engine.Ephemeral.Execution;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class StepContextContractsContractTests
{
    [Fact]
    public void Factory_HasNoDeadResumedEventReflectionBridge()
    {
        typeof(StepContextContracts)
            .GetMethod(
                "CreateResumedEvent",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Should()
            .BeNull();
    }
}
