using System.Text.Json;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using OrcaCore.TestSupport;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class ProtectedStepLeaseRecoveryTests
{
    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    [Trait(Traits.Container, "PostgreSql")]
    public Task PostgreSqlMissingObligationRestart(bool afterAdmission) =>
        RunMissingObligationRestartAsync("PostgreSql", afterAdmission);

    [Theory(Explicit = true)]
    [InlineData(false)]
    [InlineData(true)]
    [Trait(Traits.Container, "SqlServer")]
    public Task SqlServerMissingObligationRestart(bool afterAdmission) =>
        RunMissingObligationRestartAsync("SqlServer", afterAdmission);

    private sealed record HistoricalCut(
        int Pid, Guid Instance, long Version, string Path, Guid Ticket, string Holder, string Protection,
        string? Operation, int Attempt, string Instruction);

    private sealed class HistoricalWriter(string directory, string cut) : DagEventStoreProbe, IWorkflowStartIdempotencyStore
    {
        public Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(string key, CancellationToken token) =>
            ((IWorkflowStartIdempotencyStore)Inner).GetStartedAsync(key, token);

        public override async Task<Result<AppendEventsResult>> AppendAsync(ProviderCommitBatch batch, CancellationToken token)
        {
            if (batch.Checkpoint is not { LastStepPath: { } path } checkpoint || !path.EndsWith(cut, StringComparison.Ordinal))
                return await base.AppendAsync(batch, token);

            // Reproduce the old consumed-grant envelope at the chosen durable position. The
            // provider commits the genuine events, runtime ticket and all other batch operations.
            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
            var lease = Assert.Single(envelope.OwnedObligations, row => row.Kind == DurableOwnedObligationKind.Resource);
            Assert.Equal("Held", lease.LeasePhase);
            var ticket = Assert.Single(lease.LeaseTickets);
            var fiber = Assert.Single(envelope.Fibers, f => f.FiberId == lease.FiberId);
            var historical = envelope with
            {
                OwnedObligations = envelope.OwnedObligations.Where(row => row != lease).ToArray()
            };
            var result = await base.AppendAsync(batch with
            {
                Checkpoint = checkpoint with { Payload = historical.Serialize() }
            }, token);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
            var marker = new HistoricalCut(Environment.ProcessId, checkpoint.InstanceId.Value, result.Value.NewVersion.Value,
                path, Guid.Parse(ticket.TicketId), lease.HolderKey!, lease.ProtectionToken!,
                fiber.LogicalOperationKey, fiber.RetryAttempt, fiber.InstructionId);
            var file = Path.Combine(directory, "cut.json");
            await File.WriteAllTextAsync(file + ".tmp", JsonSerializer.Serialize(marker), token);
            File.Move(file + ".tmp", file);
            await Task.Delay(Timeout.InfiniteTimeSpan, token); // Controller kills and joins this process.
            return result;
        }
    }

    private static ProtectedStep ObservePhysicalEntry(string directory) =>
        new(_ => Task.CompletedTask, () => File.AppendAllText(Path.Combine(directory, "physical-entries"), $"{Environment.ProcessId}\n"));

    private static async Task SeedMissingObligationAsync(string provider, string connection, string directory, string cut,
        CancellationToken token)
    {
        var definition = Definition();
        var probe = new HistoricalWriter(directory, cut);
        var step = ObservePhysicalEntry(directory);
        while (true)
        {
            await using var host = await HostAsync(provider, connection, step, definition, token, probe);
            var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(definition.Reference);
            await handle.StartOrGetAsync(1, StartIdempotencyKey.Create("owner"), token);
            token.ThrowIfCancellationRequested();
        }
    }

    private static async Task RunMissingObligationRestartAsync(string provider, bool afterAdmission)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(4));
        var token = deadline.Token;
        await using IDatabaseContainer database = provider == "PostgreSql"
            ? new PostgreSqlBuilder("postgres:17-alpine").Build()
            : new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await database.StartAsync(token);
        var connection = database.GetConnectionString();
        var directory = Directory.CreateTempSubdirectory("orca-historical-lease-");
        try
        {
            var cut = afterAdmission ? ":attempt-admission" : ":lease-activated";
            HistoricalCut marker;
            await using (var worker = new OwnerProcess(provider, connection, directory.FullName, cut))
            {
                var file = await worker.WaitForFileAsync("cut.json", token);
                marker = JsonSerializer.Deserialize<HistoricalCut>(await File.ReadAllTextAsync(file, token))!;
                Assert.Equal(worker.Id, marker.Pid);
                Assert.NotEqual(Environment.ProcessId, marker.Pid);
                Assert.EndsWith(cut, marker.Path, StringComparison.Ordinal);
                await worker.KillAndJoinAsync();
            }

            var owner = InstanceId.Parse(marker.Instance.ToString());
            var definition = Definition();
            var step = ObservePhysicalEntry(directory.FullName);
            var entries = Path.Combine(directory.FullName, "physical-entries");
            Assert.False(File.Exists(entries), "Seed worker entered the protected body.");
            await using var observer = await HostAsync(provider, connection, step, definition, token);
            var checkpoint = await CheckpointAsync(observer, owner, token);
            Assert.Equal(marker.Version, checkpoint.StreamVersion.Value);
            Assert.Equal(marker.Path, checkpoint.LastStepPath);
            Assert.Equal(WorkflowInstanceStatus.Running, checkpoint.Status);
            var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload);
            Assert.Empty(envelope.OwnedObligations);
            Assert.Equal(1, JsonSerializer.Deserialize<int>(envelope.StatePayload));
            Assert.Null(envelope.Output);
            var fiber = Assert.Single(envelope.Fibers);
            Assert.Equal(marker.Instruction, fiber.InstructionId);
            Assert.Equal(afterAdmission, fiber.AttemptInFlight);
            Assert.Equal(marker.Operation, fiber.LogicalOperationKey);
            Assert.Equal(marker.Attempt, fiber.RetryAttempt);
            Assert.Equal(afterAdmission, !string.IsNullOrEmpty(fiber.LogicalOperationKey));
            Assert.Equal(afterAdmission ? 1 : 0, fiber.RetryAttempt);
            var ticket = Assert.Single(checkpoint.RuntimeState.ActiveResourceTickets);
            Assert.Equal(marker.Ticket, ticket.TicketId);
            Assert.Equal(marker.Holder, ticket.HolderKey);
            Assert.Equal(owner, ticket.HolderInstanceId);
            Assert.Equal(Pool.Value, ticket.PoolName);
            Assert.Equal(1, ticket.Count);
            Assert.Equal(1, ticket.ProviderGeneration);
            Assert.Empty(checkpoint.RuntimeState.ActiveWaits);
            Assert.Empty(checkpoint.RuntimeState.PendingResumes);
            var journal = await JournalAsync(observer, owner, token);
            var acquired = Assert.Single(journal.OfType<WorkflowResourcePoolAcquiredEvent>());
            Assert.Equal(ticket.HolderKey, acquired.HolderKey);
            Assert.Equal(ticket, Assert.Single(acquired.Tickets));
            var activation = Assert.Single(journal.OfType<WorkflowStepCompletedEvent>(),
                e => e.StepPath.EndsWith(":lease-activated", StringComparison.Ordinal));
            var admitted = journal.SkipWhile(e => e.EventId != activation.EventId).Skip(1).ToArray();
            if (afterAdmission) Assert.IsType<WorkflowExecutionPositionChangedEvent>(Assert.Single(admitted));
            else Assert.Empty(admitted);
            Assert.DoesNotContain(journal.OfType<WorkflowStepCompletedEvent>(), e => e.StepPath == "root/1/lease/0");
            Assert.Empty(journal.OfType<WorkflowTerminalEvent>());
            Assert.Empty(journal.OfType<WorkflowResourcePoolReleasedEvent>());
            var governance = observer.GetRequiredService<IDurableResourceGovernanceStore>();
            var head = (await governance.LoadLatestAsync(Partition, token)).Version;
            var pools = observer.GetRequiredService<IResourcePoolStore>();
            async Task AssertOwnershipAsync()
            {
                var pool = (await pools.GetPoolAsync(Pool.Value, token)).Value;
                Assert.Equal(1, pool.Capacity);
                Assert.Equal(0, pool.AvailableCapacity);
                Assert.Equal(ticket, Assert.Single(pool.HeldTickets));
                Assert.Empty(pool.QueuedWaiters);
                Assert.False((await pools.GetReleaseEvidenceAsync(LeaseProtectionToken.Parse(marker.Protection), token)).HasValue);
                Assert.Equal(head, (await governance.LoadLatestAsync(Partition, token)).Version);
            }

            await AssertOwnershipAsync();
            EventId? firstPark = null;
            for (var reopen = 0; reopen < 2; reopen++)
            {
                await using var host = await HostAsync(provider, connection, step, definition, token);
                var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>().GetRequiredHandle(definition.Reference);
                var binding = await handle.StartOrGetAsync(1, StartIdempotencyKey.Create("owner"), token);
                Assert.Equal(owner, binding.GetHandleOrThrow().InstanceId);
                Assert.False(File.Exists(entries), $"Physical protected step entered after {marker.Path}, seed PID {marker.Pid}.");
                var current = await JournalAsync(observer, owner, token);
                Assert.Equal(journal.Select(e => e.EventId), current.Take(journal.Count).Select(e => e.EventId));
                var parked = Assert.IsType<WorkflowParkedEvent>(Assert.Single(current.Skip(journal.Count)));
                Assert.Equal(DurableParkReason.Poison, parked.Reason);
                Assert.Equal(checkpoint.StreamVersion, parked.PositionStreamVersion);
                if (firstPark is not null) Assert.Equal(firstPark, parked.EventId);
                firstPark = parked.EventId;
                var retained = await CheckpointAsync(observer, owner, token);
                Assert.Equal(checkpoint.Payload, retained.Payload);
                Assert.Equal(checkpoint.StreamVersion, retained.StreamVersion);
                Assert.Equal(checkpoint.LastStepPath, retained.LastStepPath);
                Assert.Equal(ticket, Assert.Single(retained.RuntimeState.ActiveResourceTickets));
                await AssertOwnershipAsync();
            }
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"{provider} killed seed {marker.Pid} at {marker.Path}@{marker.Version}; original operation={marker.Operation}, " +
                $"attempt={marker.Attempt}, ticket={marker.Ticket}, governance={head}, park={firstPark}, physical entries=0.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
