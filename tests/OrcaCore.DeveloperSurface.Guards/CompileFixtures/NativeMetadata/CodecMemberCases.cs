using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;

internal static class CodecMemberCases
{
    internal static async Task IncludedMembers()
    {
        var bad = new BoundaryDerived { Number = 7, Extra = 99 };
        Reject<NotSupportedException>(() => Materialize(new IncludedSubtypeField { Value = bad }), "polymorphic");
        IncludedSubtypeProperty.Reads = 0;
        Reject<NotSupportedException>(() => Materialize(new IncludedSubtypeProperty { Value = bad }), "polymorphic");
        Check(IncludedSubtypeProperty.Reads == 1, "rejected included getter is read exactly once");
        Reject<NotSupportedException>(() => Materialize(new OrdinarySubtypeProperty { Value = bad }), "polymorphic");
        Reject<NotSupportedException>(() => Materialize(new IncludedCollectionField { Value = new BoundaryList() }), "allowlist");
        IncludedCollectionProperty.Reads = 0;
        Reject<NotSupportedException>(() => Materialize(new IncludedCollectionProperty { Value = new BoundaryList() }), "allowlist");
        Check(IncludedCollectionProperty.Reads == 1, "rejected collection getter is read exactly once");
        Reject<NotSupportedException>(() => Materialize(new OrdinaryCollectionProperty { Value = new BoundaryList() }), "allowlist");
        Reject<NotSupportedException>(() => Materialize(new IncludedObjectList { Value = new List<BoundaryBase> { bad } }), "polymorphic");
        Reject<NotSupportedException>(() => Materialize(new IncludedObjectMap { Value = new Dictionary<string, BoundaryBase> { ["bad"] = bad } }), "polymorphic");
        ChangingSubtypeProperty.Reads = 0;
        Reject<NotSupportedException>(() => Materialize(new ChangingSubtypeProperty()), "polymorphic");
        Check(ChangingSubtypeProperty.Reads == 2, "the serializer validates its actual public getter value without another read");

        var field = new IncludedSubtypeField { Value = new BoundaryBase { Number = 7 } };
        var fieldCopy = Materialize(field);
        Check(fieldCopy.Value.Number == 7 && !ReferenceEquals(fieldCopy.Value, field.Value), "included fields remain detached");
        var property = new IncludedSubtypeProperty { Value = new BoundaryBase { Number = 7 } };
        IncludedSubtypeProperty.Reads = 0;
        var propertyCopy = Materialize(property);
        Check(IncludedSubtypeProperty.Reads == 1 && propertyCopy.Peek().Number == 7 &&
            !ReferenceEquals(property.Peek(), propertyCopy.Peek()), "valid included getter runs once and remains detached");
        OrdinaryCountingProperty.Reads = 0;
        _ = Materialize(new OrdinaryCountingProperty());
        Check(OrdinaryCountingProperty.Reads == 2, "ordinary public getter retains its legacy validation and serialization reads");
        OrdinaryCountingProperty.Reads = 0;
        _ = Materialize(new NestedIncludedGetter());
        Check(OrdinaryCountingProperty.Reads == 1, "an included field must not add a prepass through its child's getter");
        var ignored = Materialize(new IgnoredSubtypeField { Hidden = bad, Number = 7 });
        Check(ignored.Number == 7 && ignored.Hidden is null, "ignored members remain omitted without reading their runtime value");
        var approved = Materialize(new ApprovedSubtypeField { Value = new ApprovedBoundaryDerived { Number = 7, Extra = 99 } });
        Check(approved.Value is ApprovedBoundaryDerived { Number: 7, Extra: 99 }, "explicitly approved included polymorphism retains every value");
        var list = new IncludedCollectionProperty { Value = new List<int> { 7, 8 } };
        IncludedCollectionProperty.Reads = 0;
        var listCopy = Materialize(list);
        Check(IncludedCollectionProperty.Reads == 1 && listCopy.Peek().SequenceEqual([7, 8]) &&
            !ReferenceEquals(list.Peek(), listCopy.Peek()), "allowlisted included collections remain detached with one getter read");

        var services = new ServiceCollection().AddTransient<MemberWaitStep>();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] }, TransientPools = []
        });
        await using var provider = services.BuildServiceProvider();
        try
        {
            var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
            var waiting = Workflow.Ephemeral<int>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<int>(value => value).Then<MemberWaitStep>().End(value => value.Value).Build();
            var retained = (await registry.Register(waiting).GetHandleOrThrow()
                .StartOrGetAsync(7, StartIdempotencyKey.Create("retained-member"))).GetHandleOrThrow();
            var before = await retained.GetSnapshotAsync();
            var calls = 0;
            var accepting = Workflow.Ephemeral<int>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<IncludedSubtypeField>(value => { calls++; return value.Value.Number; }).End(value => value.Value).Build();
            var handle = registry.Register(accepting).GetHandleOrThrow();
            var key = StartIdempotencyKey.Create("rejected-member");
            try
            {
                await handle.StartOrGetAsync(new IncludedSubtypeField { Value = bad }, key);
                throw new InvalidOperationException("ASSERTION: invalid input was accepted");
            }
            catch (NotSupportedException error) when (error.Message.Contains("polymorphic", StringComparison.Ordinal)) { }
            Check(calls == 0, "invalid included value rejects before Init or key binding");
            var accepted = (await handle.StartOrGetAsync(field, key)).GetHandleOrThrow();
            Check(await accepted.WaitForOutputAsync() == 7 && calls == 1, "rejected input leaves its start key reusable");
            var bodies = 0;
            var dynamicState = Workflow.Ephemeral<IncludedSubtypeField>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<int>(number => new IncludedSubtypeField { Value = number == 1 ? bad : new BoundaryBase { Number = 7 } })
                .Then(_ => { bodies++; return ValueTask.CompletedTask; }).End(value => value.Value.Value.Number).Build();
            var stateHandle = registry.Register(dynamicState).GetHandleOrThrow();
            var stateKey = StartIdempotencyKey.Create("rejected-member-state");
            try
            {
                await stateHandle.StartOrGetAsync(1, stateKey);
                throw new InvalidOperationException("ASSERTION: invalid dynamic state was accepted");
            }
            catch (NotSupportedException error) when (error.Message.Contains("polymorphic", StringComparison.Ordinal)) { }
            Check(bodies == 0, "an invalid Init result rejects before the following body or acceptance");
            var goodState = (await stateHandle.StartOrGetAsync(2, stateKey)).GetHandleOrThrow();
            Check(await goodState.WaitForOutputAsync() == 7 && bodies == 1, "aborted dynamic state leaves the key reusable");
            var after = await retained.GetSnapshotAsync();
            Check(before.Status == WorkflowInstanceStatus.Waiting && after.Status == WorkflowInstanceStatus.Waiting &&
                before.ActiveWaits.Single().WaitId.Equals(after.ActiveWaits.Single().WaitId) &&
                await retained.GetStateAsync<int>() == 7, "rejection leaves the accepted instance and its exact wait intact");
        }
        finally
        {
            Check(await provider.GetRequiredService<IEphemeralWorkflowHostLifetime>().StopAsync(Timeout.InfiniteTimeSpan)
                is EphemeralHostShutdownResult.Completed, "member-boundary tests join their physical work");
        }
    }

    internal static async Task WriteOnly()
    {
        var map = new WriteOnlyMap { Value = 7 };
        var converter = new WriteOnlyConverter { Value = 7 };
        Check(Materialize(map).Value == 7 && Materialize(converter).Value == 7,
            "write-only members do not narrow legacy serialized-value admission");
        await CodecCases.CheckFingerprint(map, new WriteOnlyMap { Value = 8 }, "{\"Value\":7}");
        await CodecCases.CheckFingerprint(converter, new WriteOnlyConverter { Value = 8 }, "{\"Value\":7}");
        var mapDefinition = Workflow.Ephemeral<SerializedMap>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => new SerializedMap()).End().TryBuild();
        var converterDefinition = Workflow.Ephemeral<SerializedConverter>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(_ => new SerializedConverter()).End().TryBuild();
        Check(!mapDefinition.IsValid && mapDefinition.Diagnostics.Any(value => value.Code == "SFE-TYPE-002") &&
            !converterDefinition.IsValid && converterDefinition.Diagnostics.Any(value => value.Code == "SFE-TYPE-002"),
            "serialized forbidden maps and converters still reject rather than becoming an admission exception");
    }

    private static T Materialize<T>(T value) => WorkflowInboundEvent<T>.Create(
        WorkflowEventContract<T>.Create(EventName.Create("member"), EventContractVersion.Initial),
        EventId.Create("member"), CorrelationId.Create("member"), null, DateTimeOffset.UtcNow,
        new WorkflowEventRoute.Correlation(DefinitionId.New()), value).Payload;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ASSERTION: " + message);
    }

    private static void Reject<TException>(Action action, string detail) where TException : Exception
    {
        try { action(); }
        catch (TException error)
        {
            Check(error.Message.Contains(detail, StringComparison.Ordinal), "rejection retains its diagnostic: " + error.Message);
            return;
        }
        throw new InvalidOperationException("ASSERTION: included member must reject " + detail);
    }
}

