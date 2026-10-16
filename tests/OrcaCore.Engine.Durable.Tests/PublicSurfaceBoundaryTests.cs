using System.Runtime.CompilerServices;
using AwesomeAssertions;
using OrcaCore.Engine.Durable.Execution;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests;

public sealed class PublicSurfaceBoundaryTests
{
    [Fact]
    public void DurableEngine_ExportsNoTypes()
    {
        typeof(DurableCommandProcessor).Assembly.GetExportedTypes().Should().BeEmpty();
    }

    [Fact]
    public void DurableEngine_UsesOnlyTheApprovedFriendAssemblies()
    {
        typeof(DurableCommandProcessor).Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false)
            .Cast<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .Should().BeEquivalentTo(
                "OrcaCore.ProviderCertification",
                "OrcaCore.Durable.Hosting",
                "OrcaCore.Engine.Durable.Tests");
    }
}
