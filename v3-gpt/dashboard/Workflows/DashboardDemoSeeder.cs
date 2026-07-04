using System.Text.Json;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;

namespace OrcaCore.Dashboard.Workflows;

public sealed class DashboardDemoSeeder(
    DurableCommandProcessor commandProcessor,
    DurableOutboxPump outboxPump,
    TimeProvider timeProvider,
    ILogger<DashboardDemoSeeder> logger)
{
    private int sequence;

    public async Task<DashboardDemoResult> SeedAsync(CancellationToken cancellationToken)
    {
        var run = Interlocked.Increment(ref sequence);
        var now = timeProvider.GetUtcNow();
        var definitionId = DefinitionId.New();
        var created = new List<InstanceId>();

        created.Add(await StartOnlyAsync(definitionId, run, now, cancellationToken).ConfigureAwait(false));
        created.Add(await WaitingExternalJobAsync(definitionId, run, now.AddSeconds(1), cancellationToken)
            .ConfigureAwait(false));
        created.Add(await TimedOutExternalJobAsync(definitionId, run, now.AddSeconds(2), cancellationToken)
            .ConfigureAwait(false));
        created.Add(await CancelledAsync(definitionId, run, now.AddSeconds(3), cancellationToken)
            .ConfigureAwait(false));
        created.Add(await PermanentDispatchFailureAsync(definitionId, run, now.AddSeconds(4), cancellationToken)
            .ConfigureAwait(false));
        created.Add(await CompensationFailureAsync(definitionId, run, now.AddSeconds(5), cancellationToken)
            .ConfigureAwait(false));

        var dispatched = await outboxPump.PumpOnceAsync(100, cancellationToken).ConfigureAwait(false);
        logger.LogInformation(
            "Dashboard demo run {DashboardDemoRun} created {CreatedCount} instance(s) and dispatched {DispatchedCount} outbox record(s).",
            run,
            created.Count,
            dispatched);

        return new DashboardDemoResult(run, created.Select(instanceId => instanceId.ToString()).ToArray(), dispatched);
    }

    public Task<int> PumpOutboxAsync(CancellationToken cancellationToken)
    {
        return outboxPump.PumpOnceAsync(100, cancellationToken);
    }

    private async Task<InstanceId> StartOnlyAsync(
        DefinitionId definitionId,
        int run,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var instanceId = InstanceId.New();
        await ProcessStartAsync(
            instanceId,
            definitionId,
            run,
            "running-baseline",
            at,
            cancellationToken)
            .ConfigureAwait(false);
        return instanceId;
    }

    private async Task<InstanceId> WaitingExternalJobAsync(
        DefinitionId definitionId,
        int run,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var instanceId = InstanceId.New();
        await ProcessStartAsync(instanceId, definitionId, run, "waiting-approval", at, cancellationToken)
            .ConfigureAwait(false);
        await commandProcessor.ProcessAsync(
            ExternalJob(
                instanceId,
                $"approval-{run}",
                run,
                "approval",
                at.AddSeconds(1),
                at.AddMinutes(30)),
            cancellationToken)
            .ConfigureAwait(false);
        return instanceId;
    }

    private async Task<InstanceId> TimedOutExternalJobAsync(
        DefinitionId definitionId,
        int run,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var instanceId = InstanceId.New();
        var jobId = $"invoice-timeout-{run}";
        await ProcessStartAsync(instanceId, definitionId, run, "timed-out-external-job", at, cancellationToken)
            .ConfigureAwait(false);
        await commandProcessor.ProcessAsync(
            ExternalJob(instanceId, jobId, run, "invoice-export", at.AddSeconds(1), at.AddSeconds(2)),
            cancellationToken)
            .ConfigureAwait(false);
        await commandProcessor.ProcessAsync(
            new TimeoutExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = at.AddSeconds(3),
                ExternalJobId = jobId
            },
            cancellationToken)
            .ConfigureAwait(false);
        return instanceId;
    }

    private async Task<InstanceId> CancelledAsync(
        DefinitionId definitionId,
        int run,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var instanceId = InstanceId.New();
        await ProcessStartAsync(instanceId, definitionId, run, "operator-cancelled", at, cancellationToken)
            .ConfigureAwait(false);
        await commandProcessor.ProcessAsync(
            new CancelWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = at.AddSeconds(1)
            },
            cancellationToken)
            .ConfigureAwait(false);
        return instanceId;
    }

    private async Task<InstanceId> PermanentDispatchFailureAsync(
        DefinitionId definitionId,
        int run,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var instanceId = InstanceId.New();
        await ProcessStartAsync(instanceId, definitionId, run, "permanent-dispatch", at, cancellationToken)
            .ConfigureAwait(false);
        await commandProcessor.ProcessAsync(
            ExternalJob(
                instanceId,
                $"permanent-dispatch-{run}",
                run,
                "shipping-notice",
                at.AddSeconds(1),
                at.AddMinutes(15)),
            cancellationToken)
            .ConfigureAwait(false);
        return instanceId;
    }

    private async Task<InstanceId> CompensationFailureAsync(
        DefinitionId definitionId,
        int run,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var instanceId = InstanceId.New();
        await ProcessStartAsync(instanceId, definitionId, run, "compensation-failed", at, cancellationToken)
            .ConfigureAwait(false);
        await commandProcessor.ProcessAsync(
            new FailSagaCompensationCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = at.AddSeconds(1),
                ScopeId = $"payment-{run}",
                ActionKey = "refund-capture",
                ErrorSummary = "Payment provider rejected refund because the capture was already settled."
            },
            cancellationToken)
            .ConfigureAwait(false);
        return instanceId;
    }

    private Task ProcessStartAsync(
        InstanceId instanceId,
        DefinitionId definitionId,
        int run,
        string lane,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        return commandProcessor.ProcessAsync(
            new StartWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = instanceId,
                RequestedAt = at,
                DefinitionId = definitionId,
                DefinitionVersion = DefinitionVersion.Initial,
                IdempotencyKey = $"dashboard-demo:{run}:{lane}:{instanceId}"
            },
            cancellationToken);
    }

    private static RunExternalJobCommand ExternalJob(
        InstanceId instanceId,
        string externalJobId,
        int run,
        string kind,
        DateTimeOffset at,
        DateTimeOffset timeoutAt)
    {
        return new RunExternalJobCommand
        {
            CommandId = CommandId.New(),
            InstanceId = instanceId,
            RequestedAt = at,
            ExternalJobId = externalJobId,
            Payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                demoRun = run,
                kind,
                externalJobId
            }),
            TimeoutAt = timeoutAt
        };
    }
}

public sealed record DashboardDemoResult(
    int Run,
    IReadOnlyList<string> InstanceIds,
    int DispatchedOutboxRecords);
