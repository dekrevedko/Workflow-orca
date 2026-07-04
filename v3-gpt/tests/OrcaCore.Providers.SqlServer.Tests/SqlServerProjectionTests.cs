using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using Testcontainers.MsSql;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

public sealed class SqlServerProjectionTests : IAsyncLifetime
{
    private readonly MsSqlContainer container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("OrcaCore!123")
        .Build();
    private SqlServerWorkflowStore? store;

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        store = new SqlServerWorkflowStore(container.GetConnectionString());
        await store.InitializeAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (store is not null)
        {
            await store.DisposeAsync();
        }

        await container.DisposeAsync();
    }

    [Fact]
    public async Task ProjectionQueries_MetadataFilters_ReturnExpectedRows()
    {
        var definitionId = DefinitionIdValue(1);
        await RequiredStore().ApplyAsync(
            [
                Upsert(InstanceIdValue(1), definitionId, WorkflowStatus.Running),
                Upsert(InstanceIdValue(2), definitionId, WorkflowStatus.Completed),
                Upsert(InstanceIdValue(3), DefinitionIdValue(2), WorkflowStatus.Running)
            ],
            TestContext.Current.CancellationToken);

        var snapshots = await RequiredStore().ListAsync(
            new WorkflowProjectionQuery
            {
                DefinitionId = definitionId,
                Status = WorkflowStatus.Running
            },
            TestContext.Current.CancellationToken);

        snapshots.Should().ContainSingle()
            .Which.InstanceId.Should().Be(InstanceIdValue(1));
    }

    [Fact]
    [Trait("AC", "DU-071")]
    public async Task AppendHistoryProjection_PersistsHistoryRow()
    {
        var instanceId = InstanceIdValue(4);
        await RequiredStore().ApplyAsync(
            [
                new ProjectionWrite(instanceId, ProjectionOperationKind.AppendHistory)
                {
                    History = new ProjectionHistoryWrite(
                        GuidValue(4),
                        Timestamp(4),
                        "operator-note",
                        """{"message":"created"}""")
                }
            ],
            TestContext.Current.CancellationToken);

        var count = await CountHistoryRowsAsync(instanceId);

        count.Should().Be(1);
    }

    private async Task<int> CountHistoryRowsAsync(InstanceId instanceId)
    {
        await using var connection = new SqlConnection(container.GetConnectionString());
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new SqlCommand(
            """
            select count(*)
            from dbo.orcacore_history_projections
            where instance_id = @instance_id;
            """,
            connection);
        command.Parameters.AddWithValue("@instance_id", instanceId.Value);

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    private SqlServerWorkflowStore RequiredStore()
    {
        return store ?? throw new InvalidOperationException("SQL Server projection store is not initialized.");
    }

    private static ProjectionWrite Upsert(
        InstanceId instanceId,
        DefinitionId definitionId,
        WorkflowStatus status)
    {
        return new ProjectionWrite(instanceId, ProjectionOperationKind.UpsertSummary)
        {
            InstanceSnapshot = new WorkflowInstanceSnapshot
            {
                InstanceId = instanceId,
                RootInstanceId = instanceId,
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial,
                Status = status,
                CreatedAt = Timestamp(1),
                UpdatedAt = Timestamp(1)
            }
        };
    }

    private static DateTimeOffset Timestamp(int seconds)
    {
        return new DateTimeOffset(2026, 7, 2, 12, 0, seconds, TimeSpan.Zero);
    }

    private static InstanceId InstanceIdValue(int value)
    {
        return new InstanceId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static DefinitionId DefinitionIdValue(int value)
    {
        return new DefinitionId(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    }

    private static Guid GuidValue(int value)
    {
        return Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}");
    }
}
