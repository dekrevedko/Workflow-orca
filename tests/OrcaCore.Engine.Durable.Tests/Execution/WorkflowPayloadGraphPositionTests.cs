using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Execution;

/// <summary>
/// Executes every graph position the fixed codec must reject as an independent case, so one early
/// throw can never stand in as proof for a traversal branch that was never reached, and proves each
/// durable rejection happens before any provider call.
/// </summary>
public sealed class WorkflowPayloadGraphPositionTests
{
    [Fact]
    public void FixedCodec_RejectsUnsupportedDictionaryKeyContract()
    {
        var payload = new DictionaryKeyPayload(new Dictionary<BasePayload, string>
        {
            [new DerivedPayload("base", "derived")] = "item"
        });

        var act = () => CreateEvent(payload);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*serialization customization*not supported*");
    }

    [Fact]
    public async Task RootPolymorphism_IsRejectedBeforeProviderMutation()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var handle = CreateHandle<BasePayload>(store, definitionId);
        var key = StartIdempotencyKey.Create("graph-root");

        Func<Task> act = async () =>
        {
            _ = await handle.StartOrGetAsync(
                new DerivedPayload("base", "derived"),
                key,
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*polymorphic*not supported*");
        await AssertNoStartMutationAsync(store, key, "the root graph must be rejected atomically");
    }

    [Fact]
    public async Task NestedPropertyPolymorphism_IsRejectedBeforeProviderMutation()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var handle = CreateHandle<NestedPayload>(store, definitionId);
        var key = StartIdempotencyKey.Create("graph-nested");

        Func<Task> act = async () =>
        {
            _ = await handle.StartOrGetAsync(
                new NestedPayload(new DerivedPayload("base", "derived")),
                key,
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*polymorphic*not supported*");
        await AssertNoStartMutationAsync(store, key, "the nested graph must be rejected atomically");
    }

    [Fact]
    public async Task CollectionElementPolymorphism_IsRejectedBeforeProviderMutation()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var handle = CreateHandle<CollectionPayload>(store, definitionId);
        var key = StartIdempotencyKey.Create("graph-collection");

        Func<Task> act = async () =>
        {
            _ = await handle.StartOrGetAsync(
                new CollectionPayload([new DerivedPayload("base", "derived")]),
                key,
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*polymorphic*not supported*");
        await AssertNoStartMutationAsync(store, key, "the collection graph must be rejected atomically");
    }

    [Fact]
    public async Task DictionaryValuePolymorphism_IsRejectedBeforeProviderMutation()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var handle = CreateHandle<DictionaryValuePayload>(store, definitionId);
        var key = StartIdempotencyKey.Create("graph-dictionary-value");

        Func<Task> act = async () =>
        {
            _ = await handle.StartOrGetAsync(
                new DictionaryValuePayload(new Dictionary<string, BasePayload>
                {
                    ["item"] = new DerivedPayload("base", "derived")
                }),
                key,
                CancellationToken.None);
        };

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*polymorphic*not supported*");
        await AssertNoStartMutationAsync(store, key, "the dictionary value must be rejected atomically");
    }

    [Fact]
    public async Task DictionaryKeyContract_IsRejectedBeforeProviderMutation()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var key = StartIdempotencyKey.Create("graph-dictionary-key");

        Action act = () => CreateHandle<DictionaryKeyPayload>(store, definitionId);

        act.Should().Throw<WorkflowDefinitionException>().WithMessage("*SFE-TYPE-002*");
        await AssertNoStartMutationAsync(store, key, "the dictionary key must be rejected atomically");
    }

    [Fact]
    public async Task CyclicGraph_IsRejectedBeforeProviderMutation()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var handle = CreateHandle<CyclicPayload>(store, definitionId);
        var key = StartIdempotencyKey.Create("graph-cyclic");
        var cyclic = new CyclicPayload();
        cyclic.Self = cyclic;

        Func<Task> act = async () =>
        {
            _ = await handle.StartOrGetAsync(cyclic, key, CancellationToken.None);
        };

        await act.Should().ThrowAsync<JsonException>();
        await AssertNoStartMutationAsync(store, key, "the cyclic graph must be rejected atomically");
    }

    private static DurableDefinitionHandle<TPayload> CreateHandle<TPayload>(
        InMemoryWorkflowProvider store,
        DefinitionId definitionId)
    {
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(store, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: store);
        var registry = new DurableWorkflowDefinitionRegistry(
            runtime,
            store,
            store,
            processor,
            notifications,
            TimeProvider.System);
        return registry.Register(
            Workflow
                .Durable<TPayload>(definitionId, DefinitionVersion.Initial)
                .Init<TPayload>(payload => payload)
                .End()
                .Build()).GetHandleOrThrow();
    }

    private static WorkflowEvent<TPayload> CreateEvent<TPayload>(TPayload payload)
    {
        return WorkflowEvent<TPayload>.Create(
            EventId.Create("graph-event"),
            EventName.Create("graph-event"),
            CorrelationId.Create("graph-correlation"),
            payload,
            DateTimeOffset.Parse("2026-07-30T12:00:00Z"));
    }

    private static async Task AssertNoStartMutationAsync(
        InMemoryWorkflowProvider store,
        StartIdempotencyKey key,
        string because)
    {
        var persisted = await store.GetStartedAsync(key.Value, CancellationToken.None);
        persisted.HasValue.Should().BeFalse(because);
    }

    public record BasePayload(string Value);

    public sealed record DerivedPayload(string Value, string Detail) : BasePayload(Value);

    public sealed record NestedPayload(BasePayload Value);

    public sealed record CollectionPayload(List<BasePayload> Items);

    public sealed record DictionaryValuePayload(Dictionary<string, BasePayload> Map);

    public sealed record DictionaryKeyPayload(Dictionary<BasePayload, string> Map);

    public sealed class CyclicPayload
    {
        public CyclicPayload? Self { get; set; }
    }

}
