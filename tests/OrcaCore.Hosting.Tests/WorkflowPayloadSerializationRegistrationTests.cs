using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class WorkflowPayloadSerializationRegistrationTests
{
    [Fact]
    public void AddOrcaCore_DoesNotResolveAnApplicationSerializerReplacement()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ApplicationSerializerReplacement());
        services.AddOrcaCore();
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<DurableWorkflowRuntime>().Should().NotBeNull();
        services.Should().NotContain(
            descriptor => descriptor.ServiceType.Name.Contains(
                "WorkflowPayloadSerializer",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ProductSurface_HasNoSerializerReplacementHook()
    {
        var publicTypes = typeof(OrcaCoreServiceCollectionExtensions).Assembly
            .GetExportedTypes()
            .Concat(typeof(IWorkflowEventStore).Assembly.GetExportedTypes())
            .Concat(typeof(DurableWorkflowRuntime).Assembly.GetExportedTypes())
            .Select(type => type.FullName)
            .ToArray();
        var publicRuntimeConstructorParameterTypes = typeof(DurableWorkflowRuntime)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters())
            .Select(parameter => parameter.ParameterType.FullName)
            .ToArray();
        var services = new ServiceCollection();
        services.AddSingleton(new ApplicationSerializerReplacement());
        services.AddOrcaCore();

        publicTypes.Should().NotContain("OrcaCore.Abstractions.Providers.IWorkflowPayloadSerializer");
        publicTypes.Should().NotContain("OrcaCore.Abstractions.Providers.IWorkflowPayloadCodec");
        publicTypes.Should().NotContain("OrcaCore.Hosting.WorkflowPayloadSerializationOptions");
        publicTypes.Should().NotContain("OrcaCore.Engine.Durable.Execution.ContentTypeWorkflowPayloadSerializer");
        publicRuntimeConstructorParameterTypes.Should().NotContain(
            typeName => typeName != null &&
                        typeName.Contains("WorkflowPayloadSerializer", StringComparison.Ordinal));
        services.Should().NotContain(
            descriptor => descriptor.ServiceType.Name.Contains(
                "WorkflowPayloadSerializer",
                StringComparison.Ordinal));
    }

    private sealed class ApplicationSerializerReplacement;

}
