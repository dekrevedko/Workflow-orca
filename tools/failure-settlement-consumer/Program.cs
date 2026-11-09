using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;

var services = new ServiceCollection();
services.AddOrcaCoreInMemoryDurableProvider();
services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
{
    StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
    ResourcePools = new() { PartitionId = ResourceGovernancePartitionId.Create("issue16-consumer"), Pools = [] }
});
var provider = services.BuildServiceProvider();
WorkflowInstanceHandle instance;
try
{
    var definition = provider.GetRequiredService<IWorkflowDefinitionRegistry>().Register(
        Workflow.Durable<ConsumerState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => new ConsumerState(value)).End(state => state.Value.Value).Build()).GetHandleOrThrow();
    var accepted = await definition.StartOrGetAsync(42, StartIdempotencyKey.Create("durable-settlement"));
    if (await accepted.WaitForOutputAsync() != 42) throw new InvalidOperationException("Ordinary durable output changed.");
    instance = accepted.GetHandleOrThrow();
    await Unsupported(instance);
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    try { await instance.WaitForSettlementAsync(cancelled.Token); throw new InvalidOperationException("Pre-cancelled token was ignored."); }
    catch (OperationCanceledException error) when (error.CancellationToken == cancelled.Token) { }
}
finally { await provider.DisposeAsync(); }
await Unsupported(instance);
Console.WriteLine("PASS durable-public-settlement: output, unsupported, token-first, disposed-owner");

static async Task Unsupported(WorkflowInstanceHandle handle)
{
    try { await handle.WaitForSettlementAsync(); }
    catch (NotSupportedException) { return; }
    throw new InvalidOperationException("Durable settlement was accepted.");
}
public sealed record ConsumerState(int Value);
