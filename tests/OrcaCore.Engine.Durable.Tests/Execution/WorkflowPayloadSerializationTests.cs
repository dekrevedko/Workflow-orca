using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

public sealed class WorkflowPayloadSerializationTests
{
    [Fact]
    public async Task PublicTypedEvent_UsesDeterministicDetachedDurableEncoding()
    {
        var store = new InMemoryWorkflowProvider();
        var eventName = EventName.Create("codec-event");
        var correlation = CorrelationId.Create("codec-correlation");
        var definition = WaitingDefinition(DefinitionId.New(), eventName);
        var facade = CreateFacade(store);
        var handle = facade.Registry.Register(definition).GetHandleOrThrow();
        var firstInstanceId = (await handle.StartOrGetAsync(
            new Input(correlation.Value),
            StartIdempotencyKey.Create("codec-first"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow().InstanceId;
        var source = new TestPayload("value", ["first", "second"]);
        var causationEventId = EventId.Create("codec-cause-id");
        var occurredAt = DateTimeOffset.Parse("2026-07-30T12:00:00-07:00");
        var workflowEvent = WorkflowInboundEvent<TestPayload>.Create(
            WorkflowEventContract<TestPayload>.Create(eventName, EventContractVersion.Initial),
            EventId.Create("codec-event-id"),
            correlation,
            causationEventId,
            occurredAt,
            new WorkflowEventRoute.Direct(firstInstanceId),
            source);
        source.Items[0] = "mutated";

        var accepted = await facade.Events.AcceptAsync(
            workflowEvent,
            TestContext.Current.CancellationToken);
        var first = await store.GetAsync(
            firstInstanceId,
            workflowEvent.EventId,
            TestContext.Current.CancellationToken);
        var global = await store.GetByEventIdAsync(
            workflowEvent.EventId,
            TestContext.Current.CancellationToken);
        accepted.Should().BeOfType<WorkflowEventAcceptanceResult.Accepted>();
        workflowEvent.Payload.Should().BeEquivalentTo(
            new TestPayload("value", ["first", "second"]));
        workflowEvent.Payload.Should().NotBeSameAs(source);
        workflowEvent.Payload.Items.Should().NotBeSameAs(source.Items);
        first.Value.Envelope.Should().NotBeNull();
        first.Value.Envelope!.EventId.Should().Be(workflowEvent.EventId);
        first.Value.Envelope.EventName.Should().Be(eventName.Value);
        first.Value.Envelope.EventContractVersion.Should().Be(EventContractVersion.Initial.Value);
        first.Value.Envelope.CorrelationId.Should().Be(correlation);
        first.Value.Envelope.CausationEventId.Should().Be(causationEventId);
        first.Value.Envelope.OccurredAt.Should().Be(occurredAt.ToUniversalTime());
        first.Value.Envelope.Route.Kind.Should().Be("direct");
        first.Value.Envelope.Route.InstanceId.Should().Be(firstInstanceId);
        first.Value.Envelope.PayloadContentType.Should().Be(JsonWorkflowPayloadSerializer.JsonContentType);
        first.Value.Envelope.Payload.Should().NotBeEmpty();
        first.Value.EnvelopeFingerprint.Should().NotBeNullOrWhiteSpace();
        global.Value.Should().Be(first.Value);
    }

    [Fact]
    public void FixedCodec_RoundTripsNullDeterministically()
    {
        var workflowEvent = CreateEvent<string?>(null);

        workflowEvent.Payload.Should().BeNull();
    }

    [Fact]
    public void FixedCodec_RejectsCyclicAndUnapprovedPolymorphicGraphs()
    {
        var cyclic = new CyclicPayload();
        cyclic.Self = cyclic;
        BasePayload polymorphic = new DerivedPayload("base", "derived");

        var cyclicWrite = () => CreateEvent(cyclic);
        var polymorphicWrite = () => CreateEvent<BasePayload>(polymorphic);

        cyclicWrite.Should().Throw<JsonException>();
        polymorphicWrite.Should().Throw<NotSupportedException>()
            .WithMessage("*polymorphic*not supported*");
    }

    [Fact]
    public void FixedCodec_RejectsUnapprovedPolymorphismInNestedProperty()
    {
        var payload = new NestedPayload(new DerivedPayload("base", "derived"));

        var act = () => CreateEvent(payload);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*polymorphic*not supported*");
    }

    [Fact]
    public void FixedCodec_RoundTripsPolymorphismDeclaredByStaticContract()
    {
        ApprovedBasePayload payload = new ApprovedDerivedPayload("base", "derived");

        var restored = CreateEvent<ApprovedBasePayload>(payload).Payload;

        restored.Should().Be(new ApprovedDerivedPayload("base", "derived"));
    }

    [Fact]
    public void FixedCodec_RejectsUnapprovedPolymorphismInCollectionElement()
    {
        var payload = new List<BasePayload>
        {
            new DerivedPayload("base", "derived")
        };

        var act = () => CreateEvent(payload);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*polymorphic*not supported*");
    }

    [Fact]
    public void FixedCodec_RejectsUnapprovedPolymorphismInDictionaryValue()
    {
        var payload = new Dictionary<string, BasePayload>
        {
            ["item"] = new DerivedPayload("base", "derived")
        };

        var act = () => CreateEvent(payload);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*polymorphic*not supported*");
    }

    [Fact]
    public void PublicTypedEvent_ExposesNoContentTypeReplacement()
    {
        typeof(WorkflowInboundEvent<string>)
            .GetProperties()
            .Should().NotContain(property => property.Name.Contains("ContentType", StringComparison.Ordinal));
        typeof(WorkflowInboundEvent<string>)
            .GetMethods()
            .Where(method => method.IsPublic && method.IsStatic)
            .Should().ContainSingle(method => method.Name == nameof(WorkflowInboundEvent<string>.Create));
    }

    [Fact]
    public async Task FixedCodec_RejectsPolymorphicGraphBeforeProviderMutation()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var facade = CreateFacade(store);
        var handle = facade.Registry.Register(
            Workflow.Durable<NestedPayload>(
                    definitionId,
                    DefinitionVersion.Initial)
                .Init<NestedPayload>(payload => payload)
                .End()
                .Build()).GetHandleOrThrow();
        var idempotencyKey = StartIdempotencyKey.Create("polymorphic-input");

        Func<Task> act = async () =>
        {
            _ = await handle.StartOrGetAsync(
                new NestedPayload(new DerivedPayload("base", "derived")),
                idempotencyKey,
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*polymorphic*not supported*");
        var persisted = await store.GetStartedAsync(idempotencyKey.Value, CancellationToken.None);
        persisted.HasValue.Should().BeFalse();
    }

    private static WorkflowInboundEvent<TPayload> CreateEvent<TPayload>(TPayload payload)
    {
        var eventName = EventName.Create("codec-event");
        return WorkflowInboundEvent<TPayload>.Create(
            WorkflowEventContract<TPayload>.Create(eventName, EventContractVersion.Initial),
            EventId.Create("codec-event"),
            CorrelationId.Create("codec-correlation"),
            causationEventId: null,
            DateTimeOffset.Parse("2026-07-30T12:00:00Z"),
            new WorkflowEventRoute.Direct(InstanceId.Parse(Guid.CreateVersion7().ToString())),
            payload);
    }

    private static DurableWorkflowDefinition<Input> WaitingDefinition(
        DefinitionId definitionId,
        EventName eventName)
    {
        return Workflow.Durable<WaitState>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new WaitState(input.Correlation))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), state => CorrelationId.Create(state.Value.Correlation))
            .End(WorkflowOutcomeName.Create("completed"))
            .Build();
    }

    private static FacadeServices CreateFacade(InMemoryWorkflowProvider store)
    {
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(store, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: store);
        return new FacadeServices(
            new DurableWorkflowDefinitionRegistry(
                runtime,
                store,
                store,
                processor,
                notifications,
                TimeProvider.System),
            new DurableWorkflowEventIngressCore(
                runtime,
                store,
                store,
                driveAfterAcceptance: false));
    }

    private sealed record TestPayload(string Value, List<string> Items);

    private sealed record Input(string Correlation);

    private sealed record WaitState(string Correlation);

    private sealed class CyclicPayload
    {
        public CyclicPayload? Self { get; set; }
    }

    private record BasePayload(string Value);

    private sealed record DerivedPayload(string Value, string Detail) : BasePayload(Value);

    private sealed record NestedPayload(BasePayload Value);

    [JsonDerivedType(typeof(ApprovedDerivedPayload), typeDiscriminator: "derived")]
    private abstract record ApprovedBasePayload(string Value);

    private sealed record ApprovedDerivedPayload(string Value, string Detail)
        : ApprovedBasePayload(Value);

    private sealed record FacadeServices(
        DurableWorkflowDefinitionRegistry Registry,
        DurableWorkflowEventIngressCore Events);
}
