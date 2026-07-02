using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests;

public sealed class SkeletonTests
{
    [Fact]
    public void ProjectWiring_Compiles_AndRuns()
    {
        true.Should().BeTrue();
    }
}
