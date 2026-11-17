using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Dag;
using OrcaCore.Durable.Hosting.Dag;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DagFacadeTests
{
    [Fact]
    public async Task IncompatibleHost_ConsumesCancellationCommittedByCompatibleHost()
    {
        using var deadline = Deadline(90);
        var token = deadline.Token;
        var oldType = CreateDtoType(false);
        var newType = CreateDtoType(true);
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).Then<Gate>().End(value => value.Value).Build();
        var identity = DefinitionId.New();
        using var first = CreateHost(1, services => services.AddSingleton(new Gate(Task.Delay(Timeout.Infinite, token))), child);
        await first.StartAsync(token);
        var factory = typeof(DagFacadeTests).GetMethod(nameof(RegisterDtoPlan), BindingFlags.Static | BindingFlags.NonPublic)!;
        var a = (DtoPlanAccess)factory.MakeGenericMethod(oldType).Invoke(null, [first.Services, identity, child])!;
        var input = Activator.CreateInstance(oldType)!;
        oldType.GetProperty("Value")!.SetValue(input, 17);
        var run = await a.Start(input, token);
        await UntilAsync(run, snapshot => snapshot.Nodes[0].Status == DagNodeStatus.Running, token);
        var shared = first.Services.GetRequiredService<IWorkflowEventStore>();
        await StopAsync(first, dispose: false);
        using var second = CreateHost(1, services =>
        {
            services.AddSingleton(shared.GetType(), shared);
            services.AddSingleton(new Gate(Task.Delay(Timeout.Infinite, token)));
        }, child);
        var b = (DtoPlanAccess)factory.MakeGenericMethod(newType).Invoke(null, [second.Services, identity, child])!;
        (await Record.ExceptionAsync(() => b.Reopen(run.RunId, token).AsTask())).Should().BeOfType<DagRunInputContractMismatchException>();
        // The request commits on the compatible host before the incompatible host's pumps start.
        (await run.RequestCancellationAsync(token)).Should().Be(DagCancellationRequestStatus.Requested);
        await second.StartAsync(token);
        try
        {
            (await run.WaitForTerminalAsync(token)).Status.Should().Be(DagRunStatus.Cancelled);
            var facts = (await second.Services.GetRequiredService<DurableDagRunStore>().ReadAsync(run.RunId.Value, token))!;
            facts.Nodes[0].State.Should().Be(DagNodeStateV1.Cancelled);
            facts.Nodes.Should().OnlyContain(node => node.FailureCode == null);
        }
        finally { await StopAsync(second); }
    }

    private sealed record DtoPlanAccess(Func<object, CancellationToken, ValueTask<DagRunHandle>> Start,
        Func<DagRunId, CancellationToken, ValueTask<DagRunHandle>> Reopen);

    private static DtoPlanAccess RegisterDtoPlan<T>(IServiceProvider services, DefinitionId id, DurableWorkflowDefinition<int, int> child)
    {
        var dag = global::OrcaCore.Dag.Dag.Define<T>(id, DefinitionVersion.Initial);
        dag.Node(DagNodeId.Create("only"), child.Reference).MapInput(value => (int)typeof(T).GetProperty("Value")!.GetValue(value.RunInput)!);
        var definition = services.GetRequiredService<IDagDefinitionRegistry>().Register(dag.Build()).GetHandleOrThrow();
        return new(async (input, token) => (await definition.StartOrGetAsync((T)input,
                StartIdempotencyKey.Create("cross-host-cancel"), token)).GetHandleOrThrow(),
            (run, token) => definition.GetRunAsync(run, token));
    }

    [Fact]
    public async Task SnapshotsOutputsAndCancellation_UseAuthoredNodesAndDetachedPublicValues()
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value + 1).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var first = dag.Node(DagNodeId.Create("z-first"), child.Reference).MapInput(value => value.RunInput);
        var second = dag.Node(DagNodeId.Create("a-second"), child.Reference).DependsOn(first).MapInput(value => value.OutputOf(first));
        using var host = CreateHost(1, _ => { }, child);
        var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(dag.Build()).GetHandleOrThrow();
        var run = (await definition.StartOrGetAsync(17, StartIdempotencyKey.Create("snapshots"), token)).GetHandleOrThrow();
        var before = await run.GetSnapshotAsync(token);
        before.Nodes.Select(node => node.NodeId.Value).Should().Equal("z-first", "a-second");
        before.Nodes.Select(node => node.Status).Should().Equal(DagNodeStatus.Ready, DagNodeStatus.Pending);
        before.Nodes[1].Dependencies.Should().Equal(first.NodeId);
        (await run.GetOutputAsync(first, token)).Should().BeOfType<DagNodeOutputResult<int>.Unavailable>().Which.Status.Should().Be(DagNodeStatus.Ready);
        var foreign = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Node(DagNodeId.Create("z-first"), child.Reference).MapInput(value => value.RunInput);
        (await Record.ExceptionAsync(() => run.GetOutputAsync(foreign, token).AsTask())).Should().BeOfType<ArgumentException>();
        await host.StartAsync(token);
        try
        {
            (await run.WaitForTerminalAsync(token)).Status.Should().Be(DagRunStatus.Succeeded);
            (await run.GetOutputAsync(second, token)).Should().BeOfType<DagNodeOutputResult<int>.Available>().Which.Output.Should().Be(19);
            before.Nodes.Select(node => node.Status).Should().Equal([DagNodeStatus.Ready, DagNodeStatus.Pending], "snapshots are detached");
            (await run.RequestCancellationAsync(token)).Should().Be(DagCancellationRequestStatus.AlreadyTerminal);
        }
        finally { await StopAsync(host); }
    }

    [Fact]
    public async Task MappingFailure_IsProjectedWithBlockedDependents()
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value).End(value => value.Value).Build();
        var dag = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial);
        var first = dag.Node(DagNodeId.Create("first"), child.Reference).MapInput(_ => throw new InvalidOperationException("private mapper details"));
        var next = dag.Node(DagNodeId.Create("next"), child.Reference).DependsOn(first).MapInput(value => value.RunInput);
        using var host = CreateHost(1, _ => { }, child);
        await host.StartAsync(token);
        try
        {
            var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>().Register(dag.Build()).GetHandleOrThrow();
            var run = (await definition.StartOrGetAsync(17, StartIdempotencyKey.Create("mapping-failure"), token)).GetHandleOrThrow();
            var terminal = await run.WaitForTerminalAsync(token);
            terminal.Status.Should().Be(DagRunStatus.Failed);
            terminal.Nodes.Select(node => node.Failure!.Code).Should().Equal("DAG_INPUT_MAPPING_INVALID", "DEPENDENCY_BLOCKED");
            terminal.Nodes.Select(node => node.Failure!.Message).Should().NotContain(message => message.Contains("private"));
            (await run.GetOutputAsync(next, token)).Should().BeOfType<DagNodeOutputResult<int>.Unavailable>().Which.Failure!.Code.Should().Be("DEPENDENCY_BLOCKED");
        }
        finally { await StopAsync(host); }
    }

    [Fact]
    public async Task RegistrationAndStart_UseDistinctClosedConflicts_AndReopenReportsFullTriple()
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        using var hostA = CreateHost(1, _ => { });
        var id = DefinitionId.New();
        var plan = global::OrcaCore.Dag.Dag.Define<int>(id, DefinitionVersion.Initial).Build();
        var otherType = global::OrcaCore.Dag.Dag.Define<string>(id, DefinitionVersion.Initial).Build();
        plan.DefinitionFingerprint.Should().Be(otherType.DefinitionFingerprint);
        var registryA = hostA.Services.GetRequiredService<IDagDefinitionRegistry>();
        var original = registryA.Register(plan).GetHandleOrThrow();
        var differentType = registryA.Register(otherType).Should().BeOfType<DagRegistrationResult<string>.RunInputConflict>().Subject;
        differentType.ExistingRunInputType.Should().Be(typeof(int));
        differentType.AttemptedRunInputType.Should().Be(typeof(string));
        Action throwRegistration = () => differentType.GetHandleOrThrow();
        throwRegistration.Should().Throw<DagRunInputContractMismatchException>().Which.RunId.Should().BeNull();
        registryA.Register(plan).Should().BeOfType<DagRegistrationResult<int>.Registered>();

        var key = StartIdempotencyKey.Create("binding");
        var run = (await original.StartOrGetAsync(17, key, token)).GetHandleOrThrow();
        var storage = hostA.Services.GetRequiredService<IWorkflowEventStore>();
        using var hostB = CreateHost(1, services => services.AddSingleton(storage.GetType(), storage));
        var registryB = hostB.Services.GetRequiredService<IDagDefinitionRegistry>();
        var foreign = registryB.Register(otherType).GetHandleOrThrow();
        var conflict = (await foreign.StartOrGetAsync("17", key, token)).Should().BeOfType<DagStartResult.RunInputConflict>().Subject;
        conflict.ExistingRunId.Should().Be(run.RunId);
        Action throwStart = () => conflict.GetHandleOrThrow();
        throwStart.Should().Throw<DagRunInputContractMismatchException>().Which.RunId.Should().Be(run.RunId);
        var mismatch = await Record.ExceptionAsync(() => foreign.GetRunAsync(run.RunId, token).AsTask());
        mismatch.Should().BeOfType<DagRunInputContractMismatchException>().Which.RunId.Should().Be(run.RunId);

        var otherDefinition = registryB.Register(global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial).Build()).GetHandleOrThrow();
        var error = (await Record.ExceptionAsync(() => otherDefinition.GetRunAsync(run.RunId, token).AsTask()))
            .Should().BeOfType<DagRunDefinitionMismatchException>().Subject;
        error.ExpectedDefinitionId.Should().Be(otherDefinition.DefinitionId);
        error.ActualDefinitionId.Should().Be(id);
        error.ExpectedDefinitionVersion.Should().Be(otherDefinition.DefinitionVersion);
        error.ActualDefinitionVersion.Should().Be(plan.DefinitionVersion);
        error.ExpectedDefinitionFingerprint.Should().Be(otherDefinition.DefinitionFingerprint);
        error.ActualDefinitionFingerprint.Should().Be(plan.DefinitionFingerprint);
        (await otherDefinition.StartOrGetAsync(17, key, token)).Should().BeOfType<DagStartResult.Conflict>();

        var child = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(value => value).End(value => value.Value).Build();
        var changed = global::OrcaCore.Dag.Dag.Define<int>(id, DefinitionVersion.Initial);
        changed.Node(DagNodeId.Create("new-node"), child.Reference).MapInput(value => value.RunInput);
        registryA.Register(changed.Build()).Should().BeOfType<DagRegistrationResult<int>.Conflict>();
        (await original.GetRunAsync(run.RunId, token)).RunId.Should().Be(run.RunId);
    }

    [Fact]
    public async Task LossyInput_IsRejectedOnReplay_WithoutChangingOwner()
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        using var host = CreateHost(1, _ => { });
        var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>()
            .Register(global::OrcaCore.Dag.Dag.Define<Lossy>(DefinitionId.New(), DefinitionVersion.Initial).Build()).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("lossy-replay");
        var run = (await definition.StartOrGetAsync(Lossy.Create(0), key, token)).GetHandleOrThrow();
        var events = host.Services.GetRequiredService<IWorkflowEventStore>();
        var before = await events.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(run.RunId.Value), StreamVersion.Empty, token);
        (await Record.ExceptionAsync(() => definition.StartOrGetAsync(Lossy.Create(17), key, token).AsTask()))
            .Should().BeOfType<NotSupportedException>();
        (await events.LoadDagRunTailAsync(WorkflowStreamId.ForDagRun(run.RunId.Value), StreamVersion.Empty, token)).Should().BeEquivalentTo(before);
        (await definition.GetRunAsync(run.RunId, token)).RunId.Should().Be(run.RunId);
    }

    [Fact]
    public async Task CompetingCreates_ResolveOneAuthoritativeOwner()
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        var first = new DagEventStoreProbe();
        var second = new DagEventStoreProbe();
        var bothRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reads = 0;
        async Task Hold(CancellationToken ct)
        {
            if (Interlocked.Increment(ref reads) == 2) bothRead.TrySetResult();
            await bothRead.Task.WaitAsync(ct);
        }
        first.AfterKeyLookup = second.AfterKeyLookup = Hold;
        using var hostA = CreateHost(1, services => Decorate(services, first));
        _ = hostA.Services.GetRequiredService<IWorkflowEventStore>();
        using var hostB = CreateHost(1, services =>
        {
            services.AddSingleton(first.Inner.GetType(), first.Inner);
            Decorate(services, second);
        });
        var plan = global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial).Build();
        var a = hostA.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
        var b = hostB.Services.GetRequiredService<IDagDefinitionRegistry>().Register(plan).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("competing");
        var results = await Task.WhenAll(a.StartOrGetAsync(17, key, token).AsTask(), b.StartOrGetAsync(17, key, token).AsTask());
        results.Should().AllBeOfType<DagStartResult.Accepted>();
        results.Cast<DagStartResult.Accepted>().Select(result => result.WasExisting).Should().BeEquivalentTo([false, true]);
        results.Select(result => result.GetHandleOrThrow().RunId).Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task SameFullNameAndShape_DoesNotMakeDifferentClrTypesCompatible()
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        var oldType = CreateDtoType(dropOnRead: false);
        var newType = CreateDtoType(dropOnRead: true);
        oldType.FullName.Should().Be(newType.FullName);
        oldType.Should().NotBe(newType);
        using var first = CreateHost(1, _ => { });
        var id = DefinitionId.New();
        var method = typeof(DagFacadeTests).GetMethod(nameof(ExerciseDto), BindingFlags.Static | BindingFlags.NonPublic)!;
        var previous = await (Task<DagRunId>)method.MakeGenericMethod(oldType).Invoke(null, [first.Services, id, null, false, token])!;
        await (Task<DagRunId>)method.MakeGenericMethod(newType).Invoke(null, [first.Services, id, previous, true, token])!;
        var shared = first.Services.GetRequiredService<IWorkflowEventStore>();
        using var next = CreateHost(1, services => services.AddSingleton(shared.GetType(), shared));
        await (Task<DagRunId>)method.MakeGenericMethod(newType).Invoke(null, [next.Services, id, previous, false, token])!;
        var oldKey = first.Services.GetRequiredService<DurableDagRunStore>();
        var saved = (await oldKey.ReadAsync(previous.Value, token))!;
        next.Services.GetRequiredService<DurableDagRunInputCatalog>().Resolve(saved.Graph).Should().Be(newType,
            "the digest is equal even though the new type cannot decode losslessly");
    }

    private static async Task<DagRunId> ExerciseDto<T>(IServiceProvider services, DefinitionId id, DagRunId? existing,
        bool localConflict, CancellationToken token)
    {
        var registered = services.GetRequiredService<IDagDefinitionRegistry>().Register(global::OrcaCore.Dag.Dag.Define<T>(id, DefinitionVersion.Initial).Build());
        if (localConflict)
        {
            registered.Should().BeOfType<DagRegistrationResult<T>.RunInputConflict>();
            return existing!;
        }
        var definition = registered.GetHandleOrThrow();
        if (existing is not null)
        {
            (await Record.ExceptionAsync(() => definition.GetRunAsync(existing, token).AsTask()))
                .Should().BeOfType<DagRunInputContractMismatchException>();
            // Equal key with different, independently valid bytes uses ordinary start conflict.
            (await definition.StartOrGetAsync(Activator.CreateInstance<T>(), StartIdempotencyKey.Create("dto"), token))
                .Should().BeOfType<DagStartResult.Conflict>();
            return existing;
        }
        var input = Activator.CreateInstance<T>();
        typeof(T).GetProperty("Value")!.SetValue(input, 17);
        return (await definition.StartOrGetAsync(input, StartIdempotencyKey.Create("dto"), token)).GetHandleOrThrow().RunId;
    }

    private static Type CreateDtoType(bool dropOnRead)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("DagDto" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
        var type = assembly.DefineDynamicModule("dto").DefineType("Deployment.RunInput", TypeAttributes.Public | TypeAttributes.Class);
        type.DefineDefaultConstructor(MethodAttributes.Public);
        var field = type.DefineField("value", typeof(int), FieldAttributes.Private);
        var property = type.DefineProperty("Value", PropertyAttributes.None, typeof(int), null);
        var getter = type.DefineMethod("get_Value", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(int), Type.EmptyTypes);
        var il = getter.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, field); il.Emit(OpCodes.Ret);
        var setter = type.DefineMethod("set_Value", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(void), [typeof(int)]);
        il = setter.GetILGenerator();
        if (!dropOnRead) { il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Stfld, field); }
        il.Emit(OpCodes.Ret); property.SetGetMethod(getter); property.SetSetMethod(setter);
        return type.CreateType()!;
    }

    [Theory]
    [InlineData("key")]
    [InlineData("format")]
    [InlineData("fingerprint")]
    [InlineData("json")]
    public async Task CorruptStoredInput_RemainsAnIntegrityError_OnReopenReplayAndMapping(string corruption)
    {
        using var deadline = Deadline();
        var token = deadline.Token;
        var probe = new DagEventStoreProbe();
        using var host = CreateHost(1, services => Decorate(services, probe));
        var definition = host.Services.GetRequiredService<IDagDefinitionRegistry>()
            .Register(global::OrcaCore.Dag.Dag.Define<int>(DefinitionId.New(), DefinitionVersion.Initial).Build()).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("corruption");
        var run = (await definition.StartOrGetAsync(17, key, token)).GetHandleOrThrow();
        probe.RewriteCheckpoint = checkpoint =>
        {
            var doc = JsonNode.Parse(checkpoint.Payload)!;
            var value = doc["snapshot"]!["runInput"]!;
            if (corruption == "key") value["contractKey"] = "foreign-key";
            if (corruption == "format") value["codecFormat"] = "foreign-format";
            if (corruption == "fingerprint") value["fingerprint"] = new string('0', 64);
            if (corruption == "json")
            {
                var invalid = "{"u8.ToArray();
                value["bytes"] = Convert.ToBase64String(invalid);
                value["fingerprint"] = Convert.ToHexString(SHA256.HashData(invalid));
            }
            var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(doc);
            return checkpoint with { Payload = bytes, Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) };
        };
        (await Record.ExceptionAsync(() => definition.GetRunAsync(run.RunId, token).AsTask())).Should().BeOfType<InvalidDataException>();
        (await Record.ExceptionAsync(() => definition.StartOrGetAsync(17, key, token).AsTask())).Should().BeOfType<InvalidDataException>();
        (await Record.ExceptionAsync(() => host.Services.GetRequiredService<DurableDagRunStore>().ReadAsync(run.RunId.Value, token).AsTask()))
            .Should().BeOfType<InvalidDataException>();
        probe.RewriteCheckpoint = null;
        (await definition.GetRunAsync(run.RunId, token)).RunId.Should().Be(run.RunId);
    }

    [Theory]
    [InlineData("DAG_INPUT_MAPPING_INVALID", "The node input mapping was invalid.")]
    [InlineData("CHILD_FAILED", "The node child workflow failed.")]
    [InlineData("CHILD_TIMED_OUT", "The node child workflow timed out.")]
    [InlineData("CHILD_TERMINATED", "The node child workflow was terminated.")]
    [InlineData("CHILD_CANCELLED", "The node child workflow was cancelled without a run cancellation request.")]
    [InlineData("DEPENDENCY_BLOCKED", "A dependency of the node did not succeed.")]
    public void NodeFailure_HasStableCodeLocationAndRootOccurrence(string code, string message)
    {
        var node = new DagRunNodeStateV1(7, code == "DEPENDENCY_BLOCKED" ? DagNodeStateV1.DependencyBlocked : DagNodeStateV1.Failed,
            null, null, null, code, null, null, null);
        var failure = DurableDagFacade.NodeFailure(node)!;
        failure.Code.Should().Be(code); failure.Message.Should().Be(message);
        failure.AuthoredLocation.ToString().Should().Be("dag:$/dag-node:00000007");
        failure.Occurrence.Should().BeOfType<FailureOccurrence.Root>();
        failure.Causes.Should().BeEmpty(); failure.ExceptionType.Should().BeNull();
        DurableDagFacade.NodeFailure(node with { State = DagNodeStateV1.Cancelled }).Should().BeNull();
    }
}
