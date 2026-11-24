using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AwesomeAssertions;
using DotNet.Testcontainers.Containers;
using OrcaCore.TestSupport;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class DurableStepOutcomeTests
{
    [Theory(Explicit = true)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [Trait(Traits.Container, "PostgreSql")]
    public Task PostgreSqlProcessRestart(bool loseAcknowledgement, int maxAttempts) =>
        RunProcessRestartAsync("PostgreSql", loseAcknowledgement, maxAttempts);

    [Theory(Explicit = true)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [Trait(Traits.Container, "SqlServer")]
    public Task SqlServerProcessRestart(bool loseAcknowledgement, int maxAttempts) =>
        RunProcessRestartAsync("SqlServer", loseAcknowledgement, maxAttempts);

    private static async Task RunProcessRestartAsync(string provider, bool loseAcknowledgement, int maxAttempts)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromMinutes(5));
        var token = deadline.Token;
        await using IDatabaseContainer database = provider switch
        {
            "PostgreSql" => new PostgreSqlBuilder("postgres:17-alpine").Build(),
            "SqlServer" => new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
        await database.StartAsync(token);
        var directory = Directory.CreateTempSubdirectory("orca-step-outcome-");
        try
        {
            async Task<int> RunWorkerAsync(string phase)
            {
                var start = new ProcessStartInfo("dotnet")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true
                };
                foreach (var argument in new[] { typeof(DurableStepOutcomeTests).Assembly.Location,
                    "-explicit", "only", "-method", $"{typeof(DurableStepOutcomeTests).FullName}.{nameof(DatabaseWorker)}" })
                    start.ArgumentList.Add(argument);
                start.Environment["ORCA_STEP_PHASE"] = phase;
                start.Environment["ORCA_STEP_DB"] = provider;
                start.Environment["ORCA_STEP_CONNECTION"] = database.GetConnectionString();
                start.Environment["ORCA_STEP_DIRECTORY"] = directory.FullName;
                start.Environment["ORCA_STEP_LOST_ACK"] = loseAcknowledgement.ToString();
                start.Environment["ORCA_STEP_BUDGET"] = maxAttempts.ToString(CultureInfo.InvariantCulture);

                using var worker = Process.Start(start) ?? throw new InvalidOperationException("Could not launch step worker.");
                var stdout = worker.StandardOutput.ReadToEndAsync();
                var stderr = worker.StandardError.ReadToEndAsync();
                var marker = Path.Combine(directory.FullName, phase == "seed" ? "seed.json" : "recovered.json");
                try
                {
                    if (phase == "seed")
                    {
                        while (!File.Exists(marker) && !worker.HasExited)
                            await Task.Delay(50, token);
                        if (worker.HasExited)
                            throw new InvalidOperationException($"Seed exited before the crash boundary: {await stdout} {await stderr}");
                        VerifyWorkerMarker(marker, worker.Id);
                        worker.Kill(entireProcessTree: true);
                    }

                    await worker.WaitForExitAsync(token);
                    if (phase == "recover")
                    {
                        worker.ExitCode.Should().Be(0, "{0}\n{1}", await stdout, await stderr);
                        VerifyWorkerMarker(marker, worker.Id);
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

            var seedPid = await RunWorkerAsync("seed");
            var recoveryPid = await RunWorkerAsync("recover");
            recoveryPid.Should().NotBe(seedPid);
            var result = await File.ReadAllTextAsync(Path.Combine(directory.FullName, "recovered.json"), token);
            TestContext.Current.TestOutputHelper!.WriteLine(
                $"{provider}, lost ack={loseAcknowledgement}, maxAttempts={maxAttempts}. Killed PID {seedPid}. Recovered: {result}");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static void VerifyWorkerMarker(string path, int workerPid)
    {
        using var marker = JsonDocument.Parse(File.ReadAllBytes(path));
        marker.RootElement.GetProperty("WorkerPid").GetInt32().Should().Be(workerPid);
    }

    public static bool IsWorker => Environment.GetEnvironmentVariable("ORCA_STEP_PHASE") is "seed" or "recover";

    [Fact(Explicit = true, SkipUnless = nameof(IsWorker), Skip = "Invoked by the SQL process restart tests.")]
    public Task DatabaseWorker() => RunOutcomeScenarioAsync(
        Environment.GetEnvironmentVariable("ORCA_STEP_PHASE")!,
        Environment.GetEnvironmentVariable("ORCA_STEP_DB")!,
        Environment.GetEnvironmentVariable("ORCA_STEP_CONNECTION")!,
        Environment.GetEnvironmentVariable("ORCA_STEP_DIRECTORY")!,
        bool.Parse(Environment.GetEnvironmentVariable("ORCA_STEP_LOST_ACK")!),
        int.Parse(Environment.GetEnvironmentVariable("ORCA_STEP_BUDGET")!, CultureInfo.InvariantCulture),
        TestContext.Current.CancellationToken);
}
