using BenchmarkDotNet.Running;
using OrcaCore.Benchmarks.Scenarios;

if (args is ["--smoke"])
{
    var execution = new EphemeralExecutionLoopBenchmarks { StepCount = 10 };
    execution.Setup();
    try
    {
        if (await execution.StartAndComplete() != 10)
            throw new InvalidOperationException("Ephemeral benchmark did not execute all steps.");
    }
    finally { execution.Cleanup(); }

    var commits = new ProviderCommitBenchmarks();
    commits.Setup();
    try { await commits.AppendCommitBatch(); }
    finally { commits.Cleanup(); }

    var serialization = new ProviderSerializationMaterializationBenchmarks();
    await serialization.SetupAsync();
    try
    {
        serialization.SerializeAndDeserializePayload();
        serialization.DeserializeExistingPayload();
        if ((await serialization.LoadTailMaterializesEvents()).Count != 128 ||
            (await serialization.LoadCheckpointAndMaterializePayload()).Sequence != 1)
            throw new InvalidOperationException("Provider materialization benchmark returned incorrect data.");
    }
    finally { serialization.Cleanup(); }

    var projections = new ManagementProjectionBenchmarks();
    await projections.SetupAsync();
    try
    {
        if (!(await projections.GetProjectedInstance()).HasValue)
            throw new InvalidOperationException("Projection benchmark did not find its seeded instance.");
        await projections.ListRecoveryCandidates();
    }
    finally { projections.Cleanup(); }

    var scheduling = new ResourcePoolTimerSchedulingBenchmarks();
    await scheduling.SetupAsync();
    try
    {
        if ((await scheduling.AcquireAndReleasePoolTicket()).ReleasedTickets.Count != 1 ||
            (await scheduling.ScheduleAndClaimDueTimer()).Count != 1)
            throw new InvalidOperationException("Resource/timer benchmark did not acquire, release, and claim.");
    }
    finally { scheduling.Cleanup(); }

    foreach (var count in new[] { 2, 8, 32 })
        new StructuredFiberExecutionBenchmarks { BranchCount = count }.CompileStructuredPlan();

    Console.WriteLine("All benchmark scenarios passed the smoke check.");
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
