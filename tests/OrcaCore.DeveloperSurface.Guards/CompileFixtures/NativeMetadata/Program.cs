using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using OrcaCore;
using OrcaCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

try
{
    switch (args.Single())
    {
        case "strong-metadata":
        case "strong-default":
        case "strong-legacy":
            Console.WriteLine("STRONG " + await StrongValueCases.Run(args[0]));
            break;
        case "product-converter-options-rejected":
            Reject<NotSupportedException>(() => WorkflowJsonMetadata.Register<ExplicitProductConverterContext>(), "ExplicitProductConverterContext", "Converters", "sealed=False");
            WorkflowJsonMetadata.Register<StrongMetadataContext>();
            Check(Materialize(EventName.Create("catalogue-value")).Value == "catalogue-value", "product attribution works after invalid explicit converter settings");
            break;
        case "public-factory-no-seal":
            StrongValueCases.FactoryBoundary();
            WorkflowJsonMetadata.Register<StrongMetadataContext>();
            Check(Materialize(EventName.Create("catalogue-value")).Value == "catalogue-value", "direct factory use does not configure or seal metadata");
            break;
        case "graph-members-generated":
        case "graph-members-legacy":
            if (args[0].EndsWith("generated", StringComparison.Ordinal)) WorkflowJsonMetadata.Register<AppContext>();
            MemberBoundary.Reads = 0;
            _ = Materialize(new MemberBoundary());
            Check(MemberBoundary.Reads == 1, "nonpublic JsonInclude getter runs only during serialization, never during the public-member graph check: " + MemberBoundary.Reads);
            break;
        case "foreach-array-missing":
            DispatchCases.MissingArray();
            break;
        case "closed-typed-ownership":
            await OwnershipCases.Run();
            break;
        case "closed-typed-dispatch":
            await DispatchCases.Run();
            break;
        case "registration-parity":
            WorkflowJsonMetadata.Register<AppContext>();
            var original = new Payload { Value = 7 };
            var detached = Materialize(original);
            Check(detached.Value == 7 && !ReferenceEquals(original, detached), "metadata preserves detached public event values");
            break;
        case "baked-enum-rejected":
            Check(EnumContext.Default.Options.Converters.Count == 0, "enum policy is baked, not an options converter");
            Check(JsonSerializer.Serialize(new EnumState { State = Mode.Ready }, EnumContext.Default.EnumState) == "{\"State\":\"Ready\"}", "framework reproduces the baked enum counterexample");
            Reject<NotSupportedException>(() => WorkflowJsonMetadata.Register<EnumContext>(), "EnumContext", "UseStringEnumConverter");
            WorkflowJsonMetadata.Register<AppContext>();
            Check(Materialize(new EnumState { State = Mode.Ready }).State == Mode.Ready, "valid retry uses original numeric enum contract");
            break;
        case "handwritten-rejected":
            var handmade = new HandmadeContext();
            Check(handmade.Options.Converters.Count == 0 && handmade.Options.PropertyNamingPolicy is null, "handwritten context presents default options");
            var handmadeOptions = new JsonSerializerOptions { TypeInfoResolver = handmade };
            Check(JsonSerializer.Serialize(new Payload { Value = 7 }, handmadeOptions.GetTypeInfo(typeof(Payload))) == "{\"changed\":7}", "framework reproduces the renamed-property counterexample");
            Reject<NotSupportedException>(() => WorkflowJsonMetadata.Register<HandmadeContext>(), "HandmadeContext", "provenance", "sealed=False");
            WorkflowJsonMetadata.Register<AppContext>();
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "valid retry after handmade context rejection");
            break;
        case "configuration-failures":
            Reject<NotSupportedException>(() => WorkflowJsonMetadata.Register<CamelContext>(), "CamelContext", "options", "sealed=False");
            Reject<NotSupportedException>(() => WorkflowJsonMetadata.Register<SerializationContext>(), "SerializationContext", "Metadata");
            try { WorkflowJsonMetadata.Register<ThrowingContext>(); throw new InvalidOperationException("ASSERTION: throwing constructor was accepted"); }
            catch (Exception exception) when (ReferenceEquals(exception.GetBaseException(), Probe.ConstructorFailure)) { }
            WorkflowJsonMetadata.Register<AppContext>();
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "configuration failure leaves a valid retry before first use");
            break;
        case "authoring-seals":
            _ = Workflow.Ephemeral<Payload>(DefinitionId.New(), DefinitionVersion.Initial);
            Reject<InvalidOperationException>(() => WorkflowJsonMetadata.Register<AppContext>(), "AppContext", "Reflection", "sealed");
            break;
        case "pending-registration-conflict":
            var configuring = Task.Run(() => WorkflowJsonMetadata.Register<BlockingContext>());
            Check(Probe.Entered.Wait(TimeSpan.FromSeconds(15)), "context construction entered the barrier");
            try
            {
                Reject<InvalidOperationException>(() => WorkflowJsonMetadata.Register<DefaultContext>(), "BlockingContext", "DefaultContext", "progress", "sealed=False");
                Check(Probe.DefaultConstructions == 0, "conflicting pending context rejects before construction");
            }
            finally { Probe.Release.Set(); }
            await configuring;
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "the incumbent context publishes intact");
            break;
        case "f1-shape-rejection":
            WorkflowJsonMetadata.Register<AppContext>();
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "unused excluded context root does not reject registration");
            var beforeModels = Probe.ModelConstructions;
            var completion = Workflow.Ephemeral<InitState>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(_ => new InitState()).End();
            var invalid = completion.TryBuild();
            Check(!invalid.IsValid && invalid.Diagnostics.Any(diagnostic => diagnostic.Code == "SFE-TYPE-002" && diagnostic.Message.Contains("Count", StringComparison.Ordinal)), "F1 declared closure fails TryBuild with unsupported-shape diagnostic");
            Reject<WorkflowDefinitionException>(() => completion.Build(), "Count");
            Check(Probe.ModelConstructions == beforeModels && Probe.ModelGetters == 0, "classification never constructs a model or invokes its getters");
            foreach (var number in new[] { 0, 7 })
            {
                Reject<NotSupportedException>(() => Materialize(new InitState { Count = number }), "InitState", "Count", "Generated", "omission");
                Reject<NotSupportedException>(() => Materialize(new NestedInit { Item = new InitState { Count = number } }), "NestedInit", "Count");
                Reject<NotSupportedException>(() => Materialize(new List<InitState> { new() { Count = number } }), "InitState", "Count");
            }
            break;
        case "product-contracts":
            WorkflowJsonMetadata.Register<DefaultContext>();
            Check(Materialize(true), "product owns primitive metadata even when app roots omit it");
            Check(Materialize(EventName.Create("inner space")).Value == "inner space", "product owns its strong-value converter contract");
            Check(Materialize(CorrelationId.Create("correlation")).Value == "correlation", "product-owned ID is not a missing application root");
            Check(Materialize(new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero)).Year == 2026, "product owns framework value metadata");
            break;
        case "idempotence-and-provider-ownership":
            _ = AppContext.Default; // Account for the SDK-owned static Default before observing our owned registration.
            var sdkConstructions = Probe.AppConstructions;
            var services = new ServiceCollection();
            services.AddSingleton<ScopeSentinel>(_ => throw new InvalidOperationException("setup resolved a service"));
            services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions { StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] }, TransientPools = [] });
            WorkflowJsonMetadata.Register<AppContext>();
            WorkflowJsonMetadata.Register<AppContext>();
            using (var provider = services.BuildServiceProvider()) { _ = provider; }
            using (var provider = services.BuildServiceProvider()) { _ = provider; }
            _ = Workflow.Ephemeral<Payload>(DefinitionId.New(), DefinitionVersion.Initial).Init<Payload>(value => value).End().Build();
            _ = Materialize(new Payload { Value = 9 });
            WorkflowJsonMetadata.Register<AppContext>();
            Reject<InvalidOperationException>(() => WorkflowJsonMetadata.Register<DefaultContext>(), "AppContext", "DefaultContext", "sealed=True");
            Check(Probe.AppConstructions == sdkConstructions + 1 && Probe.DefaultConstructions == 0 && Probe.Disposals == 0, "one context is reused across providers, codec copies and seal without disposal or conflicting construction");
            break;
        case "legacy-success-seals":
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "unconfigured legacy event path stays supported");
            Reject<InvalidOperationException>(() => WorkflowJsonMetadata.Register<AppContext>(), "AppContext", "Reflection", "sealed=True");
            break;
        case "legacy-failure-seals":
            Reject<NotSupportedException>(() => Materialize(new HashSet<int> { 1 }));
            Reject<InvalidOperationException>(() => WorkflowJsonMetadata.Register<AppContext>(), "AppContext", "Reflection", "sealed=True");
            break;
        case "missing-metadata-seals":
            _ = AppContext.Default;
            var constructionsBeforeMissing = Probe.AppConstructions;
            WorkflowJsonMetadata.Register<AppContext>();
            var missingCompletion = Workflow.Ephemeral<MissingState>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(_ => new MissingState()).End();
            var missing = missingCompletion.TryBuild();
            Check(!missing.IsValid && missing.Diagnostics.Any(diagnostic => diagnostic.Code == "SFE-TYPE-003" && diagnostic.Message.Contains("MissingState", StringComparison.Ordinal) && diagnostic.Message.Contains("AppContext", StringComparison.Ordinal)), "missing root reports SFE-TYPE-003 with root/context/remedy in TryBuild");
            Reject<WorkflowDefinitionException>(() => missingCompletion.Build(), "MissingState");
            Reject<NotSupportedException>(() => Materialize(new MissingState()), "MissingState", "AppContext", "restart");
            WorkflowJsonMetadata.Register<AppContext>();
            Reject<InvalidOperationException>(() => WorkflowJsonMetadata.Register<DefaultContext>(), "AppContext", "DefaultContext", "sealed=True");
            Check(Probe.AppConstructions == constructionsBeforeMissing + 1 && Probe.ModelGetters == 0, "failed lookup seals Generated with no fallback or repeated context");
            break;
        case "first-use-wins-race":
            var losing = Task.Run(() => WorkflowJsonMetadata.Register<BlockingContext>());
            Check(Probe.Entered.Wait(TimeSpan.FromSeconds(15)), "constructor entered outside the global lock");
            try { Check(Materialize(new Payload { Value = 7 }).Value == 7, "first use proceeds in complete Reflection mode while constructor is blocked"); }
            finally { Probe.Release.Set(); }
            try { await losing; throw new InvalidOperationException("ASSERTION: pending registration must lose to legacy seal"); }
            catch (InvalidOperationException exception) { Check(exception.Message.Contains("Reflection", StringComparison.Ordinal) && exception.Message.Contains("sealed=True", StringComparison.Ordinal), "loser identifies legacy seal"); }
            Reject<InvalidOperationException>(() => WorkflowJsonMetadata.Register<AppContext>(), "Reflection", "sealed=True");
            break;
        case "same-context-race":
            Probe.BlockEnabled = false;
            _ = BlockingContext.Default; // The framework owns this independent generated Default instance.
            Probe.BlockEnabled = true;
            var leading = Task.Run(() => WorkflowJsonMetadata.Register<BlockingContext>());
            Check(Probe.Entered.Wait(TimeSpan.FromSeconds(15)), "first context construction reached barrier");
            using (var releaseWaiters = new Barrier(5))
            {
                var repeats = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
                {
                    Check(releaseWaiters.SignalAndWait(TimeSpan.FromSeconds(15)), "registration callers joined barrier");
                    WorkflowJsonMetadata.Register<BlockingContext>();
                })).ToArray();
                Check(releaseWaiters.SignalAndWait(TimeSpan.FromSeconds(15)), "all duplicate callers were released");
                Probe.Release.Set();
                await Task.WhenAll(repeats.Append(leading));
            }
            Check(Probe.BlockingConstructions == 1 && Materialize(new Payload { Value = 7 }).Value == 7, "concurrent repeats publish one complete context");
            break;
        case "reentrant-registration":
            try { WorkflowJsonMetadata.Register<ReentrantContext>(); throw new InvalidOperationException("ASSERTION: reentry was accepted"); }
            catch (Exception exception) { Check(exception.GetBaseException() is InvalidOperationException && exception.GetBaseException().Message.Contains("reentrant", StringComparison.Ordinal), "reentrant configuration fails explicitly without deadlock"); }
            WorkflowJsonMetadata.Register<AppContext>();
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "reentrant failure publishes nothing and permits retry");
            break;
        case "native-missing-install":
            System.AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", false);
            Check(!JsonSerializer.IsReflectionEnabledByDefault, "negative control disabled reflection JSON");
            Reject<InvalidOperationException>(() => Workflow.Ephemeral<Payload>(DefinitionId.New(), DefinitionVersion.Initial), "AddOrcaCoreEphemeralEngine", "before authoring");
            break;
        case "static-bootstrap":
            System.AppContext.SetSwitch("System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault", false);
            Check(!JsonSerializer.IsReflectionEnabledByDefault, "static bootstrap control disabled reflection JSON");
            new ServiceCollection().AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions { StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] }, TransientPools = [] });
            WorkflowJsonMetadata.Register<AppContext>();
            _ = Workflow.Ephemeral<Payload>(DefinitionId.New(), DefinitionVersion.Initial).Init<Payload>(value => value).End().Build();
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "static role installation precedes authoring with JSON reflection disabled");
            break;
        case "f1-positive-generated":
        case "f1-positive-legacy":
            if (args[0].EndsWith("generated", StringComparison.Ordinal)) WorkflowJsonMetadata.Register<AppContext>();
            foreach (var number in new[] { 0, 7 })
            {
                Check(Materialize(new ValueState(number)).Number == number, "positional value-state initializer alone stays admitted");
                Check(Materialize(new MutableState { Count = number }).Count == (number == 0 ? 42 : 7), "mutable omission/initializer retains legacy round trip");
                Check(Materialize(new ConstructorState(number)).Count == number, "genuine constructor-bound omission retains legacy round trip");
                if (args[0].EndsWith("legacy", StringComparison.Ordinal))
                    Check(Materialize(new InitState { Count = number }).Count == (number == 0 ? 42 : 7), "legacy init-only behavior remains unchanged");
            }
            break;
        case "f1-member-metadata":
            WorkflowJsonMetadata.Register<AppContext>();
            var beforeInspection = Probe.ModelConstructions;
            var fields = Workflow.Ephemeral<IncludedField>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(_ => new IncludedField { Count = 0 }).End().TryBuild();
            Check(fields.Diagnostics.Any(diagnostic => diagnostic.Code == "SFE-TYPE-002" && diagnostic.Message.Contains("Count", StringComparison.Ordinal)), "included field synthetic initializer is classified");
            var derived = Workflow.Ephemeral<ShapeBase>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(_ => new ShapeBase()).End().TryBuild();
            Check(derived.Diagnostics.Any(diagnostic => diagnostic.Code == "SFE-TYPE-002" && diagnostic.Message.Contains("ExcludedDerived", StringComparison.Ordinal)), "approved derived closure is classified even if current value uses base");
            var getter = Workflow.Ephemeral<GetterInit>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(_ => new GetterInit()).End().TryBuild();
            Check(getter.Diagnostics.Any(diagnostic => diagnostic.Code == "SFE-TYPE-002"), "getter-bearing init member is classified by metadata");
            Check(Probe.ModelConstructions == beforeInspection && Probe.ModelGetters == 0, "field/derived/getter classifier runs no application model code");
            break;
        case "unsupported-before-missing":
            WorkflowJsonMetadata.Register<DefaultContext>();
            var unsupported = Workflow.Ephemeral<HashSet<int>>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(_ => []).End().TryBuild();
            Check(unsupported.Diagnostics.Any(diagnostic => diagnostic.Code == "SFE-TYPE-002") && unsupported.Diagnostics.All(diagnostic => diagnostic.Code != "SFE-TYPE-003"), "known unsupported collection stays SFE-TYPE-002 even when metadata is also absent");
            break;
        case "codec-negative-boundaries":
            WorkflowJsonMetadata.Register<AppContext>();
            var cycle = new Link(); cycle.Next = cycle;
            Reject<JsonException>(() => Materialize(cycle), "Cyclic");
            Reject<NotSupportedException>(() => Materialize<BaseValue>(new DerivedValue()), "polymorphic");
            Reject<NotSupportedException>(() => Materialize(new Customized()), "not supported");
            Reject<NotSupportedException>(() => Materialize(new MissingState()), "MissingState");
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "rejected direct values do not corrupt the incumbent context");
            break;
        case "completed-constructor-context":
            WorkflowJsonMetadata.Register<SpawningContext>();
            Probe.Published.TrySetResult();
            await Task.WhenAll(Probe.SpawnedCalls);
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "a completed constructor execution context may repeat registration after publication");
            break;
        case "golden-generated":
        case "golden-legacy":
            if (args[0].EndsWith("generated", StringComparison.Ordinal)) WorkflowJsonMetadata.Register<AppContext>();
            Console.WriteLine("GOLDEN " + await CodecCases.Golden());
            break;
        case "failure-and-size-legacy":
            await CodecCases.Failures();
            break;
        case "failure-and-size-boundaries":
            WorkflowJsonMetadata.Register<AppContext>();
            await CodecCases.Failures();
            break;
        case "explicit-empty-converters":
            WorkflowJsonMetadata.Register<EmptyConvertersContext>();
            Check(Materialize(new Payload { Value = 7 }).Value == 7, "an explicitly empty converter list is still ordinary default metadata");
            break;
        default: throw new InvalidOperationException("Unknown scenario");
    }
    Console.WriteLine("PASS " + args.Single());
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    return 1;
}

