using System.Text.Json.Serialization;

namespace OrcaCore;

/// <summary>Configures the process-wide generated metadata for the fixed workflow codec.</summary>
public static class WorkflowJsonMetadata
{
    /// <summary>Registers one ordinary source-generated metadata context before workflow use.</summary>
    public static void Register<TContext>() where TContext : JsonSerializerContext, new() =>
        Internal.WorkflowJsonMetadataRegistry.Register<TContext>();
}
