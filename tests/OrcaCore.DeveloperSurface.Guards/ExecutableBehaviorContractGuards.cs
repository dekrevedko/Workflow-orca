using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using OrcaCore.DeveloperSurface.BehaviorContracts;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class ExecutableBehaviorInfrastructureGuards
{
    internal static readonly string[] Fixtures =
    [
        "state-and-codec-scenarios.json", "structured-fanout-scenarios.json",
        "facade-hosting-scenarios.json", "application-journey-scenarios.json",
        "deadline-retry-scenarios.json", "dag-contract-scenarios.json",
        "lease-authoring-admission-scenarios.json", "lease-retry-exit-scenarios.json",
        "lease-discovery-confirmation-scenarios.json", "governance-accounting-scenarios.json"
    ];

    [Fact]
    public void EveryBehaviorScenario_HasOneFullySpecifiedRuntimeCallContract()
    {
        var scenarios = Fixtures.SelectMany(Read).ToArray();
        var contracts = ScenarioCertification.ReadContracts();
        var signatures = ScenarioCertification.ReadSignatures();
        var scenarioKeys = scenarios.Select(x => $"{x.TaskId}/{x.Id}").ToArray();
        var contractKeys = contracts.Select(x => $"{x.TaskId}/{x.Id}").ToArray();

        scenarios.Should().HaveCount(95);
        scenarioKeys.Should().OnlyHaveUniqueItems();
        contractKeys.Should().OnlyHaveUniqueItems();
        contractKeys.Should().BeEquivalentTo(scenarioKeys);
        signatures.Select(signature => signature.Id).Should().OnlyHaveUniqueItems();
        signatures.Should().OnlyContain(signature =>
            !string.IsNullOrWhiteSpace(signature.Assembly) &&
            !string.IsNullOrWhiteSpace(signature.Type) &&
            !string.IsNullOrWhiteSpace(signature.Member) &&
            signature.GenericArity >= 0 && signature.Parameters != null &&
            !string.IsNullOrWhiteSpace(signature.ReturnType));

        var root = FixtureDefinitions.RepositoryRoot();
        var authority = File.ReadAllText(Path.Combine(root, "docs", "specs", "17-selected-mode-capability-matrix.md")) +
            Environment.NewLine +
            File.ReadAllText(Path.Combine(root, "docs", "specs", "17-public-authoring-contract.cs"));
        var tasks = File.ReadAllText(Path.Combine(root, "openspec", "changes",
            "reshape-developer-facing-interfaces", "tasks.md"));
        var productSource = string.Join(Environment.NewLine,
            Directory.GetFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                               !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Select(File.ReadAllText));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "tests",
            "OrcaCore.DeveloperSurface.Guards", "Fixtures", "v1-public-contract.json")));
        var packageIds = manifest.RootElement.GetProperty("packages").EnumerateArray()
            .Select(package => package.GetProperty("id").GetString()).OfType<string>().ToHashSet(StringComparer.Ordinal);

        foreach (var contract in contracts)
        {
            contract.RequiredCalls.Should().NotBeEmpty($"{contract.TaskId}/{contract.Id} needs an exact product seam");
            contract.HostAssembly.Should().Be(contract.TaskId == "3.11d"
                ? "OrcaCore.ProviderCertification"
                : "OrcaCore.DeveloperSurface.BehaviorScenarios");
            Enum.TryParse<Phase0DeterministicRequirement>(contract.Determinism, out _).Should().BeTrue();
            var resolved = ScenarioCertification.Resolve(contract, signatures);
            resolved.Calls.Select(call => call.Id).Should().OnlyHaveUniqueItems(
                $"{contract.TaskId}/{contract.Id} must execute each exact overload once through its observation");
            foreach (var call in resolved.Calls)
            {
                packageIds.Should().Contain(call.Assembly);
                if (call.Type == "OrcaCore.IWorkflowEventClient")
                {
                    tasks.Should().Contain("Completed against the superseded pre-7B contract",
                        "the old event-client executable remains current-product evidence only until Section 7B implementation replaces it");
                    productSource.Should().Contain(call.Type.Split('.').Last().Split('`')[0]);
                    productSource.Should().Contain(call.Member);
                    continue;
                }
                authority.Should().Contain(call.Type.Split('.').Last().Split('`')[0],
                    $"{contract.TaskId}/{contract.Id} type must be anchored in the normative matrix/companion");
                authority.Should().Contain(call.Member.StartsWith("get_", StringComparison.Ordinal) ? call.Member[4..] : call.Member,
                    $"{contract.TaskId}/{contract.Id} member must be anchored in the normative matrix/companion");
            }
        }

        contracts.Select(contract => ScenarioCertification.ExactCallSignature(
                ScenarioCertification.Resolve(contract, signatures))).Distinct().Should().HaveCountGreaterThan(10,
            "certification must be scenario-specific rather than a task-to-assembly lookup");
    }

    [Theory]
    [MemberData(nameof(Section4Scenarios))]
    public Task Section4Scenario_HasOneRuntimeRecordedExactProductDriverAndPassingAssertion(
        string taskId,
        string scenarioId) => ScenarioCertification.AssertExecutableAsync(taskId, scenarioId);

    public static IEnumerable<object[]> Section4Scenarios() => AllScenarios()
        .Where(scenario => FinalOwningSection(scenario) == 4)
        .Select(scenario => new object[] { scenario.TaskId, scenario.Id });

    [Theory]
    [MemberData(nameof(Section5Scenarios))]
    public Task Section5Scenario_HasOneRuntimeRecordedExactProductDriverAndPassingAssertion(
        string taskId,
        string scenarioId) =>
        ScenarioCertification.AssertExecutableAsync(taskId, scenarioId);

    public static IEnumerable<object[]> Section5Scenarios() => AllScenarios()
        .Where(scenario => FinalOwningSection(scenario) == 5)
        .Select(scenario => new object[] { scenario.TaskId, scenario.Id });

    [Theory]
    [MemberData(nameof(Section6Scenarios))]
    public Task Section6Scenario_HasOneRuntimeRecordedExactProductDriverAndPassingAssertion(
        string taskId,
        string scenarioId) =>
        ScenarioCertification.AssertExecutableAsync(taskId, scenarioId);

    public static IEnumerable<object[]> Section6Scenarios() => AllScenarios()
        .Where(scenario => FinalOwningSection(scenario) == 6)
        .Select(scenario => new object[] { scenario.TaskId, scenario.Id });

    [Theory]
    [MemberData(nameof(Section7Scenarios))]
    public Task Section7Scenario_HasOneRuntimeRecordedExactProductDriverAndPassingAssertion(
        string taskId,
        string scenarioId) => ScenarioCertification.AssertExecutableAsync(taskId, scenarioId);

    public static IEnumerable<object[]> Section7Scenarios() => AllScenarios()
        .Where(scenario => FinalOwningSection(scenario) == 7)
        .Select(scenario => new object[] { scenario.TaskId, scenario.Id });

    [Fact]
    public void UninvokedLambdaAndStoredIgnoredResult_CannotEnterObservationContext()
    {
        var contract = MutationContract(nameof(Phase0MutationProbe.RequiredProductOperation));

        var service = Expression.Parameter(typeof(Phase0ScenarioServices), "services");
        var exactCall = Expression.Call(typeof(Phase0MutationProbe),
            nameof(Phase0MutationProbe.RequiredProductOperation), Type.EmptyTypes);
        var uninvoked = Expression.Lambda<Func<string>>(exactCall);
        var uninvokedBody = Expression.Block(uninvoked, Expression.Constant("unrelated"));
        var uninvokedExpression = Expression.Lambda<Func<Phase0ScenarioServices, string>>(uninvokedBody, service);
        var uninvokedContext = ScenarioCertification.CreateContext(contract);
        Action uninvokedAction = () => uninvokedContext.Observe(uninvokedExpression);
        uninvokedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*one direct method, property, or constructor call*");

        var stored = Expression.Variable(typeof(string), "stored");
        var storedBody = Expression.Block([stored], Expression.Assign(stored, exactCall), Expression.Constant(true));
        var storedExpression = Expression.Lambda<Func<Phase0ScenarioServices, bool>>(storedBody, service);
        var storedContext = ScenarioCertification.CreateContext(contract);
        Action storedAction = () => storedContext.Observe(storedExpression);
        storedAction.Should().Throw<InvalidOperationException>()
            .WithMessage("*blocks, wrappers, stored results, and unrelated returns are forbidden*");
    }

    [Fact]
    public void WrongMemberAndWrongOverloadSignature_CannotMintAnObservation()
    {
        var contract = MutationContract(nameof(Phase0MutationProbe.RequiredProductOperation));
        var context = ScenarioCertification.CreateContext(contract);
        Action wrongMember = () => context.Observe(_ => Phase0MutationProbe.TrivialProductOperation());
        wrongMember.Should().Throw<InvalidOperationException>().WithMessage("*does not match any required exact signature*");

        var correct = contract.Calls[0];
        var wrongReturn = contract with
        {
            Calls = [correct with { Id = "wrong-return", ReturnType = typeof(bool).FullName! }]
        };
        var overloadContext = ScenarioCertification.CreateContext(wrongReturn);
        Action wrongOverload = () => overloadContext.Observe(_ => Phase0MutationProbe.RequiredProductOperation());
        wrongOverload.Should().Throw<InvalidOperationException>().WithMessage("*does not match any required exact signature*");
    }

    [Fact]
    public void ThrowingArgumentExpression_CannotMintAnObservationForAnUninvokedFacade()
    {
        var facade = typeof(global::OrcaCore.Workflow).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == nameof(global::OrcaCore.Workflow.Ephemeral) &&
                method.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(object));
        var signature = ScenarioCertification.FromMethod("mutation-throwing-argument", facade);
        var contract = new ResolvedScenarioContract(
            "mutation-throwing-argument",
            "mutation-control",
            typeof(ExecutableBehaviorInfrastructureGuards).Assembly.GetName().Name!,
            "None",
            [signature],
            ["OrcaCore", "OrcaCore.Core"]);
        var context = ScenarioCertification.CreateContext(contract);
        Action observe = () => context
            .ObserveThrows<InvalidOperationException, global::OrcaCore.EphemeralWorkflowInitBuilder<object>>(_ =>
                global::OrcaCore.Workflow.Ephemeral<object>(
                    ThrowBeforeFacadeExecution(),
                    global::OrcaCore.DefinitionVersion.Initial));

        observe.Should().Throw<InvalidOperationException>()
            .WithMessage("*argument failed before the facade executed*");
        context.CertificationErrors().Should().Contain(error =>
            error.Contains("required exact call", StringComparison.Ordinal));
    }

    private static global::OrcaCore.DefinitionId ThrowBeforeFacadeExecution() =>
        throw new InvalidOperationException("argument failed before the facade executed");

    [Fact]
    public async Task SystemClockAndNoOpBarrier_FailRuntimeConsumptionCertification()
    {
        var clockContract = MutationContract(nameof(Phase0MutationProbe.IgnoreInjectedClock), "Time");
        var clockContext = ScenarioCertification.CreateContext(clockContract);
        var clockObservation = clockContext.Observe(services =>
            Phase0MutationProbe.IgnoreInjectedClock(services.TimeProvider));
        Phase0Assert.Satisfies(clockObservation, value => value != default, "a clock value was returned");
        clockContext.CertificationErrors().Should().Contain(error =>
            error.Contains("deterministic TimeProvider", StringComparison.Ordinal));

        var barrierContract = MutationContract(nameof(Phase0MutationProbe.IgnoreInjectedBarrierAsync), "Barrier");
        var barrierContext = ScenarioCertification.CreateContext(barrierContract);
        var barrierObservation = await barrierContext.ObserveAsync(services =>
            Phase0MutationProbe.IgnoreInjectedBarrierAsync(services.Barrier));
        Phase0Assert.Completed(barrierObservation, "the no-op product barrier returned");
        barrierContext.CertificationErrors().Should().Contain(error =>
            error.Contains("deterministic barrier", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DirectHarnessUse_CannotCertifyDeterministicProductConsumption()
    {
        var signature = ScenarioCertification.FromMethod(
            "mutation-direct-harness",
            typeof(Phase0MutationProbe).GetMethod(
                nameof(Phase0MutationProbe.RequiredProductOperation),
                BindingFlags.Static | BindingFlags.NonPublic)!);
        var contract = new ResolvedScenarioContract(
            "mutation-direct-harness",
            "mutation-control",
            typeof(ExecutableBehaviorInfrastructureGuards).Assembly.GetName().Name!,
            "TimeAndBarrier",
            [signature],
            ["OrcaCore.Core"]);
        var context = ScenarioCertification.CreateContext(contract);

        _ = context.Services.TimeProvider.GetUtcNow();
        context.ReleaseBarrier("direct-harness");
        await context.Services.Barrier.ReachAsync(
            "direct-harness",
            TestContext.Current.CancellationToken);

        var errors = context.CertificationErrors();
        context.ProductConsumptionFrames.Should().BeEmpty(
            "a direct harness call has no product invocation on its causal stack");
        errors.Should().Contain(error =>
            error.Contains("deterministic TimeProvider", StringComparison.Ordinal));
        errors.Should().Contain(error =>
            error.Contains("deterministic barrier", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DirectAsyncAndThrowingProductExpressions_AreRuntimeRecordedAndLinked()
    {
        var deterministic = MutationContract(nameof(Phase0MutationProbe.ConsumeDeterminismAsync), "TimeAndBarrier");
        var deterministicContext = ScenarioCertification.CreateContext(deterministic);
        deterministicContext.ReleaseBarrier("mutation");
        var deterministicObservation = await deterministicContext.ObserveAsync(services =>
            Phase0MutationProbe.ConsumeDeterminismAsync(services));
        Phase0Assert.Equal("observed", deterministicObservation, "the product consumed both guard-owned seams");
        deterministicContext.CertificationErrors().Should().BeEmpty();

        var throwing = MutationContract(nameof(Phase0MutationProbe.ThrowAsync));
        var throwingContext = ScenarioCertification.CreateContext(throwing);
        var exceptionObservation = await throwingContext.ObserveThrowsAsync<InvalidOperationException>(services =>
            Phase0MutationProbe.ThrowAsync(services));
        Phase0Assert.Satisfies(exceptionObservation,
            exception => exception.Message == "mutation probe",
            "the exact product exception is the asserted observation");
        throwingContext.CertificationErrors().Should().BeEmpty();
    }

    [Fact]
    public async Task EverySyncTaskAndValueTaskExceptionShape_IsRuntimeRecordedAndLinked()
    {
        var syncValue = MutationContract(nameof(Phase0MutationProbe.ThrowSyncValue));
        var syncValueContext = ScenarioCertification.CreateContext(syncValue);
        var syncValueObservation = syncValueContext.ObserveThrows<InvalidOperationException, string>(_ =>
            Phase0MutationProbe.ThrowSyncValue());
        Phase0Assert.Satisfies(syncValueObservation, exception => exception.Message == "sync value probe", "sync value");
        syncValueContext.CertificationErrors().Should().BeEmpty();

        var syncVoid = MutationContract(nameof(Phase0MutationProbe.ThrowSyncVoid));
        var syncVoidContext = ScenarioCertification.CreateContext(syncVoid);
        var syncVoidObservation = syncVoidContext.ObserveThrows<InvalidOperationException>(_ =>
            Phase0MutationProbe.ThrowSyncVoid());
        Phase0Assert.Satisfies(syncVoidObservation, exception => exception.Message == "sync void probe", "sync void");
        syncVoidContext.CertificationErrors().Should().BeEmpty();

        var valueTaskValue = MutationContract(nameof(Phase0MutationProbe.ThrowValueTaskOfTAsync));
        var valueTaskValueContext = ScenarioCertification.CreateContext(valueTaskValue);
        var valueTaskValueObservation = await valueTaskValueContext
            .ObserveThrowsAsync<InvalidOperationException, string>(_ => Phase0MutationProbe.ThrowValueTaskOfTAsync());
        Phase0Assert.Satisfies(valueTaskValueObservation,
            exception => exception.Message == "valuetask value probe", "valuetask value");
        valueTaskValueContext.CertificationErrors().Should().BeEmpty();

        var task = MutationContract(nameof(Phase0MutationProbe.ThrowTaskAsync));
        var taskContext = ScenarioCertification.CreateContext(task);
        var taskObservation = await taskContext
            .ObserveTaskThrowsAsync<InvalidOperationException>(_ => Phase0MutationProbe.ThrowTaskAsync());
        Phase0Assert.Satisfies(taskObservation, exception => exception.Message == "task probe", "task");
        taskContext.CertificationErrors().Should().BeEmpty();

        var taskValue = MutationContract(nameof(Phase0MutationProbe.ThrowTaskOfTAsync));
        var taskValueContext = ScenarioCertification.CreateContext(taskValue);
        var taskValueObservation = await taskValueContext
            .ObserveTaskThrowsAsync<InvalidOperationException, string>(_ => Phase0MutationProbe.ThrowTaskOfTAsync());
        Phase0Assert.Satisfies(taskValueObservation, exception => exception.Message == "task value probe", "task value");
        taskValueContext.CertificationErrors().Should().BeEmpty();
    }

    internal static GuardScenario[] Read(string fixture) => FixtureDefinitions.Read<GuardScenario[]>(
        $"tests/OrcaCore.DeveloperSurface.Guards/Fixtures/{fixture}");

    internal static IEnumerable<GuardScenario> AllScenarios() => Fixtures.SelectMany(Read);

    internal static int FinalOwningSection(GuardScenario scenario)
    {
        var sections = Regex.Matches(scenario.TurnsGreenTask, @"(?<![\d.])(?<section>\d+)\.\d+")
            .Select(match => int.Parse(match.Groups["section"].Value, System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        return sections.Length > 0
            ? sections.Max()
            : throw new InvalidOperationException(
                $"{scenario.TaskId}/{scenario.Id} has no parseable turnsGreenTask owner.");
    }

    private static ResolvedScenarioContract MutationContract(string member, string determinism = "None")
    {
        var method = typeof(Phase0MutationProbe).GetMethod(member, BindingFlags.Static | BindingFlags.NonPublic)!;
        var signature = ScenarioCertification.FromMethod($"mutation-{member}", method);
        return new ResolvedScenarioContract("mutation-control", "mutation",
            typeof(ExecutableBehaviorInfrastructureGuards).Assembly.GetName().Name!, determinism, [signature],
            [typeof(Phase0MutationProbe).Assembly.GetName().Name!]);
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class ExecutableBehaviorExpectedRedGuards
{
    [Theory]
    [MemberData(nameof(RemainingScenarios))]
    public Task Scenario_HasOneRuntimeRecordedExactProductDriverAndPassingAssertion(
        string taskId,
        string scenarioId) => ScenarioCertification.AssertExecutableAsync(taskId, scenarioId);

    public static IEnumerable<object[]> RemainingScenarios() =>
        ExecutableBehaviorInfrastructureGuards.AllScenarios()
            .Where(scenario => ExecutableBehaviorInfrastructureGuards.FinalOwningSection(scenario) > 7)
            .Select(scenario => new object[] { scenario.TaskId, scenario.Id });
}

internal sealed record RequiredCallContract(string Assembly, string Type, string Member)
{
    public string? SignatureId { get; init; }
}

internal sealed record ExactCallSignature(
    string Id,
    string Assembly,
    string Type,
    string Member,
    int GenericArity,
    string[] Parameters,
    string ReturnType);

internal sealed record ScenarioCertificationContract(
    string Id,
    string TaskId,
    string HostAssembly,
    string Determinism,
    RequiredCallContract[] RequiredCalls);

internal sealed record ResolvedScenarioContract(
    string Id,
    string TaskId,
    string HostAssembly,
    string Determinism,
    ExactCallSignature[] Calls,
    string[] ProductAssemblies)
{
    internal Phase0DeterministicRequirement DeterministicRequirement =>
        Enum.Parse<Phase0DeterministicRequirement>(Determinism);
}

internal static class ScenarioCertification
{
    internal static ScenarioCertificationContract[] ReadContracts() =>
        FixtureDefinitions.Read<ScenarioCertificationContract[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/behavior-certification-contracts.json");

    internal static ExactCallSignature[] ReadSignatures() =>
        FixtureDefinitions.Read<ExactCallSignature[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/behavior-exact-call-signatures.json");

    internal static ResolvedScenarioContract Resolve(
        ScenarioCertificationContract contract,
        IReadOnlyList<ExactCallSignature> signatures)
    {
        var resolved = contract.RequiredCalls.Select(required =>
        {
            var candidates = required.SignatureId is null
                ? signatures.Where(signature => signature.Assembly == required.Assembly &&
                                                signature.Type == required.Type &&
                                                signature.Member == required.Member).ToArray()
                : signatures.Where(signature => signature.Id == required.SignatureId).ToArray();
            candidates.Should().ContainSingle(
                $"{contract.TaskId}/{contract.Id} call {required.Assembly}:{required.Type}::{required.Member} must select one complete overload signature");
            var selected = candidates.Single();
            selected.Assembly.Should().Be(required.Assembly);
            selected.Type.Should().Be(required.Type);
            selected.Member.Should().Be(required.Member);
            return selected;
        }).ToArray();
        return new ResolvedScenarioContract(contract.Id, contract.TaskId, contract.HostAssembly,
            contract.Determinism, resolved, V1ProductAssemblies());
    }

    internal static string ExactCallSignature(ResolvedScenarioContract contract) => string.Join("|",
        contract.Calls.Select(call =>
            $"{call.Assembly}:{call.Type}::{call.Member}`{call.GenericArity}({string.Join(",", call.Parameters)})->{call.ReturnType}"));

    internal static Phase0ScenarioContext CreateContext(ResolvedScenarioContract contract) => new(
        contract.Calls.Select(call => new Phase0ExpectedCall(call.Id, call.Assembly, call.Type, call.Member,
            call.GenericArity, call.Parameters, call.ReturnType)).ToArray(),
        contract.ProductAssemblies,
        [typeof(ScenarioCertification).Assembly.GetName().Name!],
        contract.DeterministicRequirement);

    private static string[] V1ProductAssemblies()
    {
        var path = Path.Combine(FixtureDefinitions.RepositoryRoot(), "tests",
            "OrcaCore.DeveloperSurface.Guards", "Fixtures", "v1-public-contract.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(path));
        return manifest.RootElement.GetProperty("packages").EnumerateArray()
            .Select(package => package.GetProperty("id").GetString())
            .OfType<string>().ToArray();
    }

    internal static ExactCallSignature FromMethod(string id, MethodInfo method)
    {
        if (method.IsGenericMethod) method = method.GetGenericMethodDefinition();
        return new ExactCallSignature(id, method.DeclaringType!.Assembly.GetName().Name!,
            method.DeclaringType.FullName!, method.Name,
            method.IsGenericMethodDefinition ? method.GetGenericArguments().Length : 0,
            method.GetParameters().Select(parameter => Phase0ScenarioContext.CanonicalTypeName(parameter.ParameterType)).ToArray(),
            Phase0ScenarioContext.CanonicalTypeName(method.ReturnType));
    }

    internal static IReadOnlyList<MethodInfo> Discover()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var guardOutput = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var targetFramework = guardOutput.Name;
        var configuration = guardOutput.Parent!.Name;
        var candidates = new[]
        {
            Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.BehaviorScenarios", "bin", configuration,
                targetFramework, "OrcaCore.DeveloperSurface.BehaviorScenarios.dll"),
            Path.Combine(root, "tests", "OrcaCore.ProviderCertification", "bin", configuration,
                targetFramework, "OrcaCore.ProviderCertification.dll")
        };
        return candidates.Where(File.Exists)
            .Select(Assembly.LoadFrom)
            .SelectMany(SafeTypes)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.GetCustomAttribute<Phase0ScenarioAttribute>() is not null)
            .ToArray();
    }

    internal static IReadOnlyList<string> Validate(
        MethodInfo method,
        ResolvedScenarioContract contract,
        bool requireApprovedHost)
    {
        var errors = new List<string>();
        var attribute = method.GetCustomAttribute<Phase0ScenarioAttribute>();
        if (attribute?.Id != contract.Id || attribute.TaskId != contract.TaskId)
            errors.Add("the Phase0Scenario attribute does not match the frozen id/task");
        if (requireApprovedHost && method.DeclaringType!.Assembly.GetName().Name != contract.HostAssembly)
            errors.Add($"driver must live in approved host {contract.HostAssembly}");
        if (!method.IsPublic || !method.IsStatic || method.GetParameters() is not [{ ParameterType: var parameterType }] ||
            parameterType != typeof(Phase0ScenarioContext))
            errors.Add("driver must be public, static, and accept exactly one Phase0ScenarioContext");
        if (method.ReturnType != typeof(void) && method.ReturnType != typeof(Task) &&
            method.ReturnType != typeof(ValueTask))
            errors.Add("driver must return void, Task, or ValueTask");
        return errors;
    }

    internal static async ValueTask<IReadOnlyList<string>> ExecuteAsync(
        MethodInfo method,
        ResolvedScenarioContract contract)
    {
        var context = CreateContext(contract);
        object? result;
        try
        {
            result = method.Invoke(null, [context]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
        if (result is Task task) await task;
        else if (result is ValueTask valueTask) await valueTask;
        return context.CertificationErrors();
    }

    internal static async Task AssertExecutableAsync(string taskId, string scenarioId)
    {
        var definition = ExecutableBehaviorInfrastructureGuards.AllScenarios().Single(
            scenario => scenario.TaskId == taskId && scenario.Id == scenarioId);
        var methods = Discover();
        var signatures = ReadSignatures();
        var contractDefinition = ReadContracts().Single(
            contract => contract.TaskId == taskId && contract.Id == scenarioId);
        var contract = Resolve(contractDefinition, signatures);
        var matches = methods.Where(method =>
        {
            var attribute = method.GetCustomAttribute<Phase0ScenarioAttribute>();
            return attribute?.Id == contract.Id && attribute.TaskId == contract.TaskId;
        }).ToArray();

        matches.Should().ContainSingle(
            $"{contract.TaskId}/{contract.Id} must have one reviewable executable driver in {contract.HostAssembly}");
        if (matches.Length != 1) return;

        var errors = Validate(matches[0], contract, true);
        errors.Should().BeEmpty($"{contract.TaskId}/{contract.Id} must use the exact one-context driver signature");
        if (errors.Count != 0) return;

        Exception? failure = null;
        IReadOnlyList<string> runtimeErrors = [];
        try
        {
            runtimeErrors = await ExecuteAsync(matches[0], contract);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        failure.Should().BeNull($"{contract.TaskId}/{contract.Id}: {definition.Assertion}");
        runtimeErrors.Should().BeEmpty(
            $"{contract.TaskId}/{contract.Id} must execute every exact overload through a guard-owned observation, assert each result, and consume required deterministic seams through product code");
    }

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException exception) { return exception.Types.OfType<Type>(); }
    }
}