static T Materialize<T>(T value) => WorkflowInboundEvent<T>.Create(
    WorkflowEventContract<T>.Create(EventName.Create("metadata"), EventContractVersion.Initial),
    EventId.Create("event-metadata"), CorrelationId.Create("metadata"), null, DateTimeOffset.UtcNow,
    new WorkflowEventRoute.Correlation(DefinitionId.New()), value).Payload;

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("ASSERTION: " + message);
}

static TException Reject<TException>(Action action, params string[] details) where TException : Exception
{
    try { action(); }
    catch (TException exception)
    {
        foreach (var detail in details) Check(exception.Message.Contains(detail, StringComparison.Ordinal), "diagnostic contains " + detail + ": " + exception.Message);
        return exception;
    }
    throw new InvalidOperationException("ASSERTION: expected " + typeof(TException).Name);
}

public enum Mode { None, Ready }
public sealed class EnumState { public Mode State { get; set; } }
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, UseStringEnumConverter = true)]
[JsonSerializable(typeof(EnumState))]
public partial class EnumContext : JsonSerializerContext;

public sealed class Payload { public int Value { get; set; } }
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Payload))]
[JsonSerializable(typeof(EnumState))]
[JsonSerializable(typeof(InitState))]
[JsonSerializable(typeof(NestedInit))]
[JsonSerializable(typeof(List<InitState>))]
[JsonSerializable(typeof(ValueState))]
[JsonSerializable(typeof(MutableState))]
[JsonSerializable(typeof(ConstructorState))]
[JsonSerializable(typeof(IncludedField))]
[JsonSerializable(typeof(ShapeBase))]
[JsonSerializable(typeof(GetterInit))]
[JsonSerializable(typeof(Link))]
[JsonSerializable(typeof(BaseValue))]
[JsonSerializable(typeof(Customized))]
[JsonSerializable(typeof(IReadOnlyList<int>))]
[JsonSerializable(typeof(IReadOnlyDictionary<string, int>))]
[JsonSerializable(typeof(CorpusState))]
[JsonSerializable(typeof(TextState))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSerializable(typeof(MemberBoundary))]
[JsonSerializable(typeof(string[]))]
public partial class AppContext : JsonSerializerContext, IDisposable
{
    private readonly int sentinel = Interlocked.Increment(ref Probe.AppConstructions);
    public void Dispose() => Interlocked.Increment(ref Probe.Disposals);
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Payload))]
public partial class DefaultContext : JsonSerializerContext { private readonly int sentinel = Interlocked.Increment(ref Probe.DefaultConstructions); }
public sealed class HandmadeContext : JsonSerializerContext, IJsonTypeInfoResolver
{
    public HandmadeContext() : base(null) { }
    protected override JsonSerializerOptions GeneratedSerializerOptions => new();
    public override JsonTypeInfo? GetTypeInfo(Type type) => ((IJsonTypeInfoResolver)this).GetTypeInfo(type, Options);
    JsonTypeInfo? IJsonTypeInfoResolver.GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (type == typeof(Payload))
        {
            var info = JsonTypeInfo.CreateJsonTypeInfo<Payload>(options);
            info.CreateObject = () => new Payload();
            var prop = info.CreateJsonPropertyInfo(typeof(int), "changed");
            prop.Get = value => ((Payload)value).Value;
            prop.Set = (value, member) => ((Payload)value).Value = (int)member!;
            info.Properties.Add(prop);
            return info;
        }
        if (type == typeof(int)) return ((IJsonTypeInfoResolver)DefaultContext.Default).GetTypeInfo(type, options);
        return null;
    }
}

