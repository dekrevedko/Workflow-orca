using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;
using System.Text.Json.Serialization;

internal static class SupportBootstrap
{
    internal static void Install(bool legacy)
    {
        new ServiceCollection().AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            TransientPools = []
        });
        if (!legacy) WorkflowJsonMetadata.Register<JourneyContext>();
    }
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Data))]
[JsonSerializable(typeof(ValueState))]
[JsonSerializable(typeof(FanState))]
[JsonSerializable(typeof(ItemState))]
[JsonSerializable(typeof(StrongData))]
[JsonSerializable(typeof(Link))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(string[]))]
internal partial class JourneyContext : JsonSerializerContext;
