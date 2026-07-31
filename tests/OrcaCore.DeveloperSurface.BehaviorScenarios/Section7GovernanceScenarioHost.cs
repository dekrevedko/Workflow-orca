using Microsoft.Extensions.DependencyInjection;
using OrcaCore.DeveloperSurface.BehaviorContracts;
using OrcaCore.Hosting;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

public static class Section7GovernanceScenarioHost
{
    [Phase0Scenario("static-pool-registration-compatibility", "3.11a")]
    public static void RegistrationChecksCopiedStaticPoolRequirementsBeforeMutation(
        Phase0ScenarioContext context)
    {
        using var provider = DurableServices("static-registration", "configured").BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var configured = DurableDefinition("configured");
        var registered = context.Observe(_ => registry.Register(configured));
        Phase0Assert.Satisfies(
            registered,
            result => result is WorkflowRegistrationResult<
                DurableDefinitionHandle<GovernanceInput>>.Registered,
            "A definition using a configured static pool did not register.");

        var missing = DurableDefinition("zeta", "Alpha");
        var incompatible = context.Observe(_ => registry.Register(missing));
        Phase0Assert.Satisfies(
            incompatible,
            result => result is WorkflowRegistrationResult<
                DurableDefinitionHandle<GovernanceInput>>.HostIncompatible
            {
                Error: DefinitionHostCompatibilityFailure.MissingDurableResourcePools pools
            } &&
            pools.PoolNames.Select(pool => pool.Value).SequenceEqual(["Alpha", "zeta"]),
            "Static durable-pool compatibility did not return copied distinct ordinal-sorted names.");
        var callerOwned = new List<ResourcePoolName>
        {
            ResourcePoolName.Create("zeta"),
            ResourcePoolName.Create("Alpha"),
            ResourcePoolName.Create("zeta")
        };
        var copied = new DefinitionHostCompatibilityFailure.MissingDurableResourcePools(callerOwned);
        callerOwned.Clear();
        if (!copied.PoolNames.Select(pool => pool.Value).SequenceEqual(["Alpha", "zeta"]))
        {
            throw new InvalidOperationException(
                "The compatibility failure retained or failed to normalize caller-owned names.");
        }

        var replay = registry.Register(configured);
        if (replay is not WorkflowRegistrationResult<
                DurableDefinitionHandle<GovernanceInput>>.Registered)
        {
            throw new InvalidOperationException(
                "A failed compatibility check mutated the existing definition binding.");
        }
    }

    [Phase0Scenario("advanced-diagnostics-only", "3.11c")]
    public static async Task AdvancedDiagnosticsAreDetachedAndAbsentFromOrdinaryHandles(
        Phase0ScenarioContext context)
    {
        using var provider = DurableServices("advanced-diagnostics", "configured")
            .BuildServiceProvider();
        var diagnostics = provider.GetRequiredService<IDurableResourceLeaseDiagnostics>();
        var sequence = context.Observe(_ => diagnostics.EnumerateOutstandingAsync());
        Phase0Assert.Satisfies(
            sequence,
            value => value is not null,
            "Advanced diagnostics did not return an asynchronous detached sequence.");
        var observed = new List<DurableResourceLeaseObligationSnapshot>();
        await foreach (var snapshot in diagnostics.EnumerateOutstandingAsync())
        {
            observed.Add(snapshot);
        }

        if (observed.Count != 0)
        {
            throw new InvalidOperationException("An empty host exposed a synthetic lease obligation.");
        }

        var ordinaryMembers = typeof(WorkflowInstanceHandle)
            .GetMembers()
            .Select(member => member.Name)
            .ToArray();
        if (ordinaryMembers.Any(name =>
                name.Contains("Lease", StringComparison.Ordinal) ||
                name.Contains("Obligation", StringComparison.Ordinal) ||
                name.Contains("Ticket", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "Ordinary workflow handles exposed advanced lease-governance facts.");
        }

        var diagnosticProperties = typeof(DurableResourceLeaseObligationSnapshot)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var required = new[]
        {
            "AcceptedConfirmationId",
            "AuthoredLocation",
            "DefinitionId",
            "DefinitionVersion",
            "FiberOccurrence",
            "Generation",
            "InstanceId",
            "ObligationId",
            "ProtectionToken",
            "QuarantinedAt",
            "ScopeOccurrence",
            "Status",
            "Tickets"
        };
        if (!diagnosticProperties.SequenceEqual(required))
        {
            throw new InvalidOperationException(
                "Advanced diagnostics did not expose the exact detached correlated fact shape.");
        }
    }

    [Phase0Scenario("adapter-owned-security", "3.11c")]
    public static async Task ProductExposesNoRemoteDiagnosticsOrCallerSecurityPolicy(
        Phase0ScenarioContext context)
    {
        using var provider = DurableServices("adapter-security", "configured").BuildServiceProvider();
        var diagnostics = provider.GetRequiredService<IDurableResourceLeaseDiagnostics>();
        var missing = await context.ObserveAsync(_ => diagnostics.GetAsync(
            LeaseProtectionToken.Parse("unknown-protection-token")));
        Phase0Assert.Satisfies(
            missing,
            value => value is null,
            "Unknown diagnostics lookup returned an unrelated obligation.");

        var exported = typeof(IDurableResourceLeaseDiagnostics).Assembly.GetExportedTypes();
        if (exported.Any(type =>
                type.Name.Contains("Controller", StringComparison.Ordinal) ||
                type.Name.Contains("Endpoint", StringComparison.Ordinal) ||
                type.Name.Contains("Authorization", StringComparison.Ordinal) ||
                type.Name.Contains("Redaction", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The product package supplied a remote diagnostics endpoint or adapter security policy.");
        }

        var parameters = typeof(IDurableResourceLeaseDiagnostics)
            .GetMethods()
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.ParameterType)
            .ToArray();
        if (parameters.Any(type =>
                type.Name.Contains("Principal", StringComparison.Ordinal) ||
                type.Name.Contains("Authorization", StringComparison.Ordinal) ||
                type.Name.Contains("Redaction", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The in-process diagnostics contract invented a caller-owned authorization/redaction seam.");
        }
    }

    private static IServiceCollection DurableServices(
        string partition,
        params string[] pools)
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = ResourceGovernancePartitionId.Create(partition),
                Pools = pools
                    .Select(pool => DurableResourcePoolDefinition.Create(
                        ResourcePoolName.Create(pool),
                        2,
                        TimeSpan.FromMinutes(5)))
                    .ToArray()
            }
        });
        return services;
    }

    private static DurableWorkflowDefinition<GovernanceInput> DurableDefinition(
        params string[] pools)
    {
        var requirements = pools
            .Select(pool => ResourceLeaseRequirement.Require(ResourcePoolName.Create(pool)))
            .ToArray();
        return Workflow.Durable<GovernanceState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<GovernanceInput>(input => new GovernanceState(input.Value))
            .AcquireResources(
                ResourceLeaseRequest.Create(requirements[0], requirements.Skip(1).ToArray()),
                lease => lease.Then<GovernanceStep>())
            .End()
            .Build();
    }

    private sealed record GovernanceInput(int Value);

    private sealed record GovernanceState(int Value);

    private sealed class GovernanceStep : IStep<GovernanceState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<GovernanceState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
