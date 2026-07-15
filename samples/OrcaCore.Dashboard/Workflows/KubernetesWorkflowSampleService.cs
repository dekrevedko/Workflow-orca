using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaEventId = OrcaCore.Abstractions.Ids.EventId;

namespace OrcaCore.Dashboard.Workflows;

public sealed class KubernetesWorkflowSampleService(
    DurableWorkflowRuntime runtime,
    DurableCommandProcessor processor,
    TimeProvider timeProvider,
    ILogger<KubernetesWorkflowSampleService> logger) : BackgroundService
{
    private static readonly TimeSpan ScheduledInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan KubectlTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CompletionGrace = TimeSpan.FromMinutes(5);

    // Durable definition ids must be stable across process restarts: persisted instances and
    // start-idempotency records reference them forever. A random id here would strand every
    // instance started by a previous run of this host.
    private static readonly DefinitionId KubernetesDefinitionId =
        new(Guid.Parse("6f9f4b64-3c1e-4f5a-9b2d-7d1a52e08b31"));

    // The definition is deliberately Init -> End with no steps: this sample models the
    // external-job pattern, where kubectl-driven jobs are attached to the instance through
    // RunExternalJobCommand/CompleteExternalJobCommand rather than executed as workflow steps.
    private static readonly WorkflowDefinition<KubernetesWorkflowState> Definition =
        Workflow.Durable<KubernetesWorkflowState>(KubernetesDefinitionId, DefinitionVersion.Initial)
            .Init<KubernetesWorkflowInput>(KubernetesWorkflowState.From)
            .End("KubernetesWorkflowTracked")
            .Build();

    private readonly object gate = new();
    private readonly List<MutableWorkflowRun> runs = [];
    private bool kubectlAvailable;
    private DateTimeOffset? lastSchedulerTick;
    private string? lastError;

    public KubernetesSampleSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new KubernetesSampleSnapshot(
                kubectlAvailable,
                "default",
                ScheduledInterval,
                lastSchedulerTick,
                lastError,
                runs
                    .OrderByDescending(run => run.CreatedAt)
                    .Select(ToView)
                    .ToArray());
        }
    }

    public Task<KubernetesSampleSnapshot> StartScheduledJobAsync(CancellationToken cancellationToken)
    {
        var run = CreateRun(
            KubernetesWorkflowKind.Scheduled,
            "scheduled job",
            [CreateJobPlan("scheduled", RandomDurationSeconds(1, 15), [])]);
        QueueRun(run.Id, cancellationToken);
        return Task.FromResult(GetSnapshot());
    }

    public Task<KubernetesSampleSnapshot> StartDependencyWorkflowAsync(CancellationToken cancellationToken)
    {
        var count = Random.Shared.Next(2, 6);
        var plans = new List<KubernetesJobPlan>(count);
        for (var index = 0; index < count; index++)
        {
            var name = $"dep-{index + 1}";
            var dependsOn = index == 0 ? [] : new[] { plans[index - 1].Name };
            plans.Add(CreateJobPlan(name, RandomDurationSeconds(1, 3), dependsOn));
        }

        var run = CreateRun(
            KubernetesWorkflowKind.Dependency,
            $"{count} dependent jobs",
            plans);
        QueueRun(run.Id, cancellationToken);
        return Task.FromResult(GetSnapshot());
    }

    public Task<KubernetesSampleSnapshot> StartDagWorkflowAsync(CancellationToken cancellationToken)
    {
        var count = Random.Shared.Next(3, 10);
        var plans = new List<KubernetesJobPlan>(count);
        for (var index = 0; index < count; index++)
        {
            var name = $"dag-{index + 1}";
            var dependsOn = index == 0
                ? []
                : plans
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(Random.Shared.Next(0, Math.Min(2, index) + 1))
                    .Select(plan => plan.Name)
                    .ToArray();
            plans.Add(CreateJobPlan(name, RandomDurationSeconds(1, 3), dependsOn));
        }

        var run = CreateRun(KubernetesWorkflowKind.Dag, $"{count} DAG jobs", plans);
        QueueRun(run.Id, cancellationToken);
        return Task.FromResult(GetSnapshot());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await StartScheduledJobAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(ScheduledInterval, timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            SetSchedulerTick();
            await StartScheduledJobAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private void QueueRun(string runId, CancellationToken cancellationToken)
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await ExecuteRunAsync(runId, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    MarkRunFailed(runId, "Run cancelled.");
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Kubernetes sample run {RunId} failed.", runId);
                    MarkRunFailed(runId, exception.Message);
                }
            },
            CancellationToken.None);
    }

    private async Task ExecuteRunAsync(string runId, CancellationToken cancellationToken)
    {
        if (!await EnsureKubernetesReadyAsync(cancellationToken).ConfigureAwait(false))
        {
            MarkRunFailed(runId, lastError ?? "kubectl is not available.");
            return;
        }

        var run = GetRun(runId);
        var durable = await runtime.StartOrGetAsync<KubernetesWorkflowInput, KubernetesWorkflowState>(
            $"kubernetes/{run.Id}",
            Definition,
            new KubernetesWorkflowInput(run.Id, run.Kind.ToString(), run.Summary),
            cancellationToken).ConfigureAwait(false);
        SetDurableInstance(runId, durable.InstanceId);

        if (run.Kind == KubernetesWorkflowKind.Dag)
        {
            await ExecuteDagAsync(runId, durable.InstanceId, cancellationToken).ConfigureAwait(false);
            return;
        }

        foreach (var node in GetRun(runId).Nodes)
        {
            if (!await ExecuteNodeAsync(runId, node.Name, durable.InstanceId, cancellationToken).ConfigureAwait(false))
            {
                MarkRunFailed(runId, $"Job {node.Name} failed.");
                return;
            }
        }

        MarkRunSucceeded(runId);
    }

    // Readiness/fan-out is hand-rolled here because these DAG nodes are external kubectl jobs on
    // one instance, not durable child workflows; the library's WorkflowDagBuilder/DurableDagRunner
    // pair covers the child-workflow shape (each node its own durable instance).
    private async Task ExecuteDagAsync(
        string runId,
        InstanceId durableInstanceId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var run = GetRun(runId);
            var pending = run.Nodes
                .Where(node => node.Status == KubernetesJobStatus.Pending)
                .ToArray();
            if (pending.Length == 0)
            {
                MarkRunSucceeded(runId);
                return;
            }

            var completed = run.Nodes
                .Where(node => node.Status == KubernetesJobStatus.Succeeded)
                .Select(node => node.Name)
                .ToHashSet(StringComparer.Ordinal);
            var ready = pending
                .Where(node => node.DependsOn.All(completed.Contains))
                .ToArray();
            if (ready.Length == 0)
            {
                MarkRunFailed(runId, "DAG has no runnable nodes.");
                return;
            }

            var results = await Task.WhenAll(ready.Select(node =>
                ExecuteNodeAsync(runId, node.Name, durableInstanceId, cancellationToken))).ConfigureAwait(false);
            if (results.Any(result => !result))
            {
                MarkRunFailed(runId, "One or more DAG jobs failed.");
                return;
            }
        }
    }

    private async Task<bool> ExecuteNodeAsync(
        string runId,
        string nodeName,
        InstanceId durableInstanceId,
        CancellationToken cancellationToken)
    {
        var node = GetNode(runId, nodeName);
        MarkNodeRunning(runId, nodeName);

        await processor.ProcessAsync(
            new RunExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = durableInstanceId,
                RequestedAt = timeProvider.GetUtcNow(),
                ExternalJobId = node.Name,
                Payload = JsonSerializer.SerializeToUtf8Bytes(new KubernetesJobPayload(
                    runId,
                    node.Name,
                    node.DurationSeconds,
                    node.DependsOn)),
                TimeoutAt = timeProvider.GetUtcNow()
                    .AddSeconds(node.DurationSeconds)
                    .Add(CompletionGrace)
            },
            cancellationToken).ConfigureAwait(false);

        var apply = await RunKubectlAsync(
            ["apply", "-f", "-"],
            JobManifest(runId, node),
            KubectlTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!apply.Success)
        {
            await MarkExternalJobTimedOutAsync(durableInstanceId, node.Name, cancellationToken).ConfigureAwait(false);
            MarkNodeFailed(runId, nodeName, apply.ErrorText);
            return false;
        }

        var wait = await RunKubectlAsync(
            [
                "wait",
                "--namespace",
                "default",
                "--for=condition=complete",
                $"job/{node.Name}",
                $"--timeout={node.DurationSeconds + (int)CompletionGrace.TotalSeconds}s"
            ],
            null,
            TimeSpan.FromSeconds(node.DurationSeconds).Add(CompletionGrace).Add(KubectlTimeout),
            cancellationToken).ConfigureAwait(false);
        if (!wait.Success)
        {
            await MarkExternalJobTimedOutAsync(durableInstanceId, node.Name, cancellationToken).ConfigureAwait(false);
            MarkNodeFailed(runId, nodeName, wait.ErrorText);
            return false;
        }

        await processor.ProcessAsync(
            new CompleteExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = durableInstanceId,
                RequestedAt = timeProvider.GetUtcNow(),
                ExternalJobId = node.Name,
                CompletionEventId = OrcaEventId.New()
            },
            cancellationToken).ConfigureAwait(false);
        MarkNodeSucceeded(runId, nodeName);
        return true;
    }

    private async Task MarkExternalJobTimedOutAsync(
        InstanceId durableInstanceId,
        string externalJobId,
        CancellationToken cancellationToken)
    {
        await processor.ProcessAsync(
            new TimeoutExternalJobCommand
            {
                CommandId = CommandId.New(),
                InstanceId = durableInstanceId,
                RequestedAt = timeProvider.GetUtcNow(),
                ExternalJobId = externalJobId
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> EnsureKubernetesReadyAsync(CancellationToken cancellationToken)
    {
        var version = await RunKubectlAsync(
            ["version", "--client=true"],
            null,
            KubectlTimeout,
            cancellationToken).ConfigureAwait(false);
        if (!version.Success)
        {
            SetKubernetesStatus(false, version.ErrorText);
            return false;
        }

        var cluster = await RunKubectlAsync(
            ["get", "namespace", "default"],
            null,
            KubectlTimeout,
            cancellationToken).ConfigureAwait(false);
        SetKubernetesStatus(cluster.Success, cluster.Success ? null : cluster.ErrorText);
        return cluster.Success;
    }

    private MutableWorkflowRun CreateRun(
        KubernetesWorkflowKind kind,
        string summary,
        IReadOnlyList<KubernetesJobPlan> plans)
    {
        var id = $"k8s-{Guid.CreateVersion7():N}"[..18];
        var run = new MutableWorkflowRun(
            id,
            kind,
            summary,
            KubernetesRunStatus.Pending,
            timeProvider.GetUtcNow(),
            null,
            null,
            null,
            plans
                .Select(plan => new MutableJobNode(
                    JobName(id, plan.Name),
                    plan.Name,
                    KubernetesJobStatus.Pending,
                    plan.DurationSeconds,
                    plan.DependsOn.Select(dependency => JobName(id, dependency)).ToArray(),
                    null,
                    null,
                    null))
                .ToList());

        lock (gate)
        {
            runs.Add(run);
        }

        return run;
    }

    private static KubernetesJobPlan CreateJobPlan(
        string name,
        int durationSeconds,
        IReadOnlyList<string> dependsOn)
    {
        return new KubernetesJobPlan(name, durationSeconds, dependsOn);
    }

    private MutableWorkflowRun GetRun(string runId)
    {
        lock (gate)
        {
            return runs.Single(run => run.Id == runId);
        }
    }

    private KubernetesJobView GetNode(string runId, string nodeName)
    {
        lock (gate)
        {
            return ToView(runs.Single(run => run.Id == runId).Nodes.Single(node => node.Name == nodeName));
        }
    }

    private void SetDurableInstance(string runId, InstanceId instanceId)
    {
        lock (gate)
        {
            var run = runs.Single(candidate => candidate.Id == runId);
            run.DurableInstanceId = instanceId.ToString();
            run.Status = KubernetesRunStatus.Running;
            run.StartedAt = timeProvider.GetUtcNow();
        }
    }

    private void MarkRunSucceeded(string runId)
    {
        lock (gate)
        {
            var run = runs.Single(candidate => candidate.Id == runId);
            run.Status = KubernetesRunStatus.Succeeded;
            run.CompletedAt = timeProvider.GetUtcNow();
        }
    }

    private void MarkRunFailed(string runId, string error)
    {
        lock (gate)
        {
            var run = runs.SingleOrDefault(candidate => candidate.Id == runId);
            if (run is null)
            {
                return;
            }

            run.Status = KubernetesRunStatus.Failed;
            run.CompletedAt = timeProvider.GetUtcNow();
            run.Error = error;
            lastError = error;
        }
    }

    private void MarkNodeRunning(string runId, string nodeName)
    {
        lock (gate)
        {
            var node = runs.Single(run => run.Id == runId).Nodes.Single(candidate => candidate.Name == nodeName);
            node.Status = KubernetesJobStatus.Running;
            node.StartedAt = timeProvider.GetUtcNow();
        }
    }

    private void MarkNodeSucceeded(string runId, string nodeName)
    {
        lock (gate)
        {
            var node = runs.Single(run => run.Id == runId).Nodes.Single(candidate => candidate.Name == nodeName);
            node.Status = KubernetesJobStatus.Succeeded;
            node.CompletedAt = timeProvider.GetUtcNow();
        }
    }

    private void MarkNodeFailed(string runId, string nodeName, string error)
    {
        lock (gate)
        {
            var node = runs.Single(run => run.Id == runId).Nodes.Single(candidate => candidate.Name == nodeName);
            node.Status = KubernetesJobStatus.Failed;
            node.CompletedAt = timeProvider.GetUtcNow();
            node.Error = error;
            lastError = error;
        }
    }

    private void SetSchedulerTick()
    {
        lock (gate)
        {
            lastSchedulerTick = timeProvider.GetUtcNow();
        }
    }

    private void SetKubernetesStatus(bool available, string? error)
    {
        lock (gate)
        {
            kubectlAvailable = available;
            lastError = error;
        }
    }

    private static KubernetesWorkflowRunView ToView(MutableWorkflowRun run)
    {
        return new KubernetesWorkflowRunView(
            run.Id,
            run.Kind,
            run.Summary,
            run.Status,
            run.CreatedAt,
            run.StartedAt,
            run.CompletedAt,
            run.DurableInstanceId,
            run.Error,
            run.Nodes.Select(ToView).ToArray());
    }

    private static KubernetesJobView ToView(MutableJobNode node)
    {
        return new KubernetesJobView(
            node.Name,
            node.Label,
            node.Status,
            node.DurationSeconds,
            node.DependsOn,
            node.StartedAt,
            node.CompletedAt,
            node.Error);
    }

    private static string JobManifest(string runId, KubernetesJobView node)
    {
        var runLabel = runId.Replace('_', '-');
        return $$"""
apiVersion: batch/v1
kind: Job
metadata:
  name: {{node.Name}}
  namespace: default
  labels:
    app.kubernetes.io/name: orca-core-sample
    app.kubernetes.io/component: workflow-job
    orca.workflow/run: {{runLabel}}
spec:
  ttlSecondsAfterFinished: 900
  backoffLimit: 0
  template:
    metadata:
      labels:
        app.kubernetes.io/name: orca-core-sample
        orca.workflow/run: {{runLabel}}
    spec:
      restartPolicy: Never
      containers:
      - name: worker
        image: busybox:1.36.1
        command: ["/bin/sh", "-c"]
        args:
        - "echo orca {{node.Name}} started; sleep {{node.DurationSeconds}}; echo orca {{node.Name}} completed"
""";
    }

    private static async Task<KubectlResult> RunKubectlAsync(
        IReadOnlyList<string> arguments,
        string? standardInput,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var startInfo = new ProcessStartInfo
        {
            FileName = "kubectl",
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new KubectlResult(false, string.Empty, "kubectl did not start.");
            }

            if (standardInput is not null)
            {
                await process.StandardInput.WriteAsync(standardInput.AsMemory(), timeoutCts.Token).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            var output = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var error = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            return new KubectlResult(
                process.ExitCode == 0,
                await output.ConfigureAwait(false),
                await error.ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new KubectlResult(false, string.Empty, $"kubectl timed out after {timeout.TotalSeconds:0} seconds.");
        }
        catch (Win32Exception exception)
        {
            return new KubectlResult(false, string.Empty, exception.Message);
        }
    }

    private static int RandomDurationSeconds(int minMinutes, int maxMinutes)
    {
        return Random.Shared.Next(minMinutes * 60, (maxMinutes * 60) + 1);
    }

    private static string JobName(string runId, string label)
    {
        var cleaned = new StringBuilder();
        foreach (var character in $"{runId}-{label}".ToLowerInvariant())
        {
            cleaned.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        }

        var value = cleaned.ToString().Trim('-');
        return value[..Math.Min(50, value.Length)];
    }

    private sealed record KubernetesJobPlan(
        string Name,
        int DurationSeconds,
        IReadOnlyList<string> DependsOn);

    private sealed record MutableWorkflowRun(
        string Id,
        KubernetesWorkflowKind Kind,
        string Summary,
        KubernetesRunStatus Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        string? Error,
        List<MutableJobNode> Nodes)
    {
        public KubernetesRunStatus Status { get; set; } = Status;

        public DateTimeOffset? StartedAt { get; set; } = StartedAt;

        public DateTimeOffset? CompletedAt { get; set; } = CompletedAt;

        public string? Error { get; set; } = Error;

        public string? DurableInstanceId { get; set; }
    }

    private sealed record MutableJobNode(
        string Name,
        string Label,
        KubernetesJobStatus Status,
        int DurationSeconds,
        IReadOnlyList<string> DependsOn,
        DateTimeOffset? StartedAt,
        DateTimeOffset? CompletedAt,
        string? Error)
    {
        public KubernetesJobStatus Status { get; set; } = Status;

        public DateTimeOffset? StartedAt { get; set; } = StartedAt;

        public DateTimeOffset? CompletedAt { get; set; } = CompletedAt;

        public string? Error { get; set; } = Error;
    }

    private sealed record KubernetesWorkflowInput(string RunId, string Kind, string Summary);

    private sealed class KubernetesWorkflowState
    {
        public string RunId { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Summary { get; set; } = string.Empty;

        public static KubernetesWorkflowState From(KubernetesWorkflowInput input)
        {
            return new KubernetesWorkflowState
            {
                RunId = input.RunId,
                Kind = input.Kind,
                Summary = input.Summary
            };
        }
    }

    private sealed record KubernetesJobPayload(
        string RunId,
        string JobName,
        int DurationSeconds,
        IReadOnlyList<string> DependsOn);

    private sealed record KubectlResult(bool Success, string Output, string Error)
    {
        public string ErrorText => string.IsNullOrWhiteSpace(Error)
            ? Output
            : Error;
    }
}

public sealed record KubernetesSampleSnapshot(
    bool KubectlAvailable,
    string Namespace,
    TimeSpan ScheduledInterval,
    DateTimeOffset? LastSchedulerTick,
    string? LastError,
    IReadOnlyList<KubernetesWorkflowRunView> Runs);

public sealed record KubernetesWorkflowRunView(
    string Id,
    KubernetesWorkflowKind Kind,
    string Summary,
    KubernetesRunStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? DurableInstanceId,
    string? Error,
    IReadOnlyList<KubernetesJobView> Nodes);

public sealed record KubernetesJobView(
    string Name,
    string Label,
    KubernetesJobStatus Status,
    int DurationSeconds,
    IReadOnlyList<string> DependsOn,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error);

public enum KubernetesWorkflowKind
{
    Scheduled,
    Dependency,
    Dag
}

public enum KubernetesRunStatus
{
    Pending,
    Running,
    Succeeded,
    Failed
}

public enum KubernetesJobStatus
{
    Pending,
    Running,
    Succeeded,
    Failed
}
