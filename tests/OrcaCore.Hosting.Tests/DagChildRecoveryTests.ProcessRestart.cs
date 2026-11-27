using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using DotNet.Testcontainers.Containers;
using OrcaCore.Abstractions.Providers;
using OrcaCore.TestSupport;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DagChildRecoveryTests
{
    private enum RestartDatabase { PostgreSql, SqlServer }
    private sealed record RestartCoordinates(int SeedPid, Guid RunId, Guid SiblingChildId, Guid? OriginalClaimId);
    private sealed record RecoveryObservation(int WorkerPid, int JoinCount, Guid SiblingChildId, int Attempt, OutboxRecordState NoticeState);
    [Theory(Explicit = true)]
    [InlineData(true)]
    [InlineData(false)]
    [Trait(Traits.Container, "PostgreSql")]
    public Task PostgreSqlProcessRestart(bool loseAcknowledgement) =>
        RunProcessRestartAsync(RestartDatabase.PostgreSql, nameof(DatabaseRestartWorker), loseAcknowledgement);

    [Theory(Explicit = true)]
    [InlineData(true)]
    [InlineData(false)]
    [Trait(Traits.Container, "SqlServer")]
    public Task SqlServerProcessRestart(bool loseAcknowledgement) =>
        RunProcessRestartAsync(RestartDatabase.SqlServer, nameof(DatabaseRestartWorker), loseAcknowledgement);

    [Theory(Explicit = true)]
    [InlineData(true)]
    [InlineData(false)]
    [Trait(Traits.Container, "PostgreSql")]
    public Task PostgreSqlChildStartCancellationProcessRestart(bool continuationFirst) =>
        RunProcessRestartAsync(RestartDatabase.PostgreSql, nameof(ChildStartCancellationWorker), continuationFirst);

    [Theory(Explicit = true)]
    [InlineData(true)]
    [InlineData(false)]
    [Trait(Traits.Container, "SqlServer")]
    public Task SqlServerChildStartCancellationProcessRestart(bool continuationFirst) =>
        RunProcessRestartAsync(RestartDatabase.SqlServer, nameof(ChildStartCancellationWorker), continuationFirst);

    private static async Task RunProcessRestartAsync(RestartDatabase provider, string method, bool schedule,
        string? seedMethod = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        var token = deadline.Token;
        await using IDatabaseContainer database = provider switch
        {
            RestartDatabase.PostgreSql => new PostgreSqlBuilder("postgres:17-alpine").Build(),
            RestartDatabase.SqlServer => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
        await database.StartAsync(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var directory = Directory.CreateTempSubdirectory("orca-dag-restart-");
        try
        {
            var seedPid = await RunRestartWorkerAsync("seed", provider, database.GetConnectionString(), directory.FullName,
                seedMethod ?? method, schedule, token);
            var recoveryPid = await RunRestartWorkerAsync("recover", provider, database.GetConnectionString(), directory.FullName,
                method, schedule, token);
            recoveryPid.Should().NotBe(seedPid);
            var result = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "recovered.json"), token);
            using var observation = JsonDocument.Parse(result);
            observation.RootElement.GetProperty("WorkerPid").GetInt32().Should().Be(recoveryPid);
            TestContext.Current.TestOutputHelper!.WriteLine($"{provider}: killed worker {seedPid}, recovered: {result}.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<int> RunRestartWorkerAsync(string phase, RestartDatabase provider, string connection, string directory,
        string method, bool schedule, CancellationToken token)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(DagChildRecoveryTests).Assembly.Location);
        start.ArgumentList.Add("-explicit");
        start.ArgumentList.Add("only");
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add($"{typeof(DagChildRecoveryTests).FullName}.{method}");
        start.Environment["ORCA_DAG_RESTART_PROVIDER"] = provider.ToString();
        start.Environment["ORCA_DAG_RESTART_PHASE"] = phase;
        start.Environment["ORCA_DAG_RESTART_CONNECTION"] = connection;
        start.Environment["ORCA_DAG_RESTART_DIRECTORY"] = directory;
        start.Environment["ORCA_DAG_RESTART_SCHEDULE"] = schedule.ToString();
        using var worker = Process.Start(start) ?? throw new InvalidOperationException("Failed to launch DAG worker.");
        var stdout = worker.StandardOutput.ReadToEndAsync(token);
        var stderr = worker.StandardError.ReadToEndAsync(token);
        try
        {
            if (phase == "seed")
            {
                var marker = Path.Combine(directory, "seed.json");
                while (!File.Exists(marker) && !worker.HasExited)
                    await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, token);

                if (worker.HasExited)
                    throw new InvalidOperationException($"Seed exited before the crash boundary: {await stdout} {await stderr}");

                using var coordinates = JsonDocument.Parse(await File.ReadAllTextAsync(marker, token));
                coordinates.RootElement.GetProperty("SeedPid").GetInt32().Should().Be(worker.Id);
                worker.Kill(entireProcessTree: true);
            }
            await worker.WaitForExitAsync(token);
            if (phase != "seed")
                worker.ExitCode.Should().Be(0, "{0} {1}", await stdout, await stderr);
            return worker.Id;
        }
        finally
        {
            if (!worker.HasExited)
            {
                worker.Kill(entireProcessTree: true);
                await worker.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