public static class Probe
{
    public static readonly ManualResetEventSlim Entered = new();
    public static readonly ManualResetEventSlim Release = new();
    public static int AppConstructions;
    public static int Disposals;
    public static readonly TaskCompletionSource Published = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public static readonly List<Task> SpawnedCalls = [];
    public static int SpawnAfterPublication()
    {
        SpawnedCalls.Add(Task.Run(async () => { await Published.Task; WorkflowJsonMetadata.Register<SpawningContext>(); }));
        return 0;
    }
    public static int Reentries;
    public static int Reenter()
    {
        if (Interlocked.Increment(ref Reentries) == 1)
            Task.Run(() => WorkflowJsonMetadata.Register<ReentrantContext>()).GetAwaiter().GetResult();
        return 0;
    }
    public static int ModelConstructions;
    public static int ModelGetters;
    public static int DefaultConstructions;
    public static int BlockingConstructions;
    public static bool BlockEnabled = true;
    public static int BlockConstruction()
    {
        if (!BlockEnabled) return 0;
        Interlocked.Increment(ref BlockingConstructions);
        Entered.Set();
        if (!Release.Wait(TimeSpan.FromSeconds(15))) throw new InvalidOperationException("constructor barrier not released");
        return 0;
    }
    public static readonly Exception ConstructorFailure = new ApplicationException("original context constructor failure");
    public static int FailConstruction() => throw ConstructorFailure;
}
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Payload))]
public partial class CamelContext : JsonSerializerContext;
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(Payload))]
public partial class SerializationContext : JsonSerializerContext;
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Payload))]
public partial class ThrowingContext : JsonSerializerContext { private readonly int sentinel = Probe.FailConstruction(); }

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Payload))]
public partial class BlockingContext : JsonSerializerContext { private readonly int sentinel = Probe.BlockConstruction(); }

