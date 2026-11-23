using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using DotNet.Testcontainers.Containers;
using OrcaCore.Abstractions.Providers;
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
    public Task PostgreSqlProcessRestart(bool loseAcknowledgement) =>
        RunProcessRestartAsync(RestartDatabase.PostgreSql, loseAcknowledgement);

    [Theory(Explicit = true)]
    [InlineData(true)]
    [InlineData(false)]
    public Task SqlServerProcessRestart(bool loseAcknowledgement) =>
        RunProcessRestartAsync(RestartDatabase.SqlServer, loseAcknowledgement);

    private static async Task RunProcessRestartAsync(RestartDatabase provider, bool loseAcknowledgement)
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
                loseAcknowledgement, token);
            var recoveryPid = await RunRestartWorkerAsync("recover", provider, database.GetConnectionString(), directory.FullName,
                loseAcknowledgement, token);
            recoveryPid.Should().NotBe(seedPid);
            var observation = JsonSerializer.Deserialize<RecoveryObservation>(
                await File.ReadAllTextAsync(Path.Combine(directory.FullName, "recovered.json"), token))!;
            observation.WorkerPid.Should().Be(recoveryPid);
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"{provider}: killed worker {seedPid}, recovered in {recoveryPid}. ChildJoined={observation.JoinCount}, " +
                $"B retained={observation.SiblingChildId:D}, attempt={observation.Attempt}, notice={observation.NoticeState}.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<int> RunRestartWorkerAsync(string phase, RestartDatabase provider, string connection, string directory,
        bool loseAcknowledgement, CancellationToken token)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(DagChildRecoveryTests).Assembly.Location);
        start.ArgumentList.Add("-explicit");
        start.ArgumentList.Add("only");
        start.ArgumentList.Add("-method");
        start.ArgumentList.Add($"{typeof(DagChildRecoveryTests).FullName}.{nameof(DatabaseRestartWorker)}");
        start.Environment["ORCA_DAG_RESTART_PROVIDER"] = provider.ToString();
        start.Environment["ORCA_DAG_RESTART_PHASE"] = phase;
        start.Environment["ORCA_DAG_RESTART_CONNECTION"] = connection;
        start.Environment["ORCA_DAG_RESTART_DIRECTORY"] = directory;
        start.Environment["ORCA_DAG_RESTART_LOST_ACK"] = loseAcknowledgement.ToString();
        using var worker = Process.Start(start) ?? throw new InvalidOperationException("Failed to launch DAG worker.");
        var stdout = worker.StandardOutput.ReadToEndAsync(token);
        var stderr = worker.StandardError.ReadToEndAsync(token);
        try
        {
            if (phase == "seed")
            {
                var marker = Path.Combine(directory, "seed.json");
                while (!File.Exists(marker) && !worker.HasExited)
                    await Task.Delay(50, token);

                if (worker.HasExited)
                    throw new InvalidOperationException($"Seed exited before the crash boundary: {await stdout} {await stderr}");

                var coordinates = JsonSerializer.Deserialize<RestartCoordinates>(await File.ReadAllTextAsync(marker, token))!;
                coordinates.SeedPid.Should().Be(worker.Id);
                worker.Kill(entireProcessTree: true);
            }
            await worker.WaitForExitAsync(token);
            if (phase == "recover")
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
