using System.Reflection;
using AwesomeAssertions;
using OrcaCore.Engine.Ephemeral.Execution;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class RuntimeStepContextFactoryContractTests
{
    [Fact]
    public void Factory_HasNoDeadResumedEventReflectionBridge()
    {
        typeof(RuntimeStepContextFactory)
            .GetMethod(
                "CreateResumedEvent",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Should()
            .BeNull();
    }
}
