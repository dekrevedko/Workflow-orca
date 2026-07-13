using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedInstance(
    string InstanceId,
    string DefinitionId,
    string? DefinitionVersion,
    int ConcurrencyToken,
    JsonElement BusinessState,
    PersistedRuntimeState RuntimeState);
