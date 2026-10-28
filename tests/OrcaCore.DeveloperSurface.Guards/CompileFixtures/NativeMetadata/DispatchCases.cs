using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;
using System.Text.Json.Serialization;

internal static class DispatchCases
{
    internal static async Task Run()
    {
        try { _ = StepExecutionThrottle.For<DispatchNotStep>(1); throw new InvalidOperationException("ASSERTION: invalid step type accepted"); }
        catch (ArgumentException error) when (error.ParamName == "TStep") { }
        try { _ = StepExecutionThrottle.For<DispatchNotStep>(0); throw new InvalidOperationException("ASSERTION: invalid capacity accepted"); }
        catch (ArgumentOutOfRangeException error) when (error.ParamName == "maxConcurrency") { }
        var services = new ServiceCollection();
        services.AddTransient<DispatchStep>();
        services.AddTransient<InheritedDispatchStep>();
        var inheritedThrottle = GenericThrottle<InheritedDispatchStep>(2);
        Check(inheritedThrottle.StepType == typeof(InheritedDispatchStep) && inheritedThrottle.MaxConcurrency == 2,
            "inherited IStep contracts keep exact named-step throttle identity and capacity");
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [StepExecutionThrottle.For<DispatchStep>(1), inheritedThrottle] },
            TransientPools = []
        });
        WorkflowJsonMetadata.Register<DispatchContext>();
        await using var provider = services.BuildServiceProvider();
        try
        {
            var definition = Workflow.Ephemeral<DispatchValue>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<int?>(input => new DispatchValue(input ?? 3, DispatchMode.Ready))
                .Then<DispatchStep>()
                .Then(context => { context.ReplaceState(context.State with { Number = context.State.Number + 2 }); return ValueTask.CompletedTask; })
                .Parallel<int?>(branches => branches
                    .Branch<DispatchValue>(AuthoredBranchId.Create("first"), parent => parent.Value,
                        branch => branch.Then<DispatchStep>().Return(value => (int?)value.Value.Number))
                    .Branch<DispatchValue>(AuthoredBranchId.Create("second"), parent => parent.Value,
                        branch => branch.Return(_ => (int?)null)))
                .WhenAll((parent, results) =>
                {
                    Check(results.Count == 2 && results[0].Result == 7 && results[1].Result is null,
                        "typed nullable results preserve authored order and null");
                    return parent.Value with { Number = results[0].Result!.Value };
                })
                .End(state => state.Value.Mode).Build();
            var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
            var handle = registry.Register(definition).GetHandleOrThrow();
            var instance = (await handle.StartOrGetAsync(null, StartIdempotencyKey.Create("typed"))).GetHandleOrThrow();
            Check(await instance.WaitForOutputAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)) == DispatchMode.Ready,
                "typed enum output remains available");
            Check((await instance.GetStateAsync<DispatchValue>()).Number == 7, "struct state query preserves typed mutations");
            Check(await handle.StartOrGetAsync(null, StartIdempotencyKey.Create("typed")) is WorkflowStartResult<WorkflowInstanceHandle<DispatchMode>>.Accepted { WasExisting: true },
                "typed start replay remains idempotent");
            var itemsDefinition = Workflow.Ephemeral<DispatchValue>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<int>(number => new DispatchValue(number, DispatchMode.Ready))
                .ForEach<int, DispatchValue, int>(_ => [1, 2], ForEachOptions.Create(2, 2),
                    item => new DispatchValue(item.Item, DispatchMode.Ready),
                    body => body.Then(context =>
                    {
                        Check(context.ForEachItem is not null, "typed item context remains attached");
                        context.ReplaceState(context.State with { Number = context.State.Number + context.ForEachItem!.Index });
                        return ValueTask.CompletedTask;
                    }).Return(value => value.Value.Number))
                .WhenAll((parent, outcomes) => parent.Value with { Number = parent.Value.Number + outcomes.Sum(item => item.Result) })
                .End(value => value.Value.Number).Build();
            var items = (await registry.Register(itemsDefinition).GetHandleOrThrow()
                .StartOrGetAsync(7, StartIdempotencyKey.Create("items"))).GetHandleOrThrow();
            Check(await items.WaitForOutputAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)) == 11,
                "typed item snapshot, projection, index and ordered outcome merge preserve values");
            var inheritedDefinition = Workflow.Ephemeral<DispatchValue>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<int>(number => new DispatchValue(number, DispatchMode.Ready))
                .Then<InheritedDispatchStep>().End(value => value.Value.Number).Build();
            var inherited = (await registry.Register(inheritedDefinition).GetHandleOrThrow()
                .StartOrGetAsync(40, StartIdempotencyKey.Create("inherited"))).GetHandleOrThrow();
            Check(await inherited.WaitForOutputAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15)) == 43,
                "a named step inherited from an IStep base executes through the exact throttle");
            Console.WriteLine("DISPATCH dynamic=" + System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported +
                ";jsonReflection=" + System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault);
        }
        finally
        {
            var result = await provider.GetRequiredService<IEphemeralWorkflowHostLifetime>()
                .StopAsync(Timeout.InfiniteTimeSpan).AsTask().WaitAsync(TimeSpan.FromSeconds(15));
            Check(result is EphemeralHostShutdownResult.Completed, "typed dispatch work physically joins before disposal");
        }
    }

    internal static void MissingArray()
    {
        new ServiceCollection().AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            TransientPools = []
        });
        WorkflowJsonMetadata.Register<DispatchMissingArrayContext>();
        var effects = 0;
        var attempted = Workflow.Ephemeral<DispatchValue>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(number => { effects++; return new DispatchValue(number, DispatchMode.Ready); })
            .ForEach<int, DispatchValue, int>(_ => [1], ForEachOptions.Create(1),
                item => { effects++; return new DispatchValue(item.Item, DispatchMode.Ready); },
                body => body.Return(value => value.Value.Number))
            .WhenAll((parent, _) => parent.Value).End(value => value.Value.Number).TryBuild();
        Check(!attempted.IsValid && attempted.Diagnostics.Any(diagnostic =>
            diagnostic.Code == "SFE-TYPE-003" && diagnostic.Message.Contains("System.Int32[]", StringComparison.Ordinal)),
            "missing internal item-array metadata rejects the declared closure at TryBuild");
        Check(effects == 0, "missing array metadata never invokes initialization or item projection");
    }

    private static StepExecutionThrottle GenericThrottle<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] TStep>(int limit) =>
        StepExecutionThrottle.For<TStep>(limit);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("ASSERTION: " + message);
    }
}

public enum DispatchMode { None, Ready }
public readonly record struct DispatchValue(int Number, DispatchMode Mode);
public sealed class DispatchStep : IStep<DispatchValue>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<DispatchValue> context, CancellationToken cancellationToken)
    {
        context.ReplaceState(context.State with { Number = context.State.Number + 1 });
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DispatchValue))]
[JsonSerializable(typeof(int?))]
[JsonSerializable(typeof(int[]))]
[JsonSerializable(typeof(DispatchMode))]
public partial class DispatchContext : JsonSerializerContext;

public sealed class DispatchNotStep;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(DispatchValue))]
public partial class DispatchMissingArrayContext : JsonSerializerContext;

public class DispatchStepBase : IStep<DispatchValue>
{
    public ValueTask<StepResult> ExecuteAsync(StepContext<DispatchValue> context, CancellationToken cancellationToken)
    {
        context.ReplaceState(context.State with { Number = context.State.Number + 3 });
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
public sealed class InheritedDispatchStep : DispatchStepBase;
