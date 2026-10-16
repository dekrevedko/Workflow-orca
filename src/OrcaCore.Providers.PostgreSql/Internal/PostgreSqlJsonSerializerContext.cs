using System.Text.Json;
using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.PostgreSql;

[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(WorkflowRuntimeCheckpointState))]
internal sealed partial class PostgreSqlJsonSerializerContext : JsonSerializerContext;
