using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrcaCore.Abstractions.Providers;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(WorkflowRuntimeCheckpointState))]
public sealed partial class ProviderJsonSerializerContext : JsonSerializerContext;