public sealed class InitState
{
    public InitState() => Interlocked.Increment(ref Probe.ModelConstructions);
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Count { get; init; } = 42;
    [JsonIgnore] public int Guard { get { Interlocked.Increment(ref Probe.ModelGetters); throw new InvalidOperationException("model getter ran"); } }
}
public sealed class NestedInit { public InitState Item { get; set; } = null!; }

public sealed class ScopeSentinel;
public sealed class MissingState { public int Value { get; set; } }
[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Payload))]
public partial class ReentrantContext : JsonSerializerContext { private readonly int sentinel = Probe.Reenter(); }

public readonly record struct ValueState(int Number);
public sealed class MutableState { [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Count { get; set; } = 42; }
public sealed record ConstructorState([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int Count);
public sealed class IncludedField
{
    public IncludedField() => Interlocked.Increment(ref Probe.ModelConstructions);
    [JsonInclude, JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public required int Count = 42;
}
[JsonPolymorphic, JsonDerivedType(typeof(ExcludedDerived), "excluded")]
public class ShapeBase { public int Value { get; set; } }
public sealed class ExcludedDerived : ShapeBase { [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Count { get; init; } = 42; }
public sealed class GetterInit
{
    public GetterInit() => Interlocked.Increment(ref Probe.ModelConstructions);
    private int count = 42;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Count { get { Interlocked.Increment(ref Probe.ModelGetters); throw new InvalidOperationException("getter ran"); } init => count = value; }
}
public sealed class Link { public Link? Next { get; set; } }
public class BaseValue { public int Value { get; set; } }
public sealed class DerivedValue : BaseValue;
[JsonConverter(typeof(CustomizedConverter))]
public sealed class Customized;
public sealed class CustomizedConverter : JsonConverter<Customized>
{
    public override Customized Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new InvalidOperationException("application converter ran");
    public override void Write(Utf8JsonWriter writer, Customized value, JsonSerializerOptions options) => throw new InvalidOperationException("application converter ran");
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(Payload))]
public partial class SpawningContext : JsonSerializerContext { private readonly int sentinel = Probe.SpawnAfterPublication(); }

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata, Converters = new Type[] { })]
[JsonSerializable(typeof(Payload))]
public partial class EmptyConvertersContext : JsonSerializerContext;

public sealed class MemberBoundary
{
    public static int Reads;
    [JsonInclude]
    internal int Hidden { get { Reads++; return 7; } set { } }
}
