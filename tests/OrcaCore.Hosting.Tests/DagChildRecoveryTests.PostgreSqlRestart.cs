using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DagChildRecoveryTests
{
    private sealed record RestartCoordinates(int SeedPid, Guid RunId, Guid? OriginalClaimId);

    // Run explicitly with the standalone xUnit runner:
    // dotnet run -c Release --project tests/OrcaCore.Hosting.Tests -- -explicit only -method "OrcaCore.Hosting.Tests.DagChildRecoveryTests.PostgreSqlProcessRestart"
    [Theory(Explicit = true)]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PostgreSqlProcessRestart(bool loseAcknowledgement)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        var token = deadline.Token;
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await postgres.StartAsync(token);
        deadline.CancelAfter(TimeSpan.FromMinutes(2));
        var directory = Directory.CreateTempSubdirectory("orca-dag-restart-");
        try
        {
            var seedPid = await RunRestartWorkerAsync("seed", postgres.GetConnectionString(), directory.FullName,
                loseAcknowledgement, token);
            var recoveryPid = await RunRestartWorkerAsync("recover", postgres.GetConnectionString(), directory.FullName,
                loseAcknowledgement, token);
            recoveryPid.Should().NotBe(seedPid);
            TestContext.Current.TestOutputHelper!.WriteLine($"Killed worker {seedPid}, recovered in {recoveryPid}.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<int> RunRestartWorkerAsync(string phase, string connection, string directory,
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
        start.ArgumentList.Add($"{typeof(DagChildRecoveryTests).FullName}.{nameof(PostgreSqlRestartWorker)}");
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
            {
                worker.ExitCode.Should().Be(0, "{0} {1}", await stdout, await stderr);
                (await File.ReadAllTextAsync(Path.Combine(directory, "recovered.pid"), token))
                    .Should().Be(worker.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
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
