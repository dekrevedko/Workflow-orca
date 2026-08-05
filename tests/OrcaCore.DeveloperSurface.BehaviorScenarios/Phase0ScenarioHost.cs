using System.Text.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Hosting;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static class Phase0ScenarioHost
{
    [Phase0Scenario("definition-id-nonempty", "3.5")]
    public static void DefinitionIdIsNeverEmpty(Phase0ScenarioContext context)
    {
        var created = context.Observe(_ => DefinitionId.New());
        Phase0Assert.Satisfies(created, value => value.Value != Guid.Empty, "DefinitionId.New returned Guid.Empty.");

        DefinitionId? parsed = null;
        var empty = Guid.Empty.ToString();
        var accepted = context.Observe(_ => DefinitionId.TryParse(empty, out parsed));
        Phase0Assert.Satisfies(accepted, value => !value, "DefinitionId.TryParse accepted Guid.Empty.");
        if (parsed is not null) throw new InvalidOperationException("DefinitionId.TryParse returned a value for Guid.Empty.");

        var rejected = context.ObserveThrows<ArgumentException, DefinitionId>(_ => DefinitionId.Parse(empty));
        Phase0Assert.Satisfies(rejected, exception => exception.ParamName == "value", "DefinitionId.Parse accepted Guid.Empty.");
    }

    [Phase0Scenario("fixed-codec-determinism", "3.5")]
    public static async Task FixedCodecTypesCompileIntoStableDefinitions(Phase0ScenarioContext context)
    {
        var id = DefinitionId.New();
        var completion = Workflow.Ephemeral<CodecState>(id, DefinitionVersion.Initial)
            .Init<CodecState>(state => new CodecState(state.Value, [.. state.Items]))
            .End();

        var validation = context.Observe(_ => completion.TryBuild());
        Phase0Assert.Satisfies(
            validation,
            result => result.TryGetValue(out var definition) &&
                      definition!.DefinitionFingerprint.Value.Length > 0,
            "A fixed-codec-compatible detached state graph did not compile deterministically.");

        using var store = new DurableScenarioProvider();
        using var runtime = DurableScenarioRuntime.Create(store);
        var durableId = DefinitionId.New();
        var durableDefinition = Workflow.Durable<CodecState>(durableId, DefinitionVersion.Initial)
            .Init<CodecInput>(input => new CodecState(input.Value, [.. input.Items]))
            .End()
            .Build();
        runtime.Register(durableDefinition);

        var source = new CodecInput(7, ["first", "second"], null);
        var firstResult = await runtime.StartOrGetAsync<CodecInput, CodecState>(
            "fixed-codec-first",
            durableId,
            DefinitionVersion.Initial,
            source,
            CancellationToken.None);
        source.Items[0] = "mutated";
        var secondResult = await runtime.StartOrGetAsync<CodecInput, CodecState>(
            "fixed-codec-second",
            durableId,
            DefinitionVersion.Initial,
            new CodecInput(7, ["first", "second"], null),
            CancellationToken.None);
        var first = (await store.LoadTailAsync(
                new WorkflowStreamId(firstResult.InstanceId),
                StreamVersion.Empty,
                CancellationToken.None))
            .OfType<WorkflowStartedEvent>()
            .Single();
        var second = (await store.LoadTailAsync(
                new WorkflowStreamId(secondResult.InstanceId),
                StreamVersion.Empty,
                CancellationToken.None))
            .OfType<WorkflowStartedEvent>()
            .Single();
        var restored = JsonSerializer.Deserialize<CodecInput>(first.InputPayload!);
        if (first.InputContentType != "orcacore-json-v1" ||
            second.InputContentType != "orcacore-json-v1" ||
            !first.InputPayload!.SequenceEqual(second.InputPayload!) ||
            restored is null ||
            restored.Value != 7 ||
            !restored.Items.SequenceEqual(["first", "second"]) ||
            restored.Note is not null ||
            ReferenceEquals(restored.Items, source.Items))
        {
            throw new InvalidOperationException("The fixed codec was not deterministic, typed, and detached.");
        }

        var cyclic = new CyclicCodecState();
        cyclic.Self = cyclic;
        await AssertFixedCodecRejectsBeforeProviderMutationAsync<CyclicCodecState, JsonException>(
            "cycle",
            cyclic);
        await AssertFixedCodecRejectsBeforeProviderMutationAsync<CodecBase, NotSupportedException>(
            "polymorphic-root",
            new CodecDerived("base", "derived"));
        await AssertFixedCodecRejectsBeforeProviderMutationAsync<MemberCodecGraph, NotSupportedException>(
            "polymorphic-member",
            new MemberCodecGraph(new CodecDerived("base", "derived")));
        await AssertFixedCodecRejectsBeforeProviderMutationAsync<CollectionCodecGraph, NotSupportedException>(
            "polymorphic-collection",
            new CollectionCodecGraph([new CodecDerived("base", "derived")]));
        AssertFixedCodecShapeRejectedAtBuild<DictionaryKeyCodecGraph>(
            "non-string dictionary keys");
        await AssertFixedCodecRejectsBeforeProviderMutationAsync<DictionaryValueCodecGraph, NotSupportedException>(
            "polymorphic-dictionary-value",
            new DictionaryValueCodecGraph(new Dictionary<string, CodecBase>
            {
                ["item"] = new CodecDerived("base", "derived")
            }));

        var converterValidation = Workflow.Durable<ApplicationConvertedCodecState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<ApplicationConvertedCodecState>(value => value)
            .End()
            .TryBuild();
        if (converterValidation.IsValid ||
            converterValidation.Diagnostics.Count != 1 ||
            converterValidation.Diagnostics[0].Code != "SFE-TYPE-002")
        {
            throw new InvalidOperationException(
                "An application JsonConverter was admitted into orcacore-json-v1.");
        }

        var durableAssembly = Assembly.Load("OrcaCore.Engine.Durable");
        var exported = durableAssembly.GetExportedTypes()
            .Concat(typeof(IWorkflowEventStore).Assembly.GetExportedTypes())
            .Select(type => type.FullName)
            .ToHashSet(StringComparer.Ordinal);
        if (exported.Contains("OrcaCore.Abstractions.Providers.IWorkflowPayloadSerializer") ||
            exported.Contains("OrcaCore.Abstractions.Providers.IWorkflowPayloadCodec") ||
            exported.Contains("OrcaCore.Engine.Durable.Execution.ContentTypeWorkflowPayloadSerializer"))
        {
            throw new InvalidOperationException("A public codec replacement seam remains available.");
        }
    }

    [Phase0Scenario("structural-fingerprint-opacity", "3.5")]
    public static void StructuralFingerprintIgnoresOpaqueDelegateIdentity(Phase0ScenarioContext context)
    {
        var id = DefinitionId.New();
        var first = Workflow.Ephemeral<FingerprintState>(id, DefinitionVersion.Initial)
            .Init<int>(value => new FingerprintState(value))
            .End()
            .Build();
        var sameStructure = Workflow.Ephemeral<FingerprintState>(id, DefinitionVersion.Initial)
            .Init<int>(input => new FingerprintState(input))
            .End()
            .Build();
        var changedStructure = Workflow.Ephemeral<FingerprintState>(id, DefinitionVersion.Initial)
            .Init<int>(value => new FingerprintState(value))
            .Delay(TimeSpan.FromMilliseconds(1))
            .End()
            .Build();
        var bumpedVersion = Workflow.Ephemeral<FingerprintState>(id, new DefinitionVersion(2))
            .Init<int>(value => new FingerprintState(value + 1))
            .End()
            .Build();
        var requestA = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("pool-a"), 1));
        var requestB = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("pool-b"), 2));
        var leasedA = Workflow.Durable<FingerprintState>(id, DefinitionVersion.Initial)
            .Init<int>(value => new FingerprintState(value))
            .AcquireResources(requestA, _ => { })
            .End()
            .Build();
        var leasedB = Workflow.Durable<FingerprintState>(id, DefinitionVersion.Initial)
            .Init<int>(value => new FingerprintState(value))
            .AcquireResources(requestB, _ => { })
            .End()
            .Build();

        var fingerprint = context.Observe(_ => first.DefinitionFingerprint.Value);
        Phase0Assert.Satisfies(
            fingerprint,
            value => value == sameStructure.DefinitionFingerprint.Value &&
                     value != changedStructure.DefinitionFingerprint.Value &&
                     value == bumpedVersion.DefinitionFingerprint.Value &&
                     leasedA.DefinitionFingerprint != leasedB.DefinitionFingerprint,
            "Structural fingerprint depended on opaque delegate/root version identity or ignored a graph/static request change.");
    }

    [Phase0Scenario("attempt-local-replace-state", "3.5")]
    public static async Task ReplaceStateChangesOnlyTheAttemptLocalContext(Phase0ScenarioContext context)
    {
        var attempts = 0;
        var committedValue = -1;
        var definitionId = DefinitionId.New();
        var publicDefinition = Workflow.Ephemeral<AttemptState>(definitionId, DefinitionVersion.Initial)
            .Init<int>(value => new AttemptState(value))
            .Then(step =>
            {
                attempts++;
                if (attempts == 1)
                {
                    step.State.Value = 99;
                    throw new InvalidOperationException("fail first detached attempt");
                }

                if (step.State.Value != 1)
                    throw new InvalidOperationException("Failed-attempt mutation leaked into retry state.");
                var replacement = context.Observe(
                    _ => step.ReplaceState(new AttemptState(2)));
                Phase0Assert.Completed(
                    replacement,
                    "ReplaceState did not update the successful attempt-local context.");
                return ValueTask.CompletedTask;
            })
            .WithRetry(2)
            .Then(step =>
            {
                committedValue = step.State.Value;
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            TransientPools = []
        });
        await using var provider = services.BuildServiceProvider();
        var handle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(publicDefinition)
            .GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            1,
            StartIdempotencyKey.Create("attempt-state-is-transactional"),
            CancellationToken.None)).GetHandleOrThrow();
        var terminal = await instance.GetSnapshotAsync(CancellationToken.None);
        var committed = await instance.GetStateAsync<AttemptState>(CancellationToken.None);
        if (terminal.Status != WorkflowInstanceStatus.Completed ||
            attempts != 2 || committedValue != 2 || committed.Value != 2)
        {
            throw new InvalidOperationException(
                "The runtime did not discard failed attempt state and commit the successful replacement.");
        }
    }

    private static void AssertThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static async Task AssertThrowsAsync<TException>(Func<Task> action, string message)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static async Task AssertFixedCodecRejectsBeforeProviderMutationAsync<TGraph, TException>(
        string caseName,
        TGraph graph)
        where TException : Exception
    {
        using var store = new DurableScenarioProvider();
        using var runtime = DurableScenarioRuntime.Create(store);
        var definitionId = DefinitionId.New();
        var definition = Workflow.Durable<TGraph>(definitionId, DefinitionVersion.Initial)
            .Init<TGraph>(value => value)
            .End()
            .Build();
        runtime.Register(definition);
        var idempotencyKey = $"fixed-codec-{caseName}";

        await AssertThrowsAsync<TException>(
            () => runtime.StartOrGetAsync<TGraph, TGraph>(
                idempotencyKey,
                definitionId,
                DefinitionVersion.Initial,
                graph,
                CancellationToken.None).AsTask(),
            $"The fixed codec accepted invalid {caseName} input.");

        var started = await store.GetStartedAsync(idempotencyKey, CancellationToken.None);
        if (started.HasValue)
        {
            throw new InvalidOperationException(
                $"The fixed codec mutated the provider before rejecting {caseName} input.");
        }
    }

    private static void AssertFixedCodecShapeRejectedAtBuild<TGraph>(string caseName)
    {
        var validation = Workflow.Durable<TGraph>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<TGraph>(value => value)
            .End()
            .TryBuild();

        if (validation.IsValid ||
            validation.Diagnostics.Count != 1 ||
            validation.Diagnostics[0].Code != "SFE-TYPE-002")
        {
            throw new InvalidOperationException(
                $"The fixed codec admitted {caseName} into orcacore-json-v1.");
        }
    }

    private sealed record CodecInput(int Value, List<string> Items, string? Note);
    private sealed record CodecState(int Value, List<string> Items);
    private sealed class CyclicCodecState { public CyclicCodecState? Self { get; set; } }
    private record CodecBase(string Value);
    private sealed record CodecDerived(string Value, string Detail) : CodecBase(Value);
    private sealed record MemberCodecGraph(CodecBase Member);
    private sealed record CollectionCodecGraph(List<CodecBase> Items);
    private sealed record DictionaryKeyCodecGraph(Dictionary<CodecBase, string> Map);
    private sealed record DictionaryValueCodecGraph(Dictionary<string, CodecBase> Map);

    [JsonConverter(typeof(ApplicationConvertedCodecStateConverter))]
    private sealed record ApplicationConvertedCodecState(string Value);

    private sealed class ApplicationConvertedCodecStateConverter
        : JsonConverter<ApplicationConvertedCodecState>
    {
        public override ApplicationConvertedCodecState? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            return new ApplicationConvertedCodecState(reader.GetString()!);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ApplicationConvertedCodecState value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue($"{value.Value}-{Guid.NewGuid():N}");
        }
    }

    private sealed record FingerprintState(int Value);
    private sealed class AttemptState(int value) { public int Value { get; set; } = value; }
}
