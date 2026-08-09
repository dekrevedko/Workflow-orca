using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Durable.Hosting;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Services;
using OrcaCore.Providers.InMemory;
using OrcaCore.SampleHost.BrokerAdapters;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class WorkflowEventDispatcherTests
{
    private const string MaterializationFailureCode = "workflow-event-materialization-failed";

    [Fact]
    public async Task BrokerAdapterExamples_AcknowledgeOnlyDurablyOwnedInboundEvents()
    {
        var contract = WorkflowEventContract.Create(
            EventName.Create("broker-adapter-inbound"),
            EventContractVersion.Initial);
        var inbound = WorkflowInboundEvent.Create(
            contract,
            EventId.Create("broker-adapter-inbound-event"),
            CorrelationId.Create("broker-adapter-inbound-correlation"),
            causationEventId: null,
            new DateTimeOffset(2026, 8, 8, 12, 0, 0, TimeSpan.Zero),
            new WorkflowEventRoute.Direct(InstanceId.Parse("0198e442-b700-7000-8000-000000000001")));
        var ingress = new RecordingIngress(
            new WorkflowEventAcceptanceResult.Accepted(),
            new WorkflowEventAcceptanceResult.Duplicate(),
            new WorkflowEventAcceptanceResult.Rejected(
                new WorkflowEventAcceptanceRejection.DirectInstanceTerminal()));
        static ValueTask<BrokerPublishOutcome> PublishAsync(
            WorkflowOutboundEvent _,
            CancellationToken __) =>
            ValueTask.FromResult<BrokerPublishOutcome>(new BrokerPublishOutcome.Published());
        var massTransit = new MassTransitStyleWorkflowEventAdapter(ingress, PublishAsync);
        var rebus = new RebusStyleWorkflowEventAdapter(ingress, PublishAsync);
        var snsSqs = new SnsSqsStyleWorkflowEventAdapter(ingress, PublishAsync);

        (await massTransit.ConsumeAsync(inbound, TestContext.Current.CancellationToken))
            .Should().Be(BrokerReceiveDisposition.Acknowledge);
        (await rebus.HandleAsync(inbound, TestContext.Current.CancellationToken))
            .Should().Be(BrokerReceiveDisposition.Acknowledge);
        (await snsSqs.ReceiveAsync(inbound, TestContext.Current.CancellationToken))
            .Should().Be(BrokerReceiveDisposition.DeadLetter);
    }

    [Fact]
    public async Task BrokerAdapterExamples_MapApplicationPublishOutcomesWithoutProviderTypes()
    {
        var contract = WorkflowEventContract<DispatchPayload>.Create(
            EventName.Create("invoice-issued"),
            new EventContractVersion(4));
        var outcomes = new Queue<BrokerPublishOutcome>(
        [
            new BrokerPublishOutcome.Published(),
            new BrokerPublishOutcome.Retryable("broker-unavailable", "try again"),
            new BrokerPublishOutcome.Permanent("destination-rejected", "topic disabled")
        ]);
        var application = new MassTransitStyleWorkflowEventAdapter(
            new RecordingIngress(),
            (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(outcomes.Dequeue());
            });
        var adapter = new WorkflowEventMessageDispatcher(application);
        var record = Outbox(contract, new DispatchPayload("invoice-19"));

        (await adapter.DispatchAsync(record, TestContext.Current.CancellationToken))
            .Should().Be(DispatchResult.Success);
        (await adapter.DispatchAsync(record, TestContext.Current.CancellationToken))
            .Should().Be(DispatchResult.RetryableFailure);
        (await adapter.DispatchAsync(record, TestContext.Current.CancellationToken))
            .Should().Be(DispatchResult.PermanentFailure);
    }

    [Fact]
    public void BrokerAdapterExamples_FailureCodesRemainValidatedWhenCopied()
    {
        var retryable = new BrokerPublishOutcome.Retryable("broker-unavailable") with
        {
            Code = "broker-overloaded",
            Detail = "retry later"
        };
        var permanent = new BrokerPublishOutcome.Permanent("destination-rejected") with
        {
            Code = "destination-disabled",
            Detail = "operator action required"
        };

        retryable.Code.Should().Be("broker-overloaded");
        retryable.Detail.Should().Be("retry later");
        permanent.Code.Should().Be("destination-disabled");
        permanent.Detail.Should().Be("operator action required");
        Action copyBlankRetryable = () => _ = retryable with { Code = " " };
        Action copyBlankPermanent = () => _ = permanent with { Code = "" };
        copyBlankRetryable.Should().Throw<ArgumentException>();
        copyBlankPermanent.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Adapter_ExposesOnlyTheCompleteApplicationEventAndMapsClosedResults()
    {
        var contract = WorkflowEventContract<DispatchPayload>.Create(
            EventName.Create("invoice-issued"),
            new EventContractVersion(4));
        var application = new RecordingDispatcher(
            new WorkflowEventDispatchResult.Succeeded(),
            new WorkflowEventDispatchResult.RetryableFailure(
                WorkflowEventDispatchFailure.Create("broker-unavailable", "try again")),
            new WorkflowEventDispatchResult.PermanentFailure(
                WorkflowEventDispatchFailure.Create("route-rejected")));
        var adapter = new WorkflowEventMessageDispatcher(application);
        var record = Outbox(contract, new DispatchPayload("invoice-19"));

        (await adapter.DispatchAsync(record, TestContext.Current.CancellationToken))
            .Should().Be(DispatchResult.Success);
        (await adapter.DispatchAsync(record, TestContext.Current.CancellationToken))
            .Should().Be(DispatchResult.RetryableFailure);
        (await adapter.DispatchAsync(record, TestContext.Current.CancellationToken))
            .Should().Be(DispatchResult.PermanentFailure);

        application.Events.Should().HaveCount(3);
        var outbound = application.Events[0];
        outbound.EventContract.Should().BeEquivalentTo(contract);
        outbound.EventId.Should().Be(EventId.Create("outbound-event-19"));
        outbound.CorrelationId.Should().Be(CorrelationId.Create("invoice-correlation-19"));
        outbound.CausationEventId.Should().Be(EventId.Create("inbound-event-18"));
        outbound.OccurredAt.Should().Be(new DateTimeOffset(2026, 8, 7, 18, 0, 0, TimeSpan.Zero));
        outbound.OriginInstanceId.Should().Be(InstanceId.Parse("0198df32-5ae4-7000-8000-000000000019"));
        outbound.OriginDefinitionId.Should().Be(DefinitionId.Parse("0198df32-5ae4-7000-8000-000000000020"));
        outbound.OriginDefinitionVersion.Should().Be(new DefinitionVersion(6));
        outbound.GetPayload(contract).Should().Be(new DispatchPayload("invoice-19"));
        var wrongContract = WorkflowEventContract<WrongDispatchPayload>.Create(
            contract.EventName,
            contract.Version);
        Action materializeWrongType = () => outbound.GetPayload(wrongContract);
        materializeWrongType.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Adapter_RejectsInternalContinuationBeforeInvokingTheApplication()
    {
        var application = new RecordingDispatcher(new WorkflowEventDispatchResult.Succeeded());
        var adapter = new WorkflowEventMessageDispatcher(application);

        Func<Task> act = () => adapter.DispatchAsync(
            new OutboxWrite(OutboxRecordId.New(), OutboxKinds.Continue, []),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Only workflow-event*");
        application.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Adapter_ClassifiesPayloadSchemaMismatchAsPermanentBeforeApplicationDispatch()
    {
        var contract = WorkflowEventContract<DispatchPayload>.Create(
            EventName.Create("invoice-issued"),
            new EventContractVersion(4));
        var application = new RecordingDispatcher(new WorkflowEventDispatchResult.Succeeded());
        var adapter = new WorkflowEventMessageDispatcher(application);
        var record = Outbox(
            contract,
            new DispatchPayload("invoice-19"),
            payloadSchemaIdentity: "wrong-schema");

        var result = await adapter.DispatchAsync(record, TestContext.Current.CancellationToken);

        result.Should().Be(DispatchResult.PermanentFailure);
        application.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task PublicOutboxPump_PoisonsStructurallyInvalidRecordsWithoutApplicationDispatch()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        await using var provider = services.BuildServiceProvider();
        var eventStore = provider.GetRequiredService<IWorkflowEventStore>();
        var outboxStore = provider.GetRequiredService<IWorkflowOutboxStore>();
        var contract = WorkflowEventContract<DispatchPayload>.Create(
            EventName.Create("invoice-issued"),
            new EventContractVersion(4));
        var wrongSchema = Outbox(
            contract,
            new DispatchPayload("invoice-19"),
            payloadSchemaIdentity: "wrong-schema");
        var missingType = Outbox(
            contract,
            new DispatchPayload("invoice-20"),
            payloadSchemaIdentity: "Missing.Payload, Missing.Assembly",
            payloadTypeName: "Missing.Payload, Missing.Assembly");
        await eventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString())),
                ExpectedVersion = StreamVersion.Empty,
                OutboxRecords = [wrongSchema, missingType]
            },
            TestContext.Current.CancellationToken);
        var application = new RecordingDispatcher();
        var pump = new WorkflowEventOutboxPump(
            outboxStore,
            application,
            observer: null,
            TimeProvider.System);

        var count = await pump.PumpOnceAsync(
            new OutboxClaimRequest(2, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        var wrongSchemaSnapshot = await outboxStore.GetDispatchSnapshotAsync(
            wrongSchema.OutboxRecordId,
            TestContext.Current.CancellationToken);
        var missingTypeSnapshot = await outboxStore.GetDispatchSnapshotAsync(
            missingType.OutboxRecordId,
            TestContext.Current.CancellationToken);

        count.Should().Be(2);
        application.Events.Should().BeEmpty();
        wrongSchemaSnapshot.Value.State.Should().Be(OutboxRecordState.Poisoned);
        wrongSchemaSnapshot.Value.PoisonCode.Should().Be(MaterializationFailureCode);
        missingTypeSnapshot.Value.State.Should().Be(OutboxRecordState.Poisoned);
        missingTypeSnapshot.Value.PoisonCode.Should().Be(MaterializationFailureCode);
    }

    [Fact]
    public async Task PublicOutboxPump_RetryRedispatchesTheSameOutboundEventIdentity()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        await using var provider = services.BuildServiceProvider();
        var eventStore = provider.GetRequiredService<IWorkflowEventStore>();
        var outboxStore = provider.GetRequiredService<IWorkflowOutboxStore>();
        var contract = WorkflowEventContract<DispatchPayload>.Create(
            EventName.Create("invoice-issued"),
            new EventContractVersion(4));
        var record = Outbox(contract, new DispatchPayload("invoice-19"));
        var committed = await eventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString())),
                ExpectedVersion = StreamVersion.Empty,
                OutboxRecords = [record]
            },
            TestContext.Current.CancellationToken);
        committed.IsSuccess.Should().BeTrue();
        var application = new RecordingDispatcher(
            new WorkflowEventDispatchResult.RetryableFailure(
                WorkflowEventDispatchFailure.Create("broker-unavailable")),
            new WorkflowEventDispatchResult.Succeeded());
        var pump = new WorkflowEventOutboxPump(
            outboxStore,
            application,
            observer: null,
            TimeProvider.System);
        var claim = new OutboxClaimRequest(1, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        await pump.PumpOnceAsync(claim, TestContext.Current.CancellationToken);
        (await outboxStore.GetStateAsync(record.OutboxRecordId, TestContext.Current.CancellationToken))
            .Value.Should().Be(OutboxRecordState.Retryable);
        await pump.PumpOnceAsync(claim with { ClaimedAt = claim.ClaimedAt.AddSeconds(1) }, TestContext.Current.CancellationToken);

        application.Events.Select(outbound => outbound.EventId.Value)
            .Should().Equal("outbound-event-19", "outbound-event-19");
        (await outboxStore.GetStateAsync(record.OutboxRecordId, TestContext.Current.CancellationToken))
            .Value.Should().Be(OutboxRecordState.Dispatched);
    }

    [Fact]
    public async Task PublicOutboxPump_PermanentFailurePersistsImmutableFailureCodeAndDetail()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        await using var provider = services.BuildServiceProvider();
        var eventStore = provider.GetRequiredService<IWorkflowEventStore>();
        var outboxStore = provider.GetRequiredService<IWorkflowOutboxStore>();
        var contract = WorkflowEventContract<DispatchPayload>.Create(
            EventName.Create("invoice-issued"),
            new EventContractVersion(4));
        var record = Outbox(contract, new DispatchPayload("invoice-19"));
        await eventStore.AppendAsync(
            new ProviderCommitBatch
            {
                StreamId = new WorkflowStreamId(InstanceId.Parse(Guid.CreateVersion7().ToString())),
                ExpectedVersion = StreamVersion.Empty,
                OutboxRecords = [record]
            },
            TestContext.Current.CancellationToken);
        var application = new RecordingDispatcher(
            new WorkflowEventDispatchResult.PermanentFailure(
                WorkflowEventDispatchFailure.Create("destination-rejected", "topic is disabled")));
        var pump = new WorkflowEventOutboxPump(
            outboxStore,
            application,
            observer: null,
            TimeProvider.System);
        var claim = new OutboxClaimRequest(1, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));

        await pump.PumpOnceAsync(claim, TestContext.Current.CancellationToken);
        await outboxStore.MarkAsync(
            record.OutboxRecordId,
            OutboxRecordState.Retryable,
            TestContext.Current.CancellationToken);
        var snapshot = await outboxStore.GetDispatchSnapshotAsync(
            record.OutboxRecordId,
            TestContext.Current.CancellationToken);

        snapshot.Value.State.Should().Be(OutboxRecordState.Poisoned);
        snapshot.Value.PoisonCode.Should().Be("destination-rejected");
        snapshot.Value.PoisonDetail.Should().Be("topic is disabled");
    }

    [Fact]
    public void PublishDefinition_RequiresTheApplicationDispatcherBeforeRegistryMutation()
    {
        var definition = PublishDefinition();
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(HostOptions()).AddWorkflow(definition);
        using var provider = services.BuildServiceProvider();

        Action resolve = () => provider.GetRequiredService<IWorkflowDefinitionRegistry>();

        resolve.Should().Throw<WorkflowDefinitionHostCompatibilityException>()
            .Which.Failure.Should()
            .BeOfType<DefinitionHostCompatibilityFailure.MissingWorkflowEventDispatcher>();
    }

    [Fact]
    public void PublishDefinition_RegistersWhenTheApplicationDispatcherIsPresent()
    {
        var definition = PublishDefinition();
        var services = new ServiceCollection();
        services.AddSingleton<IWorkflowEventDispatcher>(
            new RecordingDispatcher(new WorkflowEventDispatchResult.Succeeded()));
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(HostOptions()).AddWorkflow(definition);
        using var provider = services.BuildServiceProvider();

        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();

        registry.GetRequiredHandle(definition.Reference).DefinitionId.Should().Be(definition.DefinitionId);
        provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .Should().Contain(service => service is OrcaCoreWorkflowEventOutboxPumpHostedService);
    }

    [Fact]
    public void DispatcherRegistrationOrder_DoesNotDisablePublishProgression()
    {
        var definition = PublishDefinition();
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(HostOptions()).AddWorkflow(definition);
        services.AddSingleton<IWorkflowEventDispatcher>(
            new RecordingDispatcher(new WorkflowEventDispatchResult.Succeeded()));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .GetRequiredHandle(definition.Reference).Should().NotBeNull();
        provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .Should().Contain(service => service is OrcaCoreWorkflowEventOutboxPumpHostedService);
    }

    [Fact]
    public void DispatchFailure_RequiresANonblankStableCode()
    {
        Action create = () => WorkflowEventDispatchFailure.Create(" ");

        create.Should().Throw<ArgumentException>();
    }

    private static OutboxWrite Outbox(
        WorkflowEventContract<DispatchPayload> contract,
        DispatchPayload payload,
        string? payloadSchemaIdentity = null,
        string? payloadTypeName = null) =>
        new(
            OutboxRecordId.New(),
            OutboxKinds.WorkflowEvent,
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
            {
                EventName = contract.EventName.Value,
                EventContractVersion = contract.Version.Value,
                PayloadTypeName = payloadTypeName ?? typeof(DispatchPayload).AssemblyQualifiedName,
                PayloadSchemaIdentity = payloadSchemaIdentity ?? typeof(DispatchPayload).AssemblyQualifiedName,
                EventId = "outbound-event-19",
                CorrelationId = "invoice-correlation-19",
                CausationEventId = "inbound-event-18",
                OccurredAt = new DateTimeOffset(2026, 8, 7, 18, 0, 0, TimeSpan.Zero),
                OriginInstanceId = "0198df32-5ae4-7000-8000-000000000019",
                OriginDefinitionId = "0198df32-5ae4-7000-8000-000000000020",
                OriginDefinitionVersion = 6,
                Payload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload)
            }));

    private static DurableWorkflowDefinition<PublishInput> PublishDefinition()
    {
        var contract = WorkflowEventContract.Create(
            EventName.Create("published"),
            EventContractVersion.Initial);
        return Workflow.Durable<PublishState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<PublishInput>(input => new PublishState(input.Value))
            .Publish(contract, state => CorrelationId.Create(state.Value.Value))
            .End()
            .Build();
    }

    private static DurableEngineHostOptions HostOptions() => new()
    {
        StructuredExecution = new StructuredExecutionHostOptions
        {
            MaxConcurrentExecutionPathsPerInstance = 2,
            StepThrottles = []
        },
        ResourcePools = new DurableResourcePoolOptions
        {
            PartitionId = ResourceGovernancePartitionId.Create("publish-dispatch-tests"),
            Pools = []
        }
    };

    private sealed class RecordingDispatcher(params WorkflowEventDispatchResult[] results)
        : IWorkflowEventDispatcher
    {
        private readonly Queue<WorkflowEventDispatchResult> results = new(results);

        internal List<WorkflowOutboundEvent> Events { get; } = [];

        public ValueTask<WorkflowEventDispatchResult> DispatchAsync(
            WorkflowOutboundEvent outboundEvent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Events.Add(outboundEvent);

            return ValueTask.FromResult(results.Dequeue());
        }
    }

    private sealed class RecordingIngress(params WorkflowEventAcceptanceResult[] results)
        : IWorkflowEventIngress
    {
        private readonly Queue<WorkflowEventAcceptanceResult> results = new(results);

        public ValueTask<WorkflowEventAcceptanceResult> AcceptAsync(
            WorkflowInboundEvent inboundEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(inboundEvent);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(results.Dequeue());
        }

        public ValueTask<WorkflowEventAcceptanceResult> AcceptAsync<TPayload>(
            WorkflowInboundEvent<TPayload> inboundEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(inboundEvent);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(results.Dequeue());
        }
    }

    private sealed record DispatchPayload(string Value);
    private sealed record WrongDispatchPayload(string Value);
    private sealed record PublishInput(string Value);
    private sealed record PublishState(string Value);
}
