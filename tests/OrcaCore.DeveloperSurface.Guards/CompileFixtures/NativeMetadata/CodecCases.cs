using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;

internal static class CodecCases
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);
    private static EphemeralEngineHostOptions Options() => new()
    {
        StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
        TransientPools = []
    };

    internal static async Task<string> Golden()
    {
        var corpus = new CorpusState
        {
            Name = EventName.Create("inner space"), Numbers = [2, 1],
            Map = new() { ["z"] = "last", ["a"] = "first" }
        };
        var fingerprints = new List<string>
        {
            await CheckFingerprint(new Payload { Value = 7 }, new Payload { Value = 8 }, "{\"Value\":7}"),
            await CheckFingerprint(new ValueState(7), new ValueState(8), "{\"Number\":7}"),
            await CheckFingerprint((int?)7, (int?)8, "7"),
            await CheckFingerprint(Mode.Ready, Mode.None, "1"),
            await CheckFingerprint<IReadOnlyList<int>>(new List<int> { 2, 1 }, new List<int> { 1, 2 }, "[2,1]"),
            await CheckFingerprint<IReadOnlyDictionary<string, int>>(
                new Dictionary<string, int> { ["z"] = 2, ["a"] = 1 },
                new Dictionary<string, int> { ["a"] = 1, ["z"] = 2 }, "{\"z\":2,\"a\":1}"),
            await CheckFingerprint(corpus, new CorpusState { Name = EventName.Create("other"), Numbers = [], Map = [] },
                "{\"Name\":\"inner space\",\"Numbers\":[2,1],\"Map\":{\"z\":\"last\",\"a\":\"first\"}}")
        };
        var detached = Event(corpus);
        corpus.Numbers[0] = 99; corpus.Map["z"] = "mutated";
        Check(detached.Numbers.SequenceEqual([2, 1]) && detached.Map["z"] == "last", "nested values remain detached and preserve ordering");
        return string.Join("|", fingerprints);
    }

    internal static async Task<string> CheckFingerprint<T>(T value, T different, string goldenBytes)
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(Options());
        var provider = services.BuildServiceProvider();
        try
        {
            var definition = Workflow.Ephemeral<Payload>(DefinitionId.Parse("b5d94580-4dd8-41f1-b485-033facdf82d8"), DefinitionVersion.Initial)
                .Init<T>(_ => new Payload { Value = 7 }).End(snapshot => snapshot.Value).Build();
            var handle = provider.GetRequiredService<IWorkflowDefinitionRegistry>().Register(definition).GetHandleOrThrow();
            var key = StartIdempotencyKey.Create("golden");
            var instance = (await handle.StartOrGetAsync(value, key)).GetHandleOrThrow();
            Check((await instance.WaitForOutputAsync().AsTask().WaitAsync(Guard)).Value == 7, "typed output remains available");
            var conflict = await handle.StartOrGetAsync(different, key);
            Check(conflict is WorkflowStartResult<WorkflowInstanceHandle<Payload>>.Conflict, "different bytes preserve idempotency conflict");
            var error = ((WorkflowStartResult<WorkflowInstanceHandle<Payload>>.Conflict)conflict).Error;
            var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(goldenBytes)));
            Check(error.ExistingInputFingerprint.Value == expected, "public input fingerprint binds the independent exact byte corpus for " + typeof(T));
            var detachedState = await instance.GetStateAsync<Payload>(); detachedState.Value = 99;
            Check((await instance.GetStateAsync<Payload>()).Value == 7, "state query returns a detached copy");
            return definition.DefinitionFingerprint.Value + "/" + expected;
        }
        finally { await DisposeAfterJoin(provider); }
    }

    internal static async Task Failures()
    {
        var services = new ServiceCollection(); services.AddTransient<NamedFailStep>(); services.AddOrcaCoreEphemeralEngine(Options());
        var provider = services.BuildServiceProvider();
        try
        {
            var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
            var definition = Workflow.Ephemeral<Payload>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<int>(_ => new Payload())
                .Parallel<int>(branches => branches
                    .Branch<Payload>(AuthoredBranchId.Create("first"), _ => new Payload(), branch => branch
                        .Then<NamedFailStep>().Return(_ => 1))
                    .Branch<Payload>(AuthoredBranchId.Create("second"), _ => new Payload(), branch => branch.Then<NamedFailStep>().Return(_ => 2)))
                .WhenAll((parent, _) => parent.Value).End(_ => string.Empty).Build();
            var instance = (await registry.Register(definition).GetHandleOrThrow().StartOrGetAsync(0, StartIdempotencyKey.Create("failure"))).GetHandleOrThrow();
            await ExpectFailedOutput(instance);
            var snapshot = await instance.GetSnapshotAsync();
            Check(snapshot.Status == WorkflowInstanceStatus.Failed && snapshot.Failure is not null && snapshot.Failure.Causes.Count > 0, "join exposes a nested public failure tree: " + snapshot.Status + "/" + snapshot.Failure?.Code + "/" + snapshot.Failure?.Causes.Count + "/" + snapshot.Failure?.Message);
            Check(snapshot.Failure!.Code == "SFE-JOIN-FAILED" && snapshot.Failure.Causes.Count == 2 &&
                snapshot.Failure.Causes.Select(cause => ((FailureOccurrence.Branch)cause.Occurrence).BranchId.Value).SequenceEqual(new[] { "first", "second" }),
                "both authored failures retain their ordered branch provenance");
            var detached = Event(snapshot.Failure!);
            Check(detached.Code == snapshot.Failure!.Code && detached.Causes.Count == snapshot.Failure.Causes.Count &&
                detached.Causes[0].Message == snapshot.Failure.Causes[0].Message && !ReferenceEquals(detached, snapshot.Failure), "product failure metadata preserves detached nested causes");
            Check(!ReferenceEquals(detached.Causes[0], snapshot.Failure.Causes[0]) &&
                detached.Causes[0].AuthoredLocation.Equals(snapshot.Failure.Causes[0].AuthoredLocation) &&
                detached.Causes[0].Occurrence == snapshot.Failure.Causes[0].Occurrence,
                "nested failure metadata preserves provenance without sharing references");
            const string failureJson = """{"code":"SFE-JOIN-FAILED","message":"2 authored children failed.","authoredLocation":"workflow:$/n:00000001","occurrence":{"version":1,"kind":"root"},"causes":[{"code":"WF-LEGACY-LIFECYCLE","message":"nested failure","authoredLocation":"workflow:$/n:00000001/parallel:00000000/n:00000000","occurrence":{"version":1,"kind":"branch","branchId":"first"},"causes":[]},{"code":"WF-LEGACY-LIFECYCLE","message":"nested failure","authoredLocation":"workflow:$/n:00000001/parallel:00000001/n:00000000","occurrence":{"version":1,"kind":"branch","branchId":"second"},"causes":[]}]}""";
            await CheckFingerprint(snapshot.Failure, snapshot.Failure.Causes[0], failureJson);
            var bodies = 0;
            var limit = Workflow.Ephemeral<TextState>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<string>(text => new TextState { Text = text })
                .ForEach<string, TextState, string>(state => new[] { state.Value.Text }, ForEachOptions.Create(1),
                    item => new TextState { Text = item.Item }, body => body
                        .Then(_ => { bodies++; return ValueTask.CompletedTask; }).Return(state => state.Value.Text))
                .WhenAll((parent, _) => parent.Value).End(state => state.Value.Text).Build();
            var limited = (await registry.Register(limit).GetHandleOrThrow().StartOrGetAsync(new string('x', 300 * 1024), StartIdempotencyKey.Create("size"))).GetHandleOrThrow();
            await ExpectFailedOutput(limited);
            var failed = await limited.GetSnapshotAsync();
            Check(failed.Status == WorkflowInstanceStatus.Failed && failed.Failure?.Code == "SFE-LIMIT-011" && bodies == 0,
                "256 KiB item snapshot limit rejects before item body, rather than inventing a root-input size cap: " + failed.Failure?.Code + "/" + failed.Failure?.Message + "/bodies=" + bodies);
        }
        finally { await DisposeAfterJoin(provider); }
    }

    private static async Task ExpectFailedOutput(WorkflowInstanceHandle<string> instance)
    {
        try { await instance.WaitForOutputAsync().AsTask().WaitAsync(Guard); }
        catch (WorkflowOutputUnavailableException) { return; }
        throw new InvalidOperationException("ASSERTION: expected terminal failed output");
    }

    private static T Event<T>(T value) => WorkflowInboundEvent<T>.Create(
        WorkflowEventContract<T>.Create(EventName.Create("golden"), EventContractVersion.Initial),
        EventId.Create("golden"), CorrelationId.Create("golden"), null, DateTimeOffset.UtcNow,
        new WorkflowEventRoute.Correlation(DefinitionId.New()), value).Payload;

    private static async Task DisposeAfterJoin(ServiceProvider provider)
    {
        var result = await provider.GetRequiredService<IEphemeralWorkflowHostLifetime>().StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(Guard);
        Check(result is EphemeralHostShutdownResult.Completed, "physical work joined before disposing the provider");
        await provider.DisposeAsync();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ASSERTION: " + message);
    }
}

public sealed class CorpusState
{
    public EventName Name { get; set; } = null!;
    public List<int> Numbers { get; set; } = [];
    public Dictionary<string, string> Map { get; set; } = [];
}
public sealed class TextState { public string Text { get; set; } = ""; }

public sealed class NamedFailStep : IStep<Payload>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<Payload> context, CancellationToken cancellationToken) =>
        ValueTask.FromResult<StepResult>(new StepResult.Failed(new OrcaCore.Abstractions.Errors.WorkflowLifecycleException("nested failure")));
}
