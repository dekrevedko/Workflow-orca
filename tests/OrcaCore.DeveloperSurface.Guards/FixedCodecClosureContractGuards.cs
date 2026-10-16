using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.DeveloperSurface.Guards;

/// <summary>
/// Locks the fixed-codec closure recorded in
/// <c>docs/review/developer-facing-interface-section-04-codec-remediation-independent-rereview-2026-07-27.md</c>.
/// </summary>
/// <remarks>
/// Each guard corresponds to a release blocker from that review and executes the path through the
/// public durable surface rather than asserting on type names, so a future refactor that reopens a
/// bypass fails here instead of passing a name-shaped check.
/// </remarks>
[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class FixedCodecClosureContractGuards
{
    /// <summary>
    /// The contract applies <c>orcacore-json-v1</c> to output values, not only to input and state,
    /// and requires unapproved runtime-type substitutions to fail before commit. The state graph
    /// here is fully approved; only the projection to the declared output type substitutes.
    /// </summary>
    [Fact]
    public async Task TypedCompletionOutput_RejectsUnapprovedPolymorphismBeforeCommit()
    {
        using var host = CreateHost();
        var registry = host.GetRequiredService<IWorkflowDefinitionRegistry>();
        var starts = host.GetRequiredService<IWorkflowStartIdempotencyStore>();
        var projections = host.GetRequiredService<IWorkflowProjectionStore>();
        var definitionId = DefinitionId.New();
        var definition = global::OrcaCore.Workflow
            .Durable<ConcreteCodecState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(value => new ConcreteCodecState
            {
                Item = new CodecDerived(value, "derived-only-data")
            })
            .End<CodecBase>(snapshot => snapshot.Value.Item)
            .Build();
        var handle = registry.Register(definition).GetHandleOrThrow();

        var act = () => handle.StartOrGetAsync(
            "value",
            StartIdempotencyKey.Create("typed-output-polymorphism"),
            CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<NotSupportedException>(
                "the fixed codec governs output values, not only input and state")
            .WithMessage("*polymorphic*not supported*");
        var binding = await starts.GetStartedAsync(
            "typed-output-polymorphism",
            CancellationToken.None);
        binding.HasValue.Should().BeTrue("the output rejection occurs at the terminal boundary");
        var projection = await projections.GetAsync(binding.Value.InstanceId, CancellationToken.None);
        projection.HasValue.Should().BeTrue();
        projection.Value.Status.Should().NotBe(
            WorkflowInstanceStatus.Completed,
            "no instance may complete on an output the codec cannot represent");
    }

    /// <summary>
    /// The complement of the guard above: a statically approved contract must still round-trip every
    /// member, so the closure cannot be satisfied by rejecting or truncating legitimate output.
    /// </summary>
    [Fact]
    public async Task TypedCompletionOutput_RoundTripsApprovedPolymorphismWithoutLoss()
    {
        using var host = CreateHost();
        var registry = host.GetRequiredService<IWorkflowDefinitionRegistry>();
        var store = host.GetRequiredService<IWorkflowEventStore>();
        var definitionId = DefinitionId.New();
        var definition = global::OrcaCore.Workflow
            .Durable<ApprovedCodecState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(value => new ApprovedCodecState
            {
                Item = new ApprovedDerived(value, "derived-only-data")
            })
            .End<ApprovedBase>(snapshot => snapshot.Value.Item)
            .Build();
        var handle = registry.Register(definition).GetHandleOrThrow();

        var started = await handle.StartOrGetAsync(
            "value",
            StartIdempotencyKey.Create("typed-output-approved"),
            CancellationToken.None);

        var checkpoint = await store.LoadCheckpointAsync(
            started.GetHandleOrThrow().InstanceId,
            CancellationToken.None);
        checkpoint.HasValue.Should().BeTrue();
        var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
        envelope.Output.Should().NotBeNull();
        Encoding.UTF8.GetString(envelope.Output!.Payload).Should().Contain(
            "derived-only-data",
            "an approved contract must persist every member the author observed");
    }

    /// <summary>
    /// An application-owned <see cref="JsonConverterAttribute"/> would let the application, not the
    /// codec, choose the persisted bytes, breaking the determinism AC-024 requires. The type must be
    /// rejected rather than persisted under the fixed content type.
    /// </summary>
    [Fact]
    public async Task FixedCodec_RejectsApplicationOwnedJsonConvertersAtBuildBeforeProviderMutation()
    {
        using var host = CreateHost();
        var store = host.GetRequiredService<IWorkflowStartIdempotencyStore>();
        var definitionId = DefinitionId.New();
        var validation = global::OrcaCore.Workflow
            .Durable<ConverterState>(definitionId, DefinitionVersion.Initial)
            .Init<ConverterInput>(input => new ConverterState { Value = input.Value })
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse(
            "an application converter would choose the persisted bytes instead of the fixed codec");
        validation.Diagnostics.Should().ContainSingle(diagnostic =>
            diagnostic.Code == "SFE-TYPE-002");
        (await store.GetStartedAsync("application-converter", CancellationToken.None))
            .HasValue.Should().BeFalse("build-time rejection precedes every provider operation");
    }

    /// <summary>
    /// The fixed codec is the only producer of persisted workflow payloads. A command surface that
    /// copies caller-supplied content type and bytes into <c>WorkflowStartedEvent</c> would commit a
    /// start fact the codec never produced, leaving an instance the driver can never deserialize.
    /// </summary>
    [Fact]
    public void RawStartCommand_HasNoPublicApplicationExecutionSurface()
    {
        var exported = PublicSurfaceCatalog.Assemblies
            .SelectMany(assembly => assembly.GetExportedTypes())
            .ToArray();

        exported.Should().NotContain(type =>
            type.FullName == "OrcaCore.Engine.Durable.Execution.DurableCommandProcessor");
        exported.Where(type => type != typeof(StartWorkflowCommand))
            .SelectMany(type => type.GetMethods())
            .Should().NotContain(method => method.GetParameters().Any(parameter =>
                    parameter.ParameterType == typeof(StartWorkflowCommand)),
                "applications must not receive a raw command path around the fixed codec");
    }

    private static ServiceProvider CreateHost()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = ResourceGovernancePartitionId.Create(
                    $"fixed-codec-guard-{Guid.NewGuid():N}"),
                Pools = []
            }
        });
        return services.BuildServiceProvider();
    }

    public record CodecBase(string Value);

    public sealed record CodecDerived(string Value, string Detail) : CodecBase(Value);

    public sealed class ConcreteCodecState
    {
        public CodecDerived Item { get; set; } = new("unset", "unset");
    }

    [JsonDerivedType(typeof(ApprovedDerived), typeDiscriminator: "derived")]
    public record ApprovedBase(string Value);

    public sealed record ApprovedDerived(string Value, string Detail) : ApprovedBase(Value);

    public sealed class ApprovedCodecState
    {
        public ApprovedDerived Item { get; set; } = new("unset", "unset");
    }

    [JsonConverter(typeof(ApplicationChosenConverter))]
    public sealed class ConverterInput
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class ConverterState
    {
        public string Value { get; set; } = string.Empty;
    }

    public sealed class ApplicationChosenConverter : JsonConverter<ConverterInput>
    {
        public override ConverterInput Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            reader.Skip();
            return new ConverterInput();
        }

        public override void Write(
            Utf8JsonWriter writer,
            ConverterInput value,
            JsonSerializerOptions options)
        {
            writer.WriteStringValue($"application-chosen-{Guid.NewGuid()}");
        }
    }
}
