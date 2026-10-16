using System.Data;
using AwesomeAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.SqlServer.Internal;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Providers.SqlServer.Tests;

using DurableWorkflowEvent = global::OrcaCore.Abstractions.Durable.WorkflowEvent;

[Trait(Traits.Container, "SqlServer")]
[Collection(SqlServerCollection.Name)]
public sealed class SqlServerProviderInfrastructureTests(SqlServerContainerFixture container)
{
    [Fact]
    public async Task AddOrcaCoreSqlServerDurableProvider_RegistersTheCompleteProviderRole()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreSqlServerDurableProvider(
            new SqlServerDurableProviderOptions(container.ConnectionString, "orcacore_registration"));

        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWorkflowEventStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowInboxStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowStartIdempotencyStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowOutboxStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowProjectionStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowOperationalStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IWorkflowProviderMaintenanceStore>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<ITimerScheduler>().Should().BeOfType<SqlServerWorkflowStore>();
        provider.GetRequiredService<IResourcePoolStore>().Should().BeOfType<SqlServerResourcePoolStore>();
        provider.GetRequiredService<IDurableResourceGovernanceStore>()
            .Should().BeOfType<SqlServerResourceGovernanceStore>();
        provider.GetServices<IHostedService>()
            .Should().ContainSingle()
            .Which.Should().BeOfType<SqlServerProviderInitializationHostedService>();
    }

    [Fact]
    public void AddOrcaCoreSqlServerDurableProvider_IsIdempotentOnlyForTheSameImmutableProfile()
    {
        var options = new SqlServerDurableProviderOptions(container.ConnectionString, "orcacore_registration");
        var services = new ServiceCollection();

        services.AddOrcaCoreSqlServerDurableProvider(options);
        var same = () => services.AddOrcaCoreSqlServerDurableProvider(options);
        var different = () => services.AddOrcaCoreSqlServerDurableProvider(
            new SqlServerDurableProviderOptions(container.ConnectionString, "other_schema"));

        same.Should().NotThrow();
        different.Should().Throw<InvalidOperationException>().WithMessage("*different options*");
    }

    [Fact]
    public void AddOrcaCoreSqlServerDurableProvider_RejectsPreRegisteredPartialPorts()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowEventStore, StubEventStore>();

        var act = () => services.AddOrcaCoreSqlServerDurableProvider(
            new SqlServerDurableProviderOptions(container.ConnectionString, "orcacore_registration"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*IWorkflowEventStore*");
    }

    [Fact]
    public async Task Restart_RehydratesCommittedWorkflowStateFromSqlServer()
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        var first = new SqlServerWorkflowStore(await container.CreateDocumentsAsync(schema), TimeProvider.System);
        var streamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        var append = await first.AppendAsync(Batch(streamId), TestContext.Current.CancellationToken);

        var restarted = new SqlServerWorkflowStore(await container.CreateDocumentsAsync(schema), TimeProvider.System);
        var tail = await restarted.LoadTailAsync(
            streamId,
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        append.IsSuccess.Should().BeTrue();
        tail.Should().ContainSingle().Which.Should().BeOfType<WorkflowStartedEvent>();
    }

    [Fact]
    public async Task CompetingHosts_SerializeTheSameExpectedVersionAcrossIndependentStores()
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        var first = new SqlServerWorkflowStore(await container.CreateDocumentsAsync(schema), TimeProvider.System);
        var second = new SqlServerWorkflowStore(await container.CreateDocumentsAsync(schema), TimeProvider.System);
        var streamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));

        var results = await Task.WhenAll(
            first.AppendAsync(Batch(streamId), TestContext.Current.CancellationToken),
            second.AppendAsync(Batch(streamId), TestContext.Current.CancellationToken));

        results.Count(result => result.IsSuccess).Should().Be(1);
        results.Count(result => result.IsFailure).Should().Be(1);
    }

    [Fact]
    public async Task ReadOnlyPorts_DoNotRewriteThePersistedWorkflowState()
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        var store = new SqlServerWorkflowStore(await container.CreateDocumentsAsync(schema), TimeProvider.System);
        var streamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        _ = await store.AppendAsync(Batch(streamId), TestContext.Current.CancellationToken);
        var revisionBefore = await ReadStateRevisionAsync(schema);

        _ = await store.LoadTailAsync(
            streamId,
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);
        var statistics = await store.GetOperatorStatisticsAsync(TestContext.Current.CancellationToken);

        (await ReadStateRevisionAsync(schema)).Should().Be(
            revisionBefore,
            "read-only provider ports must not create a storage mutation or a competing-writer conflict");
        statistics.ProviderName.Should().Be(
            OrcaCore.Abstractions.Diagnostics.OrcaCoreDiagnostics.SqlServerProviderName);
    }

    [Fact]
    public async Task MigrationJournal_IsIdempotentAndRejectsAnAppliedIdentifierWithDifferentContent()
    {
        var schema = SqlServerContainerFixture.CreateSchemaName();
        _ = await container.CreateDocumentsAsync(schema);
        _ = await container.CreateDocumentsAsync(schema);

        await using var connection = new SqlConnection(container.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var count = connection.CreateCommand();
        count.CommandText = $"select count(*) from [{schema}].[orcacore_migrations];";
        Convert.ToInt32(await count.ExecuteScalarAsync(TestContext.Current.CancellationToken))
            .Should().Be(1);

        await using var corrupt = connection.CreateCommand();
        corrupt.CommandText = $"update [{schema}].[orcacore_migrations] set [content_digest] = @digest;";
        corrupt.Parameters.Add(new SqlParameter("@digest", SqlDbType.Char, 64) { Value = new string('0', 64) });
        _ = await corrupt.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        var act = () => container.CreateDocumentsAsync(schema);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*different content digest*");
    }

    private async Task<long> ReadStateRevisionAsync(string schema)
    {
        await using var connection = new SqlConnection(container.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"select [state_revision] from [{schema}].[orcacore_provider_state] where [state_key] = @stateKey;";
        command.Parameters.Add(new SqlParameter("@stateKey", SqlDbType.NVarChar, 128)
        {
            Value = SqlServerStateDocumentStore.WorkflowStateKey
        });
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(TestContext.Current.CancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static ProviderCommitBatch Batch(WorkflowStreamId streamId) => new()
    {
        StreamId = streamId,
        ExpectedVersion = StreamVersion.Empty,
        Events = [Started(streamId.InstanceId)]
    };

    private static WorkflowStartedEvent Started(InstanceId instanceId) => new()
    {
        EventId = EventId.Create(Guid.CreateVersion7().ToString()),
        InstanceId = instanceId,
        CommandId = CommandId.New(),
        CausationId = CausationId.New(),
        OccurredAt = DateTimeOffset.UnixEpoch,
        DefinitionId = DefinitionId.New(),
        DefinitionVersion = DefinitionVersion.Initial
    };

    private sealed class StubEventStore : IWorkflowEventStore
    {
        public Task<Option<CheckpointWrite>> LoadCheckpointAsync(
            InstanceId instanceId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<AppendEventsResult>> AppendAsync(
            ProviderCommitBatch batch,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<DurableWorkflowEvent>> LoadTailAsync(
            WorkflowStreamId streamId,
            StreamVersion afterVersion,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
