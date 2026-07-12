using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class WorkflowPayloadSerializationRegistrationTests
{
    [Fact]
    public void AddOrcaCore_UsesContentTypeRouterWithJsonAsDefaultWriter()
    {
        var services = new ServiceCollection();
        services.AddOrcaCore();
        using var provider = services.BuildServiceProvider();

        var serializer = provider.GetRequiredService<IWorkflowPayloadSerializer>();
        var serialized = serializer.Serialize(new TestPayload("value"));

        serializer.Should().BeOfType<ContentTypeWorkflowPayloadSerializer>();
        serialized.ContentType.Should().Be(JsonWorkflowPayloadSerializer.JsonContentType);
        serializer.Deserialize<TestPayload>(serialized).Should().Be(new TestPayload("value"));
    }

    [Fact]
    public void RegisteredCodecAndWriterOption_PlugInWithoutChangingEngineRegistration()
    {
        var services = new ServiceCollection();
        services.AddOrcaCore();
        services.AddSingleton<IWorkflowPayloadCodec, TestBinaryCodec>();
        services.Configure<WorkflowPayloadSerializationOptions>(options =>
            options.WriteContentType = TestBinaryCodec.BinaryContentType);
        using var provider = services.BuildServiceProvider();

        var serializer = provider.GetRequiredService<IWorkflowPayloadSerializer>();
        var binary = serializer.Serialize("new-value");
        var jsonCodec = new JsonWorkflowPayloadSerializer();
        var historicalJson = jsonCodec.Serialize("old-value");

        binary.ContentType.Should().Be(TestBinaryCodec.BinaryContentType);
        serializer.Deserialize<string>(binary).Should().Be("new-value");
        serializer.Deserialize<string>(historicalJson).Should().Be("old-value");
    }

    private sealed record TestPayload(string Value);

    private sealed class TestBinaryCodec : IWorkflowPayloadCodec
    {
        internal const string BinaryContentType = "application/vnd.test+binary";

        public string ContentType => BinaryContentType;

        public SerializedPayload Serialize<TPayload>(TPayload payload) =>
            new(ContentType, Encoding.UTF8.GetBytes(payload?.ToString() ?? string.Empty));

        public TPayload Deserialize<TPayload>(SerializedPayload payload) =>
            (TPayload)(object)Encoding.UTF8.GetString(payload.Payload);
    }
}
