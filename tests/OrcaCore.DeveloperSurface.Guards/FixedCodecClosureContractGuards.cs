using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
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
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store);
        var definitionId = DefinitionId.New();
        var definition = global::OrcaCore.Workflow
            .Durable<ConcreteCodecState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(value => new ConcreteCodecState
            {
                Item = new CodecDerived(value, "derived-only-data")
            })
            .End<CodecBase>(snapshot => snapshot.Value.Item)
            .Build();
        RegisterDefinition(runtime, definition, typeof(ConcreteCodecState));

        var act = () => runtime.StartOrGetAsync<string, ConcreteCodecState>(
            "typed-output-polymorphism",
            definitionId,
            DefinitionVersion.Initial,
            "value",
            CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>(
                "the fixed codec governs output values, not only input and state")
            .WithMessage("*polymorphic*not supported*");
        (await store.ListAsync(new WorkflowProjectionQuery(), CancellationToken.None))
            .Should().NotContain(
                snapshot => snapshot.Status == WorkflowStatus.Completed,
                "no instance may complete on an output the codec cannot represent");
    }

    /// <summary>
    /// The complement of the guard above: a statically approved contract must still round-trip every
    /// member, so the closure cannot be satisfied by rejecting or truncating legitimate output.
    /// </summary>
    [Fact]
    public async Task TypedCompletionOutput_RoundTripsApprovedPolymorphismWithoutLoss()
    {
        var store = new InMemoryWorkflowProvider();
        var runtime = CreateRuntime(store);
        var definitionId = DefinitionId.New();
        var definition = global::OrcaCore.Workflow
            .Durable<ApprovedCodecState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(value => new ApprovedCodecState
            {
                Item = new ApprovedDerived(value, "derived-only-data")
            })
            .End<ApprovedBase>(snapshot => snapshot.Value.Item)
            .Build();
        RegisterDefinition(runtime, definition, typeof(ApprovedCodecState));

        var started = await runtime.StartOrGetAsync<string, ApprovedCodecState>(
            "typed-output-approved",
            definitionId,
            DefinitionVersion.Initial,
            "value",
            CancellationToken.None);

        var checkpoint = await store.LoadCheckpointAsync(started.InstanceId, CancellationToken.None);
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
        var store = new InMemoryWorkflowProvider();
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
    public async Task RawStartCommand_CannotCommitPayloadBytesTheFixedCodecDidNotProduce()
    {
        var store = new InMemoryWorkflowProvider();
        var processor = new DurableCommandProcessor(store);
        var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());

        var act = () => processor.ProcessAsync(
            new StartWorkflowCommand
            {
                InstanceId = instanceId,
                CommandId = CommandId.New(),
                RequestedAt = DateTimeOffset.UtcNow,
                DefinitionId = DefinitionId.New(),
                DefinitionVersion = DefinitionVersion.Initial,
                InputContentType = "application/x-foreign",
                InputPayload = [0xDE, 0xAD, 0xBE, 0xEF]
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>(
            "a start fact carrying a foreign content type must be rejected, not committed");
        (await store.LoadTailAsync(
                new WorkflowStreamId(instanceId),
                StreamVersion.Empty,
                CancellationToken.None))
            .OfType<WorkflowStartedEvent>()
            .Should().BeEmpty("no foreign-codec start fact may reach the durable stream");
    }

    private static DurableWorkflowRuntime CreateRuntime(InMemoryWorkflowProvider store) =>
        new(new DurableCommandProcessor(store),
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: store);

    private static void RegisterDefinition(
        DurableWorkflowRuntime runtime,
        object publicDefinition,
        Type stateType)
    {
        var runtimeDefinition = publicDefinition.GetType()
            .GetProperty("RuntimeDefinition", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;
        typeof(DurableWorkflowRuntime)
            .GetMethod(nameof(DurableWorkflowRuntime.RegisterDefinition))!
            .MakeGenericMethod(stateType)
            .Invoke(runtime, [runtimeDefinition]);
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
