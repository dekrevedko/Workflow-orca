using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Hosting;

public static class Cases
{
    public static readonly string[] Ids = Enumerable.Range(1, 16).Select(n => $"AOT-{n:00}").ToArray();
    public static readonly string[] Measured = ["AOT-01", "AOT-02", "AOT-03", "AOT-08", "AOT-09", "AOT-10", "AOT-11", "AOT-15", "AOT-16"];
    public static Task Run(string id) => id switch
    {
        "AOT-01" => Reference(false), "AOT-02" => Value(), "AOT-03" => Reference(true),
        "AOT-04" => WaitCancellation(), "AOT-05" => WorkflowCancellation(),
        "AOT-06" => Drain(), "AOT-07" => Incomplete(), "AOT-08" => Retry(),
        "AOT-09" => Parallel(), "AOT-10" => ForEach(false), "AOT-11" => ForEach(true),
        "AOT-12" => UnsupportedConverter(), "AOT-13" => CyclicInput(), "AOT-14" => ByteLimit(),
        "AOT-15" => StrongValues(), "AOT-16" => Roots(),
        _ => throw new ArgumentException("Unknown scenario: " + id)
    };
    public static EphemeralWorkflowDefinition<string, Data> ReferenceDefinition(bool delayed = false)
    {
        Probe.Phase = "DefinitionBuild";
        var root = Workflow.Ephemeral<Data>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(text => new Data { Text = text, Values = [1, 2] });
        if (delayed) root.Delay(TimeSpan.FromSeconds(10));
        return root.Then<ReferenceStep>().End(state => state.Value, WorkflowOutcomeName.Create("done")).Build();
    }
    private static async Task Reference(bool detached)
    {
        var session = new Session();
        await using var owner = new Owner(session);
        var definition = ReferenceDefinition();
        Probe.Phase = "Register";
        var registered = owner.Registry.Register(definition).GetHandleOrThrow();
        Probe.Phase = "Start";
        var instance = (await registered.StartOrGetAsync("reference", StartIdempotencyKey.Create("reference"))).GetHandleOrThrow();
        Probe.Phase = "Observe";
        var output = await instance.WaitForOutputAsync();
        Probe.Check(output.Text == "reference!" && output.Values.SequenceEqual([1, 2]), "named DI step and typed DTO output");
        Probe.Check(session.DependencyTouches == 1, "the named step used the real DI sentinel");
        Probe.Check((await instance.GetSnapshotAsync()).Status == WorkflowInstanceStatus.Completed, "reference terminal status");
        if (detached)
        {
            var state = await instance.GetStateAsync<Data>(); state.Values[0] = 100;
            output.Values[1] = 200;
            Probe.Check((await instance.GetStateAsync<Data>()).Values.SequenceEqual([1, 2]), "queried state is detached");
            Probe.Check((await instance.WaitForOutputAsync()).Values.SequenceEqual([1, 2]), "output observations are detached");
        }
    }
    private static async Task Value()
    {
        await using var owner = new Owner(new Session());
        Probe.Phase = "DefinitionBuild";
        var definition = Workflow.Ephemeral<ValueState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(input => new ValueState(input)).Then<ValueStep>()
            .End(state => state.Value.Number, WorkflowOutcomeName.Create("value")).Build();
        Probe.Phase = "Register";
        var handle = owner.Registry.Register(definition).GetHandleOrThrow();
        Probe.Phase = "Start";
        var instance = (await handle.StartOrGetAsync(7, StartIdempotencyKey.Create("value"))).GetHandleOrThrow();
        Probe.Phase = "Observe";
        Probe.Check(await instance.WaitForOutputAsync() == 8, "value-type input/state/output and ReplaceState");
        Probe.Check((await instance.GetStateAsync<ValueState>()).Number == 8, "value state committed");
    }
    private static async Task<(WorkflowInstanceHandle<Data> Instance, Task Driver)> BeginBlocked(Owner owner, Session session)
    {
        var handle = owner.Registry.Register(ReferenceDefinition(true)).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("blocked", StartIdempotencyKey.Create("blocked"))).GetHandleOrThrow();
        session.Clock.Advance(TimeSpan.FromSeconds(10));
        await session.Entered.Task.WaitAsync(Probe.Guard);
        return (instance, session.Settled.Task);
    }
    private static async Task WaitCancellation()
    {
        var session = new Session { Block = true };
        await using var owner = new Owner(session);
        var (instance, _) = await BeginBlocked(owner, session);
        Probe.Phase = "WaitCancellation";
        using var cancellation = new CancellationTokenSource();
        var first = instance.WaitForOutputAsync(cancellation.Token).AsTask();
        var second = instance.WaitForOutputAsync().AsTask();
        cancellation.Cancel();
        Probe.Check(await Probe.Error(async () => await first) is OperationCanceledException, "waiter cancellation is local");
        Probe.Check(!second.IsCompleted && !session.CancellationObserved.Task.IsCompleted, "another waiter and workflow remain active");
        session.Release.TrySetResult();
        Probe.Check((await second.WaitAsync(Probe.Guard)).Text == "blocked!", "remaining observer receives output");
    }
    private static async Task WorkflowCancellation()
    {
        var session = new Session { Block = true };
        await using var owner = new Owner(session);
        var (instance, driver) = await BeginBlocked(owner, session);
        Probe.Phase = "WorkflowCancellation";
        var request = instance.RequestCancellationAsync().AsTask();
        await session.CancellationObserved.Task.WaitAsync(Probe.Guard);
        session.Release.TrySetResult(); await driver.WaitAsync(Probe.Guard);
        Probe.Check(await request.WaitAsync(Probe.Guard) == WorkflowCancellationRequestStatus.Requested, "public cancellation admitted");
        Probe.Check(await Probe.Error(async () => await instance.WaitForOutputAsync()) is WorkflowOutputUnavailableException,
            "cancelled workflow has no successful output");
        Probe.Check((await instance.GetSnapshotAsync()).Status == WorkflowInstanceStatus.Cancelled, "public cancelled terminal status");
        await owner.Join();
        Probe.Check(session.CancellationObserved.Task.IsCompleted && session.BodySettled, "asynchronous token notification and physical settlement");
    }
    private static async Task Drain()
    {
        var session = new Session { Block = true };
        await using var owner = new Owner(session);
        var handle = owner.Registry.Register(ReferenceDefinition()).GetHandleOrThrow();
        var pending = handle.StartOrGetAsync("draining", StartIdempotencyKey.Create("draining")).AsTask();
        await session.Entered.Task.WaitAsync(Probe.Guard);
        Probe.Phase = "ClosedDrain";
        owner.Close();
        Probe.Check(!session.CancellationObserved.Task.IsCompleted, "Closed does not cancel initialized starts");
        Probe.Check(await Probe.Error(async () => await handle.StartOrGetAsync("new", StartIdempotencyKey.Create("new"))) is EphemeralHostStoppingException,
            "Closed refuses a new start");
        session.Release.TrySetResult();
        var accepted = (await pending.WaitAsync(Probe.Guard)).GetHandleOrThrow();
        Probe.Check((await accepted.WaitForOutputAsync()).Text == "draining!", "initialized pending start drains to Accepted");
        Probe.Check(owner.Alive, "dependency owners survive drain before Stopping");
    }
    private static async Task Incomplete()
    {
        var session = new Session { Block = true };
        await using var owner = new Owner(session);
        await BeginBlocked(owner, session);
        Probe.Phase = "Incomplete";
        Probe.Check(await owner.Lifetime.StopAsync(TimeSpan.Zero) is EphemeralHostShutdownResult.Incomplete { PendingWorkCount: > 0 }, "zero-budget stop reports physical work");
        Probe.Check(owner.Alive && !session.BodySettled, "Incomplete retains root, scope and external owner");
        session.Release.TrySetResult();
        await owner.Join();
        Probe.Check(session.BodySettled && owner.Alive, "Completed follows real settlement before disposal");
    }
    private static async Task Retry()
    {
        var session = new Session { Retry = true };
        await using var owner = new Owner(session);
        Probe.Phase = "DefinitionBuild";
        var definition = Workflow.Ephemeral<Data>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(text => new Data { Text = text }).Then<ReferenceStep>().WithRetry(2)
            .End(state => state.Value.Text, WorkflowOutcomeName.Create("retried")).Build();
        var handle = owner.Registry.Register(definition).GetHandleOrThrow();
        Probe.Phase = "Retry";
        var instance = (await handle.StartOrGetAsync("retry", StartIdempotencyKey.Create("retry"))).GetHandleOrThrow();
        Probe.Check(await instance.WaitForOutputAsync() == "retry!", "eligible retry completes");
        var attempts = session.Attempts.ToArray();
        Probe.Check(attempts.Select(a => a.Attempt).SequenceEqual([1, 2]), "attempt numbers are one then two");
        Probe.Check(attempts[0].Operation == attempts[1].Operation, "StepOperationId is stable across retry");
    }
    private static async Task Parallel()
    {
        var session = new Session();
        await using var owner = new Owner(session);
        Probe.Phase = "DefinitionBuild";
        var definition = Workflow.Ephemeral<FanState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new FanState()).Delay(TimeSpan.FromSeconds(10))
            .Parallel<int>(branches => branches
                .Branch<ItemState>(AuthoredBranchId.Create("a"), _ => new ItemState(0, 10), b => b.Then<ItemStep>().Delay(TimeSpan.FromSeconds(20)).Then<ItemFinishStep>().Return(s => s.Value.Number))
                .Branch<ItemState>(AuthoredBranchId.Create("b"), _ => new ItemState(1, 20), b => b.Then<ItemStep>().Delay(TimeSpan.FromSeconds(10)).Then<ItemFinishStep>().Return(s => s.Value.Number)))
            .WhenAll((parent, values) => new FanState { Values = values.Select(v => v.Result).ToArray(), Merges = parent.Value.Merges + 1 })
            .End(s => s.Value.Values, WorkflowOutcomeName.Create("parallel")).Build();
        var handle = owner.Registry.Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("fan", StartIdempotencyKey.Create("parallel"))).GetHandleOrThrow();
        session.Clock.Advance(TimeSpan.FromSeconds(10));
        await session.TwoEntered.Task.WaitAsync(Probe.Guard);
        await session.Clock.WaitForDue(TimeSpan.FromSeconds(20)).WaitAsync(Probe.Guard);
        Probe.Phase = "Parallel";
        Probe.Check(session.ItemsStarted.ToArray().SequenceEqual([0, 1]), "both cooperative branches admitted in authored order");
        var output = instance.WaitForOutputAsync().AsTask();
        session.Clock.Advance(TimeSpan.FromSeconds(10));
        await session.ItemFinished[1].Task.WaitAsync(Probe.Guard);
        Probe.Check(!output.IsCompleted, "earlier second-branch deadline cannot finish the first branch");
        await session.Clock.Drive(output).WaitAsync(Probe.Guard);
        Probe.Check((await output).SequenceEqual([10, 20]), "branch output remains authored order with reverse deadlines");
        Probe.Check(session.ItemsFinished.ToArray().SequenceEqual([1, 0]), "named branch tail steps run in the forced reverse order");
        Probe.Check((await instance.GetStateAsync<FanState>()).Merges == 1, "one replacement-state merge");
    }
    private static async Task ForEach(bool outcomes)
    {
        await ForEachBounds(outcomes, hostBound: 3, nodeBound: 2);
        if (!outcomes) await ForEachBounds(false, hostBound: 2, nodeBound: 3);
    }
    private static async Task ForEachBounds(bool outcomes, int hostBound, int nodeBound)
    {
        var session = new Session { FailSecond = outcomes };
        await using var owner = new Owner(session, pathCeiling: hostBound);
        Probe.Phase = "DefinitionBuild";
        var root = Workflow.Ephemeral<FanState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new FanState()).Delay(TimeSpan.FromSeconds(10))
            .ForEach<int, ItemState, int>(_ => new[] { 10, 20, 30 }, ForEachOptions.Create(3, nodeBound),
                item => new ItemState(item.Index, item.Item), body => body.Then<ItemStep>().Delay(TimeSpan.FromSeconds(20)).Then<ItemFinishStep>().Return(s => s.Value.Number));
        var definition = outcomes
            ? root.WhenAllOutcomes((parent, values) => new FanState
                {
                    Values = values.Select(v => v is ForEachItemOutcome<int>.Succeeded ok ? ok.Result : -1).ToArray(),
                    FailureIndex = values.OfType<ForEachItemOutcome<int>.Failed>().Single().Failure.Occurrence is FailureOccurrence.Item item ? item.Index : -2,
                    Merges = parent.Value.Merges + 1
                }).End(s => s.Value.Values, WorkflowOutcomeName.Create("outcomes")).Build()
            : root.WhenAll((parent, values) => new FanState { Values = values.Select(v => v.Result).ToArray(), Merges = parent.Value.Merges + 1 })
                .End(s => s.Value.Values, WorkflowOutcomeName.Create("items")).Build();
        var handle = owner.Registry.Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("fan", StartIdempotencyKey.Create("items"))).GetHandleOrThrow();
        session.Clock.Advance(TimeSpan.FromSeconds(10));
        await session.TwoEntered.Task.WaitAsync(Probe.Guard);
        Probe.Phase = "ForEach";
        Probe.Check(session.ItemsStarted.ToArray().SequenceEqual([0, 1]) && !session.ItemEntered[2].Task.IsCompleted, "two parked items occupy lower node/host bound before third admission");
        Probe.Observations.Add($"host bound {hostBound}, node bound {nodeBound}: only items 0 and 1 admitted before clock release");
        var output = instance.WaitForOutputAsync().AsTask();
        await session.Clock.Drive(output).WaitAsync(Probe.Guard);
        Probe.Check(session.ItemsStarted.ToArray().SequenceEqual([0, 1, 2]), "third item follows capacity release in index order");
        Probe.Check((await output).SequenceEqual(outcomes ? [10, -1, 30] : [10, 20, 30]), "ordered typed outcomes");
        var state = await instance.GetStateAsync<FanState>();
        Probe.Check(state.Merges == 1, "one state merge");
        if (outcomes) Probe.Check(state.FailureIndex == 1, "failure has typed item occurrence index");
    }
    private static async Task UnsupportedConverter()
    {
        await using var owner = new Owner(new Session());
        Probe.Phase = "UnsupportedDeclaredConverter";
        var error = await Probe.Error(() =>
        {
            var definition = Workflow.Ephemeral<Customized>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<string>(_ => new Customized()).End().Build();
            owner.Registry.Register(definition).GetHandleOrThrow();
            return Task.CompletedTask;
        });
        Probe.Observations.Add(error?.ToString() ?? "no exception");
        Probe.Check(error is WorkflowDefinitionException { Code: "WF-DEFINITION-INVALID" } invalid
            && invalid.Diagnostics.Any(diagnostic => diagnostic.Code == "SFE-TYPE-002"), "declared app converter has the exact existing schema-rejection diagnostic");
    }
    private static async Task CyclicInput()
    {
        await using var owner = new Owner(new Session());
        Probe.Phase = "DefinitionBuild";
        var definition = Workflow.Ephemeral<Data>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Link>(input => new Data { Text = input.Text }).Then<ReferenceStep>()
            .End(s => s.Value.Text, WorkflowOutcomeName.Create("bad-input")).Build();
        var handle = owner.Registry.Register(definition).GetHandleOrThrow();
        var input = new Link { Text = "cycle" }; input.Next = input;
        Probe.Phase = "InputBoundary";
        var error = await Probe.Error(async () => await handle.StartOrGetAsync(input, StartIdempotencyKey.Create("bad-input")));
        Probe.Observations.Add(error?.ToString() ?? "no exception");
        Probe.Check(error is JsonException, "public fixed-codec JsonException at input boundary");
        Probe.Check(error!.Message.Contains("Cyclic payload graph", StringComparison.Ordinal), "specific cycle cause retained");
    }
    private static async Task ByteLimit()
    {
        var session = new Session();
        await using var owner = new Owner(session);
        Probe.Phase = "DefinitionBuild";
        var definition = Workflow.Ephemeral<Data>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new Data { Text = input })
            .ForEach<string, Data, string>(parent => new[] { parent.Value.Text }, ForEachOptions.Create(1),
                item => new Data { Text = item.Item }, body => body.Then<ReferenceStep>().Return(s => s.Value.Text))
            .WhenAll((parent, _) => parent.Value)
            .End(s => s.Value.Text, WorkflowOutcomeName.Create("byte-limit")).Build();
        var handle = owner.Registry.Register(definition).GetHandleOrThrow();
        Probe.Phase = "EncodedItemSnapshotBoundary";
        var instance = (await handle.StartOrGetAsync(new string('x', 300 * 1024), StartIdempotencyKey.Create("byte-limit"))).GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync();
        Probe.Check(snapshot.Status == WorkflowInstanceStatus.Failed && snapshot.Failure?.Code == "SFE-LIMIT-011", "fixed 256 KiB item snapshot limit has the existing public failure code");
        Probe.Check(snapshot.Failure!.Message.Contains("262144", StringComparison.Ordinal), "exact byte-limit cause retained");
        Probe.Check(session.DependencyTouches == 0, "oversized snapshot fails before any item body");
        Probe.Check(await Probe.Error(async () => await instance.WaitForOutputAsync()) is WorkflowOutputUnavailableException, "oversized input yields no successful output");
        Probe.Observations.Add(snapshot.Failure.Message);
    }
    private static async Task StrongValues()
    {
        await using var owner = new Owner(new Session());
        Probe.Phase = "DefinitionBuild";
        var original = new StrongData(StartIdempotencyKey.Create("MiXeD / Ж"), WorkflowOutcomeName.Create("Done/λ"));
        Probe.Check(await Probe.Error(() => { StartIdempotencyKey.Create(" padded "); return Task.CompletedTask; }) is ArgumentException,
            "caller-created strong values reject leading/trailing whitespace");
        var definition = Workflow.Ephemeral<StrongData>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<StrongData>(input => input).Then<StrongStep>()
            .End(s => s.Value, WorkflowOutcomeName.Create("strong")).Build();
        var handle = owner.Registry.Register(definition).GetHandleOrThrow();
        Probe.Phase = "StrongValues";
        var instance = (await handle.StartOrGetAsync(original, StartIdempotencyKey.Create("strong"))).GetHandleOrThrow();
        var actual = await instance.WaitForOutputAsync();
        Probe.Check(actual.Key.Equals(original.Key) && actual.Name.Equals(original.Name), "ordinal whitespace/case/non-ASCII strong values round trip");
    }
    private static async Task Roots()
    {
        var first = new Owner(new Session());
        await using var second = new Owner(new Session());
        Probe.Phase = "IndependentRoots";
        await first.DisposeAsync();
        Probe.Check(first.Disposed && second.Alive, "first root disposal leaves second root and scope alive");
        var handle = second.Registry.Register(ReferenceDefinition()).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync("root2", StartIdempotencyKey.Create("root2"))).GetHandleOrThrow();
        Probe.Check((await instance.WaitForOutputAsync()).Text == "root2!", "second root remains independently usable");
        Probe.Check(second.ScopedSharesLifetime, "child scope resolves its root's lifetime");
    }
}

