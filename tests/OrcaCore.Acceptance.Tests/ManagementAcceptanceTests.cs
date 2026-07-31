using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ManagementAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-009")]
    public async Task DetachedStateQuery_DoesNotLeakTheLiveWorkflowInstance()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var definition = global::OrcaCore.Workflow
            .Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new TestState { Name = input })
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
                "original",
                StartIdempotencyKey.Create("detached-state-query"),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();

        var detached = await instance.GetStateAsync<TestState>(
            TestContext.Current.CancellationToken);
        detached.Name = "mutated";
        var reread = await instance.GetStateAsync<TestState>(
            TestContext.Current.CancellationToken);

        reread.Name.Should().Be("original");
    }

    public sealed class TestState
    {
        public string Name { get; set; } = string.Empty;
    }
}
