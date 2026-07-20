using AwesomeAssertions;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class PublicSurfaceExpectedRedGuards
{
    [Fact]
    public void PublicSignatures_RespectRecursiveTierClosure()
    {
        PublicSurfaceCatalog.FindForbiddenSignatureEdges().Should().BeEmpty(
            "application signatures may reference only application types, protocol may not reference providers, " +
            "and provider authoring may not expose implementation types");
    }

    [Fact]
    public void Definitions_AreModeSpecificAndCompiledIrIsOpaque()
    {
        var core = typeof(WorkflowDefinition<>).Assembly;
        var ephemeral = core.GetType("OrcaCore.Core.Definitions.EphemeralWorkflowDefinition`1");
        var durable = core.GetType("OrcaCore.Core.Definitions.DurableWorkflowDefinition`1");
        ephemeral.Should().NotBeNull();
        durable.Should().NotBeNull();

        foreach (var definition in new[] { ephemeral!, durable! })
        {
            PublicSurfaceCatalog.FindCompilerIrSignatureTypes(definition).Should().BeEmpty(
                $"{definition.Name} and all inherited public members must hide compiled/compiler/plan types");
        }
    }

    [Fact]
    public void ActiveWait_ExposesAuthoredLocation()
    {
        typeof(ActiveWaitSnapshot).GetProperty("AuthoredLocation").Should().NotBeNull();
    }

    [Fact]
    public void ActiveWait_UsesOpaqueWaitIdAndNoRoutingIdentity()
    {
        var type = typeof(ActiveWaitSnapshot);
        type.GetProperty("WaitId")!.PropertyType.Should().Be(typeof(WaitId));
        typeof(WaitId).IsValueType.Should().BeTrue();
        typeof(WaitId).GetProperties().Select(property => property.Name).Should().Equal("Value");
        type.GetProperty("FiberId").Should().BeNull();
        type.GetProperty("ScopeId").Should().BeNull();
        type.GetProperty("WaitSequence").Should().BeNull();
    }

    [Theory]
    [InlineData("WorkflowInstanceQueryModel")]
    [InlineData("WorkflowStatistics")]
    [InlineData("WorkflowStatisticsGroup")]
    [InlineData("DestructiveOperationConfirmation")]
    public void ApplicationModels_HaveExactlyOneCanonicalDeclaration(string name)
    {
        var declarations = PublicSurfaceCatalog.ExportedTypes
            .Select(entry => entry.Type)
            .Where(type => type.Name == name)
            .ToArray();
        declarations.Should().ContainSingle($"{name} must have one application-tier declaration");
        declarations[0].Assembly.GetName().Name.Should().Be("OrcaCore.Abstractions");
    }
}
