using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Versioning;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Versioning;

public sealed class DurableVersioningTests
{
    [Fact]
    [Trait("AC", "AC-306")]
    public async Task StartedInstance_RemainsBoundToOriginalDefinitionVersion()
    {
        var definitionId = DefinitionId.New();
        var version = new DefinitionVersion(3);
        var store = new InMemoryWorkflowProvider();
        var starter = new DurableStartService(new DurableCommandProcessor(store));

        var result = await starter.StartOrGetAsync(
            Request("order-1", definitionId, version, "input-v1"),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(result.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        var started = events.OfType<WorkflowStartedEvent>().Single();
        started.DefinitionId.Should().Be(definitionId);
        started.DefinitionVersion.Should().Be(version);
    }

    [Fact]
    [Trait("AC", "AC-307")]
    public void IncompatibleDefinitionChange_FailsWithExplicitDiagnostic()
    {
        var act = () => DurableVersionCompatibility.EnsureCompatible(
            DefinitionId.New(),
            DefinitionVersion.Initial,
            DefinitionId.New(),
            new DefinitionVersion(2));

        act.Should().Throw<WorkflowVersionException>()
            .Which.Message.Should().Contain("incompatible")
            .And.Contain("existing")
            .And.Contain("requested");
    }

    [Fact]
    [Trait("AC", "AC-311")]
    public async Task StartOrGet_SameKey_ReturnsExistingInstance()
    {
        var definitionId = DefinitionId.New();
        var store = new InMemoryWorkflowProvider();
        var starter = new DurableStartService(new DurableCommandProcessor(store));

        var first = await starter.StartOrGetAsync(
            Request("order-1", definitionId, DefinitionVersion.Initial, "input-v1"),
            TestContext.Current.CancellationToken);
        var second = await starter.StartOrGetAsync(
            Request("order-1", definitionId, DefinitionVersion.Initial, "input-v1"),
            TestContext.Current.CancellationToken);

        second.InstanceId.Should().Be(first.InstanceId);
        first.Created.Should().BeTrue();
        second.Created.Should().BeFalse();
    }

    [Fact]
    public async Task StartOrGet_SameKeyDifferentInput_ReturnsExistingWithoutDuplicate()
    {
        var definitionId = DefinitionId.New();
        var store = new InMemoryWorkflowProvider();
        var starter = new DurableStartService(new DurableCommandProcessor(store));

        var first = await starter.StartOrGetAsync(
            Request("order-1", definitionId, DefinitionVersion.Initial, "input-v1"),
            TestContext.Current.CancellationToken);
        var second = await starter.StartOrGetAsync(
            Request("order-1", definitionId, DefinitionVersion.Initial, "input-v2"),
            TestContext.Current.CancellationToken);
        var events = await store.LoadTailAsync(
            new WorkflowStreamId(first.InstanceId),
            StreamVersion.Empty,
            TestContext.Current.CancellationToken);

        second.InstanceId.Should().Be(first.InstanceId);
        second.Created.Should().BeFalse();
        events.OfType<WorkflowStartedEvent>().Should().ContainSingle();
    }

    private static StartOrGetRequest Request(
        string key,
        DefinitionId definitionId,
        DefinitionVersion version,
        object input)
    {
        return new StartOrGetRequest(
            key,
            definitionId,
            version,
            new Abstractions.Providers.SerializedPayload(
                global::OrcaCore.Engine.Durable.Execution.JsonWorkflowPayloadSerializer.JsonContentType,
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(input)),
            new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
    }
}
