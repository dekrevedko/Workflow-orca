using System.Linq.Expressions;
using System.Reflection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class DurableLeaseLifecycleExpectedRedGuards
{
    [Fact]
    public async Task NormalScopeExit_ReleasesLeaseBeforeWorkflowTerminal()
    {
        var definitionId = DefinitionId.New();
        var definition = Workflow.Durable<DurableBehaviorHarness.GuardState>(definitionId, DefinitionVersion.Initial)
            .Init<object?>(_ => State("normal-scope"))
            .Parallel<int>(
                scope => scope.Branch<DurableBehaviorHarness.GuardState>(
                    "lease-branch",
                    parent => State(parent.Value.Key),
                    branch => AcquireStructuralLease(branch, "normal-scope", TimeSpan.FromMinutes(30))
                        .Return(_ => 1)),
                (parent, _) => parent.Value)
            .Wait("HoldRoot", state => new CorrelationId(state.Key))
            .End("done")
            .Build();
        var host = await CreateLeaseHostAsync(definition);

        var instanceId = await DurableBehaviorHarness.StartAndPumpAsync(
            host,
            definitionId,
            TestContext.Current.CancellationToken);

        (await DurableBehaviorHarness.SnapshotAsync(host, instanceId, TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowStatus.Waiting, "the root remains live after the branch scope exits");
        var events = await DurableBehaviorHarness.TailAsync(host, instanceId, TestContext.Current.CancellationToken);
        events.OfType<WorkflowResourcePoolAcquiredEvent>().Should().ContainSingle();
        events.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle(
            "scope exit releases capacity without waiting for workflow termination");
        (await host.Pools.GetPoolAsync("db", TestContext.Current.CancellationToken)).Value
            .AvailableCapacity.Should().Be(1);
    }

    [Fact]
    public async Task CanceledWhenFirstBranch_ReleasesOwnedLease()
    {
        var definitionId = DefinitionId.New();
        var definition = Workflow.Durable<DurableBehaviorHarness.GuardState>(definitionId, DefinitionVersion.Initial)
            .Init<object?>(_ => State("canceled-branch"))
            .WhenFirst<int>(
                branches => branches
                    .Branch<DurableBehaviorHarness.GuardState>(
                        "lease-loser",
                        parent => State(parent.Value.Key),
                        branch => AcquireStructuralLease(
                                branch,
                                "canceled-branch",
                                TimeSpan.FromMinutes(30))
                            .Wait("Never", state => new CorrelationId(state.Key))
                            .Return(_ => 0))
                    .Branch<DurableBehaviorHarness.GuardState>(
                        "winner",
                        parent => State(parent.Value.Key),
                        branch => branch.Return(_ => 1)),
                (parent, _) => parent.Value)
            .Wait("HoldRoot", state => new CorrelationId(state.Key))
            .End("done")
            .Build();
        var host = await CreateLeaseHostAsync(definition);

        var instanceId = await DurableBehaviorHarness.StartAndPumpAsync(
            host,
            definitionId,
            TestContext.Current.CancellationToken);

        (await DurableBehaviorHarness.SnapshotAsync(host, instanceId, TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowStatus.Waiting);
        var events = await DurableBehaviorHarness.TailAsync(host, instanceId, TestContext.Current.CancellationToken);
        events.OfType<WorkflowResourcePoolAcquiredEvent>().Should().ContainSingle();
        events.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle(
            "WhenFirst cancellation deterministically releases the losing branch lease");
    }

    [Fact]
    public async Task FailedScope_ReleasesOwnedLease()
    {
        var definitionId = DefinitionId.New();
        var definition = Workflow.Durable<DurableBehaviorHarness.GuardState>(definitionId, DefinitionVersion.Initial)
            .Init<object?>(_ => State("failed-scope"))
            .Parallel<int>(
                scope => scope.Branch<DurableBehaviorHarness.GuardState>(
                    "failing-branch",
                    parent => State(parent.Value.Key),
                    branch => AcquireStructuralLease(
                            branch,
                            "failed-scope",
                            TimeSpan.FromMinutes(30))
                        .Then(() => new ExpectedFailureStep())
                        .Return(_ => 0)),
                (parent, _) => parent.Value)
            .End("unreachable")
            .Build();
        var host = await CreateLeaseHostAsync(definition);

        var instanceId = await DurableBehaviorHarness.StartAndPumpAsync(
            host,
            definitionId,
            TestContext.Current.CancellationToken);

        (await DurableBehaviorHarness.SnapshotAsync(host, instanceId, TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowStatus.Failed);
        var events = await DurableBehaviorHarness.TailAsync(host, instanceId, TestContext.Current.CancellationToken);
        events.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle(
            "scope failure deterministically releases the lease");
    }

    [Fact]
    public async Task WorkflowTerminalTransition_ReleasesRootLease()
    {
        var definitionId = DefinitionId.New();
        var builder = Workflow.Durable<DurableBehaviorHarness.GuardState>(definitionId, DefinitionVersion.Initial)
            .Init<object?>(_ => State("terminal"));
        var definition = AcquireStructuralLease(builder, "terminal", TimeSpan.FromMinutes(30))
            .End("done")
            .Build();
        var host = await CreateLeaseHostAsync(definition);

        var instanceId = await DurableBehaviorHarness.StartAndPumpAsync(
            host,
            definitionId,
            TestContext.Current.CancellationToken);

        (await DurableBehaviorHarness.SnapshotAsync(host, instanceId, TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowStatus.Completed);
        var events = await DurableBehaviorHarness.TailAsync(host, instanceId, TestContext.Current.CancellationToken);
        events.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle(
            "terminal cleanup releases every root-owned lease");
    }

    [Fact]
    public async Task CrashRestart_ExpiryRecoveryMakesCapacityAvailable()
    {
        var definitionId = DefinitionId.New();
        var builder = Workflow.Durable<DurableBehaviorHarness.GuardState>(definitionId, DefinitionVersion.Initial)
            .Init<object?>(_ => State("crash-holder"));
        var definition = AcquireStructuralLease(builder, "crash-holder", TimeSpan.FromMinutes(1))
            .Wait("Never", state => new CorrelationId(state.Key))
            .End("unreachable")
            .Build();
        var clock = new ManualTimeProvider(DurableBehaviorHarness.Epoch);
        var store = new OrcaCore.Providers.InMemory.InMemoryWorkflowProvider(clock);
        var pools = new OrcaCore.Providers.InMemory.InMemoryResourcePoolStore();
        await pools.UpsertPoolAsync(
            new ResourcePoolDefinition("db", 1, TimeSpan.FromMinutes(1)),
            TestContext.Current.CancellationToken);
        var crashedHost = DurableBehaviorHarness.CreateHost(store, pools, clock, definition);
        await DurableBehaviorHarness.StartAndPumpAsync(
            crashedHost,
            definitionId,
            TestContext.Current.CancellationToken);
        (await pools.GetPoolAsync("db", TestContext.Current.CancellationToken)).Value
            .AvailableCapacity.Should().Be(0);

        clock.Advance(TimeSpan.FromMinutes(2));
        var restartedHost = DurableBehaviorHarness.CreateHost(store, pools, clock, definition);
        var expired = await restartedHost.Runtime.Management.ExpireResourcePoolTicketsAsync(
            clock.GetUtcNow(),
            TestContext.Current.CancellationToken);
        expired.ExpiredTickets.Should().ContainSingle();
        var reacquired = await pools.AcquireAsync(
            new ResourcePoolAcquireRequest(
                InstanceId.New(),
                "replacement-holder",
                [new ResourcePoolRequirement("db", 1)],
                clock.GetUtcNow(),
                clock.GetUtcNow().AddMinutes(1)),
            TestContext.Current.CancellationToken);
        reacquired.Status.Should().Be(ResourcePoolAcquireStatus.Granted,
            "expiry is the crash-recovery backstop when deterministic release could not commit");
    }

    private static async Task<DurableGuardHost> CreateLeaseHostAsync(
        WorkflowDefinition<DurableBehaviorHarness.GuardState> definition)
    {
        var host = DurableBehaviorHarness.CreateHost(definition: definition);
        await host.Pools.UpsertPoolAsync(
            new ResourcePoolDefinition("db", 1, TimeSpan.FromMinutes(30)),
            TestContext.Current.CancellationToken);
        return host;
    }

    private static DurableBehaviorHarness.GuardState State(string key) => new() { Key = key };

    private static TBuilder AcquireStructuralLease<TBuilder>(
        TBuilder builder,
        string leaseId,
        TimeSpan leaseDuration)
        where TBuilder : notnull
    {
        var methods = builder.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == "AcquireLease")
            .Where(method => IsApprovedStructuralLeaseSignature(method, builder.GetType()))
            .ToArray();
        methods.Should().ContainSingle(
            "the structural contract is AcquireLease(id selector, ResourceLease selector, duration) on every durable root and nested builder");

        var method = methods[0];
        var parameters = method.GetParameters();
        var idSelector = ConstantSelector(parameters[0].ParameterType, leaseId);
        var resourceSelector = ResourceSelector(parameters[1].ParameterType, "db");
        var duration = parameters[2].ParameterType == typeof(TimeSpan?)
            ? (TimeSpan?)leaseDuration
            : leaseDuration;
        var result = method.Invoke(builder, [idSelector, resourceSelector, duration]);
        result.Should().BeAssignableTo<TBuilder>(
            "structural lease authoring must remain in the selected durable fluent family");
        return (TBuilder)result!;
    }

    private static bool IsApprovedStructuralLeaseSignature(MethodInfo method, Type builderType)
    {
        var parameters = method.GetParameters();
        return parameters.Length == 3 &&
               SelectorReturnType(parameters[0].ParameterType) == typeof(string) &&
               TryGetEnumerableElement(SelectorReturnType(parameters[1].ParameterType), out var resourceType) &&
               resourceType.Name == "ResourceLease" &&
               parameters[2].ParameterType is var durationType &&
               (durationType == typeof(TimeSpan) || durationType == typeof(TimeSpan?)) &&
               builderType.IsAssignableFrom(method.ReturnType);
    }

    private static Type? SelectorReturnType(Type type)
    {
        if (!typeof(Delegate).IsAssignableFrom(type))
        {
            return null;
        }

        return type.GetMethod("Invoke")?.ReturnType;
    }

    private static object ConstantSelector(Type selectorType, object value)
    {
        var invoke = selectorType.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters()
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
            .ToArray();
        return Expression.Lambda(
            selectorType,
            Expression.Constant(value, invoke.ReturnType),
            parameters).Compile();
    }

    private static object ResourceSelector(Type selectorType, string poolName)
    {
        var returnType = SelectorReturnType(selectorType)!;
        TryGetEnumerableElement(returnType, out var resourceType).Should().BeTrue();
        var require = resourceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "Require")
            .Where(method => method.ReturnType == resourceType)
            .Where(method => method.GetParameters().Length > 0 &&
                             method.GetParameters()[0].ParameterType == typeof(string))
            .OrderBy(method => method.GetParameters().Length)
            .FirstOrDefault();
        require.Should().NotBeNull(
            "the application ResourceLease descriptor must expose ResourceLease.Require(poolName, ...)");
        var arguments = require!.GetParameters()
            .Select((parameter, index) => index == 0
                ? poolName
                : parameter.HasDefaultValue
                    ? parameter.DefaultValue
                    : parameter.ParameterType == typeof(int)
                        ? 1
                        : throw new InvalidOperationException(
                            $"Unsupported required ResourceLease.Require parameter '{parameter.Name}'."))
            .ToArray();
        var resource = require.Invoke(null, arguments)!;
        var resources = Array.CreateInstance(resourceType, 1);
        resources.SetValue(resource, 0);
        returnType.IsAssignableFrom(resources.GetType()).Should().BeTrue(
            "the approved ResourceLease selector returns an array or read-only enumerable contract");
        return ConstantSelector(selectorType, resources);
    }

    private static bool TryGetEnumerableElement(Type? type, out Type elementType)
    {
        if (type is not null && type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type is null)
        {
            elementType = typeof(void);
            return false;
        }

        var enumerable = type.GetInterfaces()
            .Append(type)
            .FirstOrDefault(candidate => candidate.IsGenericType &&
                                         candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        elementType = enumerable?.GetGenericArguments()[0] ?? typeof(void);
        return enumerable is not null;
    }

    private sealed class ExpectedFailureStep : IStep<DurableBehaviorHarness.GuardState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<DurableBehaviorHarness.GuardState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Failed(
                new WorkflowDefinitionException("expected scope failure")));
    }
}
