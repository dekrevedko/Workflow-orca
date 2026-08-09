using System.Runtime.CompilerServices;
using AwesomeAssertions;
using OrcaCore.Hosting.ResourceLeases;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class PublicSurfaceBoundaryTests
{
    [Fact]
    public void DurableHosting_ExportsOnlyTheDocumentedOptionsContractsAndExtension()
    {
        typeof(OrcaCoreDurableEngineServiceCollectionExtensions).Assembly
            .GetExportedTypes()
            .Select(type => type.FullName)
            .Should().BeEquivalentTo(
                "OrcaCore.WorkflowEventDispatchFailure",
                "OrcaCore.WorkflowEventDispatchResult",
                "OrcaCore.WorkflowEventDispatchResult+Succeeded",
                "OrcaCore.WorkflowEventDispatchResult+RetryableFailure",
                "OrcaCore.WorkflowEventDispatchResult+PermanentFailure",
                "OrcaCore.Durable.Hosting.IWorkflowEventDispatcher",
                "OrcaCore.Durable.Hosting.IWorkflowEventIngress",
                "OrcaCore.Hosting.DurableEngineHostOptions",
                "OrcaCore.Hosting.DurableResourcePoolDefinition",
                "OrcaCore.Hosting.DurableResourcePoolOptions",
                "OrcaCore.Hosting.OrcaCoreDurableEngineBuilder",
                "OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions",
                "OrcaCore.Hosting.ResourceLeases.IDurableResourcePoolManagement",
                "OrcaCore.Hosting.ResourceLeases.IDurableResourceLeaseRecovery",
                "OrcaCore.Hosting.ResourceLeases.IDurableResourceLeaseDiagnostics");
    }

    [Fact]
    public void DurableHosting_HidesTheResourcePoolImplementation()
    {
        typeof(DurableResourcePoolManagement).IsPublic.Should().BeFalse();
    }

    [Fact]
    public void DurableHosting_UsesOnlyTheApprovedFriendAssemblies()
    {
        typeof(OrcaCoreDurableEngineServiceCollectionExtensions).Assembly
            .GetCustomAttributes(typeof(InternalsVisibleToAttribute), inherit: false)
            .Cast<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName)
            .Should().BeEquivalentTo("OrcaCore.Dag.Hosting", "OrcaCore.Hosting.Tests");
    }
}
