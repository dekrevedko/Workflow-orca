using System.Data;
using Microsoft.Data.SqlClient;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.SqlServer;

internal static class SqlServerProjectionQueryBuilder
{
    public const string SummaryWhereClause =
        """
        where (@instance_id is null or summary.instance_id = @instance_id)
          and (@parent_instance_id is null or summary.parent_instance_id = @parent_instance_id)
          and (@root_instance_id is null or summary.root_instance_id = @root_instance_id)
          and (@definition_id is null or summary.definition_id = @definition_id)
          and (@definition_version is null or summary.definition_version = @definition_version)
          and (@status is null or summary.status = @status)
          and (@wait_event_name is null or exists (
              select 1
              from dbo.orcacore_active_wait_projections matched_wait
              where matched_wait.instance_id = summary.instance_id
                and matched_wait.event_name = @wait_event_name))
          and (@wait_correlation_id is null or exists (
              select 1
              from dbo.orcacore_active_wait_projections matched_wait
              where matched_wait.instance_id = summary.instance_id
                and matched_wait.correlation_id = @wait_correlation_id))
        """;

    public static void AddParameters(SqlCommand command, WorkflowProjectionQuery query)
    {
        AddGuid(command, "@instance_id", query.InstanceId?.Value);
        AddGuid(command, "@parent_instance_id", query.ParentInstanceId?.Value);
        AddGuid(command, "@root_instance_id", query.RootInstanceId?.Value);
        AddGuid(command, "@definition_id", query.DefinitionId?.Value);
        AddDefinitionVersion(command, query.DefinitionVersion);
        AddString(command, "@status", query.Status?.ToString(), 64);
        AddString(command, "@wait_event_name", query.ActiveWaitEventName, 256);
        AddString(command, "@wait_correlation_id", query.ActiveWaitCorrelationId?.Value, 512);
    }

    private static void AddGuid(SqlCommand command, string name, Guid? value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.UniqueIdentifier);
        parameter.Value = value ?? (object)DBNull.Value;
    }

    private static void AddDefinitionVersion(SqlCommand command, DefinitionVersion? value)
    {
        var parameter = command.Parameters.Add("@definition_version", SqlDbType.Int);
        parameter.Value = value?.Value ?? (object)DBNull.Value;
    }

    private static void AddString(SqlCommand command, string name, string? value, int size)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.NVarChar, size);
        parameter.Value = value ?? (object)DBNull.Value;
    }
}
