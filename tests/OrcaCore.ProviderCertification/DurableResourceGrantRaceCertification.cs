using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.ResourceGovernance;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.ProviderCertification;

public static class DurableResourceGrantRaceCertification
{
    public static async Task RunAsync(Func<IServiceCollection> providerServices, Type providerType, bool race)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(1));
        var token = deadline.Token;
        using var stopLoser = CancellationTokenSource.CreateLinkedTokenSource(token);
        var a = new Gate(token);
        var b = new Gate(token);
        var observer = new StopAfterConflict(stopLoser);
        var partition = ResourceGovernancePartitionId.Create($"grant-{Guid.CreateVersion7():N}");
        await using var first = BuildHost(providerServices(), partition);
        await using var second = BuildHost(providerServices(), partition);
        await first.GetServices<IHostedService>().Single(service => service.GetType().Assembly == providerType.Assembly)
            .StartAsync(token);

        var definition = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value)
            .AcquireResources(ResourceLeaseRequest.Create(ResourceLeaseRequirement.Require(ResourcePoolName.Create("slot"))),
                scope => scope.Then<NoOpStep>())
            .End().Build();
        var firstRuntime = CreateRuntime(first, a);
        var secondRuntime = CreateRuntime(second, b, observer);
        firstRuntime.RegisterDefinition(definition);
        secondRuntime.RegisterDefinition(definition);
        var pools = second.GetRequiredService<IResourcePoolStore>();
        var events = second.GetRequiredService<IWorkflowEventStore>();
        var rival = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var firstDrive = firstRuntime.StartOrGetAsync<int, int>($"start-{partition}", definition.DefinitionId,
            definition.DefinitionVersion, 0, token);
        Task? secondDrive = null;
        try
        {
            var grant = await a.Reserved.WaitAsync(firstDrive, token);
            var original = grant.Tickets.Should().ContainSingle().Which;
            if (race)
            {
                secondDrive = secondRuntime.DriveAsync(grant.InstanceId, stopLoser.Token);
                var replay = await b.Reserved.WaitAsync(secondDrive, token);
                replay.Should().BeEquivalentTo(grant);
            }

            var waiting = await pools.AcquireAsync(new ResourcePoolAcquireRequest(rival, "rival",
                [new ResourcePoolRequirement("slot", 1)], DateTimeOffset.UtcNow, null), token);
            waiting.Status.Should().Be(ResourcePoolAcquireStatus.Queued);
            a.Reserved.Resume();
            var activation = await a.Activated.WaitAsync(firstDrive, token);
            activation.Tickets.Should().Equal(original);
            var before = (await pools.GetPoolAsync("slot", token)).Value;
            var winnerTicket = before.HeldTickets.Should().ContainSingle().Which;
            winnerTicket.HolderInstanceId.Should().Be(grant.InstanceId);
            winnerTicket.TicketId.ToString("N").Should().Be(original.TicketId);
            winnerTicket.ProviderGeneration.Should().Be(original.ProviderGeneration);
            before.QueuedWaiters.Should().ContainSingle().Which.HolderInstanceId.Should().Be(rival);

            if (secondDrive is not null)
            {
                b.Reserved.Resume();
                // Stop this driver only after its first conflict, before an automatic retry changes the evidence.
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondDrive.WaitAsync(token));
                observer.SawConflict.Should().BeTrue();
            }

            var history = await events.LoadTailAsync(new WorkflowStreamId(grant.InstanceId), StreamVersion.Empty, token);
            var acquired = history.OfType<WorkflowResourcePoolAcquiredEvent>().Should().ContainSingle().Which;
            acquired.Tickets.Should().ContainSingle().Which.TicketId.Should().Be(winnerTicket.TicketId);
            history.OfType<WorkflowResourcePoolReleasedEvent>().Should().BeEmpty();
            var checkpoint = (await events.LoadCheckpointAsync(grant.InstanceId, token)).Value;
            var lease = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Payload).OwnedObligations.Should().ContainSingle().Which;
            lease.LeasePhase.Should().Be("Held");
            lease.ObligationId.Should().Be(grant.ObligationId);
            lease.ProtectionToken.Should().Be(grant.LeaseProtectionToken.Value);
            lease.LeaseTickets.Should().ContainSingle().Which.TicketId.Should().Be(original.TicketId);
            var after = (await pools.GetPoolAsync("slot", token)).Value;
            var held = after.HeldTickets.Should().ContainSingle().Which;
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"conflict={observer.SawConflict}, workflow=Held, acquired=1, released=0, " +
                $"winner={grant.InstanceId}, original={winnerTicket.TicketId}/{winnerTicket.ProviderGeneration}, " +
                $"rival={rival}, owner={held.HolderInstanceId}, actual={held.TicketId}/{held.ProviderGeneration}, queued={after.QueuedWaiters.Count}");
            using (new AssertionScope("while A is active"))
            {
                held.Should().BeEquivalentTo(winnerTicket);
                after.QueuedWaiters.Select(x => x.HolderInstanceId).Should().Equal(rival);
            }

            a.Activated.Resume();
            await firstDrive.WaitAsync(token);
            var finalHistory = await events.LoadTailAsync(new WorkflowStreamId(grant.InstanceId), StreamVersion.Empty, token);
            var release = finalHistory.OfType<WorkflowResourcePoolReleasedEvent>().Should().ContainSingle().Which;
            release.HolderKey.Should().Be(acquired.HolderKey);
            var releasedTicket = release.Tickets.Should().ContainSingle().Which;
            releasedTicket.TicketId.Should().Be(winnerTicket.TicketId);
            releasedTicket.ProviderGeneration.Should().Be(winnerTicket.ProviderGeneration);
            finalHistory.SkipWhile(record => record != release).OfType<WorkflowCompletedEvent>().Should().ContainSingle();
            var exited = (await pools.GetPoolAsync("slot", token)).Value;
            var successor = exited.HeldTickets.Should().ContainSingle().Which;
            successor.HolderInstanceId.Should().Be(rival);
            successor.TicketId.Should().NotBe(winnerTicket.TicketId);
            successor.ProviderGeneration.Should().BeGreaterThan(winnerTicket.ProviderGeneration);
            exited.QueuedWaiters.Should().BeEmpty();
        }
        finally
        {
            stopLoser.Cancel();
            a.Resume();
            b.Resume();
            try { if (secondDrive is not null) await secondDrive.WaitAsync(token); }
            catch (OperationCanceledException) when (stopLoser.IsCancellationRequested) { }
            await firstDrive.WaitAsync(token);
        }
    }

    private static ServiceProvider BuildHost(IServiceCollection services, ResourceGovernancePartitionId partition)
    {
        services.AddTransient<NoOpStep>();
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = partition,
                Pools = [DurableResourcePoolDefinition.Create(ResourcePoolName.Create("slot"), 1, TimeSpan.FromHours(1))]
            }
        });
        return services.BuildServiceProvider();
    }

    private static DurableWorkflowRuntime CreateRuntime(ServiceProvider host, Gate gate, IDurableDriverObserver? observer = null)
    {
        var processor = new DurableCommandProcessor(host.GetRequiredService<IWorkflowEventStore>(),
            host.GetRequiredService<IResourcePoolStore>()) { LeaseCertificationGate = gate };
        return new DurableWorkflowRuntime(processor, new DurableDefinitionRegistry(host), TimeProvider.System, driverObserver: observer);
    }

    private sealed class NoOpStep : IStep<int>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<int> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class Pause
    {
        private readonly TaskCompletionSource<DurableResourceLeaseCommitBarrierFact> reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource resume = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal async Task<DurableResourceLeaseCommitBarrierFact> WaitAsync(Task worker, CancellationToken token)
        {
            if (await Task.WhenAny(reached.Task, worker).WaitAsync(token) == worker)
            {
                await worker;
                throw new InvalidOperationException("Driver completed before the lease barrier.");
            }
            return await reached.Task;
        }

        internal ValueTask EnterAsync(DurableResourceLeaseCommitBarrierFact fact, CancellationToken token)
        {
            reached.TrySetResult(fact);
            return new ValueTask(resume.Task.WaitAsync(token));
        }

        internal void Resume() => resume.TrySetResult();
    }

    private sealed class Gate(CancellationToken deadline) : IDurableResourceLeaseCertificationGate
    {
        internal Pause Reserved { get; } = new();
        internal Pause Activated { get; } = new();

        public ValueTask OnPostCommitAsync(DurableResourceLeaseCommitBarrierFact fact, CancellationToken cancellationToken = default) =>
            fact.Barrier switch
            {
                DurableResourceLeaseCommitBarrier.GovernanceReservationCommitted => Reserved.EnterAsync(fact, deadline),
                DurableResourceLeaseCommitBarrier.WorkflowActivationCommitted => Activated.EnterAsync(fact, deadline),
                _ => ValueTask.CompletedTask
            };

        internal void Resume() { Reserved.Resume(); Activated.Resume(); }
    }

    private sealed class StopAfterConflict(CancellationTokenSource stop) : IDurableDriverObserver
    {
        internal bool SawConflict { get; private set; }
        public ValueTask OnSegmentCompletedAsync(DurableDriverSegmentObservation observation, CancellationToken cancellationToken)
        {
            if (observation.Outcome == nameof(DurableSegmentOutcome.Conflict))
            {
                SawConflict = true;
                stop.Cancel();
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask OnContinuationStartedAsync(DurableContinuationObservation observation, CancellationToken cancellationToken) =>
            ValueTask.CompletedTask;
    }
}