public sealed class Data { public string Text { get; set; } = ""; public int[] Values { get; set; } = []; }
public readonly record struct ValueState(int Number);
public sealed class FanState { public int[] Values { get; set; } = []; public int Merges { get; set; } public int FailureIndex { get; set; } = -1; }
public sealed record ItemState(int Index, int Number);
public sealed record StrongData(StartIdempotencyKey Key, WorkflowOutcomeName Name);
public sealed class Link { public string Text { get; set; } = ""; public Link? Next { get; set; } }

[JsonConverter(typeof(CustomizedConverter))]
public sealed class Customized { }
public sealed class CustomizedConverter : JsonConverter<Customized>
{
    public override Customized Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new();
    public override void Write(Utf8JsonWriter writer, Customized value, JsonSerializerOptions options) => writer.WriteNullValue();
}

public sealed class ReferenceStep(Session session, Sentinel sentinel) : IStep<Data>
{
    public async ValueTask<StepResult> ExecuteAsync(StepContext<Data> context, CancellationToken cancellationToken)
    {
        sentinel.Touch(); session.Scope!.Touch(); session.External.Touch();
        Interlocked.Increment(ref session.DependencyTouches);
        session.Attempts.Enqueue((context.Execution.OperationId.ToString(), context.Execution.AttemptNumber));
        if (session.Retry && context.Execution.AttemptNumber == 1) return new StepResult.Failed(new TransientFailure());
        using var callback = cancellationToken.Register(() => session.CancellationObserved.TrySetResult());
        session.Entered.TrySetResult();
        if (session.Block) await session.Release.Task;
        sentinel.Touch(); session.Scope.Touch(); session.External.Touch();
        context.State.Text += "!"; session.BodySettled = true; session.Settled.TrySetResult();
        return new StepResult.Completed();
    }
}
public sealed class ValueStep : IStep<ValueState>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<ValueState> context, CancellationToken cancellationToken)
    { context.ReplaceState(new(context.State.Number + 1)); return ValueTask.FromResult<StepResult>(new StepResult.Completed()); }
}
public sealed class StrongStep : IStep<StrongData>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<StrongData> context, CancellationToken cancellationToken)
    { context.ReplaceState(context.State); return ValueTask.FromResult<StepResult>(new StepResult.Completed()); }
}
public sealed class ItemStep(Session session) : IStep<ItemState>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<ItemState> context, CancellationToken cancellationToken)
    {
        var index = context.State.Index;
        session.ItemsStarted.Enqueue(index);
        session.ItemEntered[index].TrySetResult();
        if (session.ItemsStarted.Count == 2) session.TwoEntered.TrySetResult();
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
public sealed class ItemFinishStep(Session session) : IStep<ItemState>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<ItemState> context, CancellationToken cancellationToken)
    {
        var index = context.State.Index;
        session.ItemsFinished.Enqueue(index);
        session.ItemFinished[index].TrySetResult();
        return ValueTask.FromResult<StepResult>(session.FailSecond && index == 1
            ? new StepResult.Failed(new TransientFailure()) : new StepResult.Completed());
    }
}
public sealed class TransientFailure() : OrcaCoreException("WF-ASSESSMENT-TRANSIENT", "controlled eligible assessment failure");