public class BoundaryBase { public int Number { get; set; } }
public sealed class BoundaryDerived : BoundaryBase { public int Extra { get; set; } }
public sealed class IncludedSubtypeField { [JsonInclude] public BoundaryBase Value = new(); }
public sealed class IncludedSubtypeProperty
{
    private BoundaryBase value = new();
    public static int Reads;
    [JsonInclude] internal BoundaryBase Value { get { Reads++; return value; } set => this.value = value; }
    public BoundaryBase Peek() => value;
}
public sealed class OrdinarySubtypeProperty { public BoundaryBase Value { get; set; } = new(); }
public sealed class IncludedCollectionField { [JsonInclude] public IReadOnlyList<int> Value = []; }
public sealed class IncludedCollectionProperty
{
    private IReadOnlyList<int> value = [];
    public static int Reads;
    [JsonInclude] internal IReadOnlyList<int> Value { get { Reads++; return value; } set => this.value = value; }
    public IReadOnlyList<int> Peek() => value;
}
public sealed class OrdinaryCollectionProperty { public IReadOnlyList<int> Value { get; set; } = []; }
public sealed class IncludedObjectList { [JsonInclude] public IReadOnlyList<BoundaryBase> Value = []; }
public sealed class IncludedObjectMap { [JsonInclude] public IReadOnlyDictionary<string, BoundaryBase> Value = new Dictionary<string, BoundaryBase>(); }
public sealed class ChangingSubtypeProperty
{
    public static int Reads;
    public BoundaryBase Value => ++Reads == 1 ? new BoundaryBase { Number = 7 } : new BoundaryDerived { Number = 7, Extra = 99 };
}
public sealed class BoundaryList : IReadOnlyList<int>
{
    public int Count => 2;
    public int this[int index] => index + 7;
    public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)new[] { 7, 8 }).GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
