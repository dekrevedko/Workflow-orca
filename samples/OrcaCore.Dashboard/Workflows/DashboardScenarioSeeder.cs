using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.Dashboard.Workflows;

public sealed class DashboardScenarioSeeder(
    DurableWorkflowRuntime runtime,
    DurableCommandProcessor processor,
    DurableManagement management,
    InMemoryResourcePoolStore resourcePools,
    TimeProvider timeProvider,
    ILogger<DashboardScenarioSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var existing = await management.All().CountAsync(cancellationToken).ConfigureAwait(false);
        if (existing > 0)
        {
            logger.LogInformation("Skipping dashboard demo seed because {InstanceCount} instance(s) already exist.", existing);
            return;
        }

        var definition = BuildDefinition();
        var childDefinitionId = DefinitionId.New();
        var now = timeProvider.GetUtcNow();

        var checkout = await runtime.StartOrGetAsync<DashboardOrderInput, DashboardOrderState>(
            "dashboard/checkout-1001",
            definition,
            new DashboardOrderInput("checkout-1001", "reference fanout"),
            cancellationToken).ConfigureAwait(false);
        await processor.ProcessAsync(
            new DurableRunChildrenCommand(
                CommandId.New(),
                checkout.InstanceId,
                now.AddSeconds(1),
                childDefinitionId,
                DefinitionVersion.Initial,
                ["validate cart", "reserve inventory", "authorize payment", "ship"],
                RunChildFailurePolicy.PropagateFailure,
                MaxConcurrency: 2,
                RunChildrenJoinPolicy.WhenAll,
                RunChildrenResidualPolicy.LetRemainingComplete),
            cancellationToken).ConfigureAwait(false);

        await SeedExternalJobAsync(definition, now.AddSeconds(5), cancellationToken).ConfigureAwait(false);
        await SeedSagaFailureAsync(definition, now.AddSeconds(10), cancellationToken).ConfigureAwait(false);
        await SeedCancelledWorkflowAsync(definition, now.AddSeconds(20), cancellationToken).ConfigureAwait(false);

        logger.LogInformation("Seeded dashboard demo workflows for local inspection.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task SeedExternalJobAsync(
        OrcaCore.Core.Definitions.WorkflowDefinition<DashboardOrderState> definition,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        await resourcePools.UpsertPoolAsync(
            new ResourcePoolDefinition("sample-workers", 1, TimeSpan.FromMinutes(30)),
            cancellationToken).ConfigureAwait(false);
        var job = await runtime.StartOrGetAsync<DashboardOrderInput, DashboardOrderState>(
            "dashboard/risk-job-1002",
            definition,
            new DashboardOrderInput("risk-job-1002", "external job wait"),
            cancellationToken).ConfigureAwait(false);

        await processor.ProcessAsync(
            new RunExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = job.InstanceId,
                RequestedAt = requestedAt,
                ExternalJobId = "risk-score/risk-job-1002",
                Payload = JsonSerializer.SerializeToUtf8Bytes(new DashboardExternalJobPayload(
                    "risk-score",
                    "risk-job-1002")),
                Requirements = [new ResourcePoolRequirement("sample-workers", 1)],
                TimeoutAt = requestedAt.AddMinutes(20)
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedSagaFailureAsync(
        OrcaCore.Core.Definitions.WorkflowDefinition<DashboardOrderState> definition,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        var saga = await runtime.StartOrGetAsync<DashboardOrderInput, DashboardOrderState>(
            "dashboard/saga-1003",
            definition,
            new DashboardOrderInput("saga-1003", "compensation failure"),
            cancellationToken).ConfigureAwait(false);

        await processor.ProcessAsync(
            new RecordSagaForwardActionCompletedCommand
            {
                CommandId = CommandId.New(),
                InstanceId = saga.InstanceId,
                RequestedAt = requestedAt,
                ScopeId = "checkout",
                ActionKey = "reserve-inventory",
                CompensationKey = "release-inventory"
            },
            cancellationToken).ConfigureAwait(false);
        await processor.ProcessAsync(
            new RecordSagaForwardActionCompletedCommand
            {
                CommandId = CommandId.New(),
                InstanceId = saga.InstanceId,
                RequestedAt = requestedAt.AddSeconds(1),
                ScopeId = "checkout",
                ActionKey = "capture-payment",
                CompensationKey = "refund-payment"
            },
            cancellationToken).ConfigureAwait(false);
        await processor.ProcessAsync(
            new RequestSagaCompensationCommand
            {
                CommandId = CommandId.New(),
                InstanceId = saga.InstanceId,
                RequestedAt = requestedAt.AddSeconds(2),
                ScopeId = "checkout",
                Reason = "shipping provider rejected the fulfillment window"
            },
            cancellationToken).ConfigureAwait(false);
        await processor.ProcessAsync(
            new FailSagaCompensationCommand
            {
                CommandId = CommandId.New(),
                InstanceId = saga.InstanceId,
                RequestedAt = requestedAt.AddSeconds(3),
                ScopeId = "checkout",
                ActionKey = "refund-payment",
                ErrorSummary = "payment gateway returned a permanent refund error"
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task SeedCancelledWorkflowAsync(
        OrcaCore.Core.Definitions.WorkflowDefinition<DashboardOrderState> definition,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken)
    {
        var cancelled = await runtime.StartOrGetAsync<DashboardOrderInput, DashboardOrderState>(
            "dashboard/cancelled-1004",
            definition,
            new DashboardOrderInput("cancelled-1004", "operator cancellation"),
            cancellationToken).ConfigureAwait(false);

        await management.CancelAsync(
            cancelled.InstanceId,
            requestedAt,
            cancellationToken).ConfigureAwait(false);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<DashboardOrderState> BuildDefinition()
    {
        return global::OrcaCore.Core.Building.Workflow.Durable<DashboardOrderState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<DashboardOrderInput>(DashboardOrderState.From)
            .End("DashboardSampleStarted")
            .Build();
    }

    private sealed record DashboardOrderInput(string OrderId, string Scenario);

    private sealed record DashboardExternalJobPayload(string Kind, string OrderId);

    private sealed class DashboardOrderState
    {
        public string OrderId { get; set; } = string.Empty;

        public string Scenario { get; set; } = string.Empty;

        public static DashboardOrderState From(DashboardOrderInput input)
        {
            return new DashboardOrderState
            {
                OrderId = input.OrderId,
                Scenario = input.Scenario
            };
        }
    }
}