public sealed class OrdinaryCountingProperty
{
    private int value = 7;
    public static int Reads;
    public int Value { get { Reads++; return value; } set => this.value = value; }
}
public sealed class NestedIncludedGetter { [JsonInclude] public OrdinaryCountingProperty Value = new(); }
public sealed class IgnoredSubtypeField
{
    public int Number { get; set; }
    [JsonInclude, JsonIgnore] public BoundaryBase? Hidden;
}
[JsonPolymorphic]
[JsonDerivedType(typeof(ApprovedBoundaryDerived), "derived")]
public class ApprovedBoundaryBase { public int Number { get; set; } }
public sealed class ApprovedBoundaryDerived : ApprovedBoundaryBase { public int Extra { get; set; } }
public sealed class ApprovedSubtypeField { [JsonInclude] public ApprovedBoundaryBase Value = new(); }
public sealed class MemberWaitStep : IStep<int>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken) =>
        ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent<int>(
            WorkflowEventContract<int>.Create(EventName.Create("member-wait"), EventContractVersion.Initial),
            CorrelationId.Create("member-wait")));
}

public sealed class WriteOnlyMap
{
    public int Value { get; set; }
    public Dictionary<int, int> Hidden { set { } }
}

public sealed class WriteOnlyConverter
{
    public int Value { get; set; }
    [JsonConverter(typeof(MemberIntConverter))] public int Hidden { set { } }
}

public sealed class SerializedMap
{
    public Dictionary<int, int> Value { get; set; } = [];
}

public sealed class SerializedConverter
{
    [JsonConverter(typeof(MemberIntConverter))] public int Value { get; set; }
}

public sealed class MemberIntConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetInt32();
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(WriteOnlyMap))]
[JsonSerializable(typeof(WriteOnlyConverter))]
[JsonSerializable(typeof(SerializedMap))]
[JsonSerializable(typeof(SerializedConverter))]
[JsonSerializable(typeof(Payload))]
[JsonSerializable(typeof(IncludedSubtypeField))]
[JsonSerializable(typeof(BoundaryDerived))]
[JsonSerializable(typeof(IncludedSubtypeProperty))]
[JsonSerializable(typeof(OrdinarySubtypeProperty))]
[JsonSerializable(typeof(IncludedCollectionField))]
[JsonSerializable(typeof(IncludedCollectionProperty))]
[JsonSerializable(typeof(OrdinaryCollectionProperty))]
[JsonSerializable(typeof(IncludedObjectList))]
[JsonSerializable(typeof(IncludedObjectMap))]
[JsonSerializable(typeof(ChangingSubtypeProperty))]
[JsonSerializable(typeof(OrdinaryCountingProperty))]
[JsonSerializable(typeof(NestedIncludedGetter))]
[JsonSerializable(typeof(IgnoredSubtypeField))]
[JsonSerializable(typeof(ApprovedSubtypeField))]
public partial class CodecMemberContext : JsonSerializerContext;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Default)]
[JsonSerializable(typeof(WriteOnlyMap))]
[JsonSerializable(typeof(WriteOnlyConverter))]
[JsonSerializable(typeof(SerializedMap))]
[JsonSerializable(typeof(SerializedConverter))]
[JsonSerializable(typeof(Payload))]
[JsonSerializable(typeof(IncludedSubtypeField))]
[JsonSerializable(typeof(BoundaryDerived))]
[JsonSerializable(typeof(IncludedSubtypeProperty))]
[JsonSerializable(typeof(OrdinarySubtypeProperty))]
[JsonSerializable(typeof(IncludedCollectionField))]
[JsonSerializable(typeof(IncludedCollectionProperty))]
[JsonSerializable(typeof(OrdinaryCollectionProperty))]
[JsonSerializable(typeof(IncludedObjectList))]
[JsonSerializable(typeof(IncludedObjectMap))]
[JsonSerializable(typeof(ChangingSubtypeProperty))]
[JsonSerializable(typeof(OrdinaryCountingProperty))]
[JsonSerializable(typeof(NestedIncludedGetter))]
[JsonSerializable(typeof(IgnoredSubtypeField))]
[JsonSerializable(typeof(ApprovedSubtypeField))]
public partial class CodecMemberDefaultContext : JsonSerializerContext;
