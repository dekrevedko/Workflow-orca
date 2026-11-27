using System.Diagnostics;
using System.Text.Json;
using AwesomeAssertions;
using DotNet.Testcontainers.Containers;
using OrcaCore.TestSupport;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class LeaseConfirmationRaceTests
{
    [Theory(Explicit = true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [Trait(Traits.Container, "PostgreSql")]
    public Task PostgreSqlProcessRestart(bool stale, bool keepWinnerAlive = false) =>
        RunConfirmationRestartAsync("PostgreSql", stale, keepWinnerAlive);

    [Theory(Explicit = true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [Trait(Traits.Container, "SqlServer")]
    public Task SqlServerProcessRestart(bool stale, bool keepWinnerAlive = false) =>
        RunConfirmationRestartAsync("SqlServer", stale, keepWinnerAlive);

    private static async Task RunConfirmationRestartAsync(string provider, bool stale, bool keepWinnerAlive)
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
        var directory = Directory.CreateTempSubdirectory("orca-confirmation-");
        try
        {
            ConfirmationWorker Start(string phase) =>
                new(phase, provider, database.GetConnectionString(), directory.FullName, stale, keepWinnerAlive);

            await using var winner = Start("winner");
            await winner.WaitForMarkerAsync("seed.json", token);
            await using var early = stale ? Start("contender") : null;
            if (early is not null)
                await early.WaitForMarkerAsync("prepared.json", token);

            await File.WriteAllTextAsync(Path.Combine(directory.FullName, "allow-winner"), "", token);
            await winner.WaitForMarkerAsync("committed.json", token);
            if (!keepWinnerAlive)
                await winner.KillAsync();

            await using var fresh = stale ? null : Start("contender");
            var contender = early ?? fresh!;
            if (stale)
                await File.WriteAllTextAsync(Path.Combine(directory.FullName, "allow-contender"), "", token);
            await contender.ExpectPassAsync("contender.json", token);

            var winnerResult = "Killed after governance commit";
            if (keepWinnerAlive)
            {
                await File.WriteAllTextAsync(Path.Combine(directory.FullName, "allow-winner-return"), "", token);
                winnerResult = await winner.ExpectPassAsync("winner-returned.json", token);
            }

            await using var verifier = Start("verify");
            var result = await verifier.ExpectPassAsync("verified.json", token);
            new[] { winner.Id, contender.Id, verifier.Id }.Should().OnlyHaveUniqueItems();
            var output = TestContext.Current.TestOutputHelper!;
            output.WriteLine($"{provider}, stale={stale}, keepWinnerAlive={keepWinnerAlive}. " +
                $"A={winner.Id} ({winnerResult}), B={contender.Id}, fresh verification={result}");
            if (!keepWinnerAlive)
            {
                await using var repair = Start("repair");
                var legacyResult = await repair.ExpectPassAsync("legacy-repair.json", token);
                new[] { winner.Id, contender.Id, verifier.Id }.Should().NotContain(repair.Id);
                output.WriteLine($"Synthetic legacy tombstone repair={legacyResult}");
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private sealed class ConfirmationWorker : IAsyncDisposable
    {
        private readonly Process process;
        private readonly string directory;
        private readonly Task<string> stdout;
        private readonly Task<string> stderr;

        public ConfirmationWorker(string phase, string provider, string connection, string directory, bool stale, bool keepWinnerAlive)
        {
            this.directory = directory;
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in new[] { typeof(LeaseConfirmationRaceTests).Assembly.Location,
                "-explicit", "only", "-method", $"{typeof(LeaseConfirmationRaceTests).FullName}.{nameof(DatabaseWorker)}" })
                start.ArgumentList.Add(argument);
            start.Environment["ORCA_CONFIRM_PHASE"] = phase;
            start.Environment["ORCA_CONFIRM_DB"] = provider;
            start.Environment["ORCA_CONFIRM_CONNECTION"] = connection;
            start.Environment["ORCA_CONFIRM_DIRECTORY"] = directory;
            start.Environment["ORCA_CONFIRM_STALE"] = stale.ToString();
            start.Environment["ORCA_CONFIRM_KEEP_WINNER_ALIVE"] = keepWinnerAlive.ToString();
            process = Process.Start(start) ?? throw new InvalidOperationException("Could not start confirmation worker.");
            stdout = process.StandardOutput.ReadToEndAsync();
            stderr = process.StandardError.ReadToEndAsync();
        }

        public int Id => process.Id;

        public async Task WaitForMarkerAsync(string name, CancellationToken token)
        {
            while (!File.Exists(Path.Combine(directory, name)))
            {
                if (process.HasExited)
                    throw new InvalidOperationException($"Worker exited before {name}: {await stdout} {await stderr}");
                await Task.Delay(TimeSpan.FromMilliseconds(50), TimeProvider.System, token);
            }
            await ReadMarkerAsync(name, token);
        }

        public async Task<string> ExpectPassAsync(string name, CancellationToken token)
        {
            await process.WaitForExitAsync(token);
            process.ExitCode.Should().Be(0, "{0}\n{1}", await stdout, await stderr);
            return await ReadMarkerAsync(name, token);
        }

        private async Task<string> ReadMarkerAsync(string name, CancellationToken token)
        {
            var json = await File.ReadAllTextAsync(Path.Combine(directory, name), token);
            using var marker = JsonDocument.Parse(json);
            marker.RootElement.GetProperty("WorkerPid").GetInt32().Should().Be(Id);
            return json;
        }

        public async Task KillAsync()
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (!process.HasExited)
                    await KillAsync();
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    public static bool IsWorker => Environment.GetEnvironmentVariable("ORCA_CONFIRM_PHASE") is "winner" or "contender" or "verify" or "repair";

    [Fact(Explicit = true, SkipUnless = nameof(IsWorker), Skip = "Invoked by the confirmation restart controller.")]
    public Task DatabaseWorker() => RunConfirmationScenarioAsync(
        Environment.GetEnvironmentVariable("ORCA_CONFIRM_PHASE")!,
        Environment.GetEnvironmentVariable("ORCA_CONFIRM_DB")!,
        Environment.GetEnvironmentVariable("ORCA_CONFIRM_CONNECTION")!,
        Environment.GetEnvironmentVariable("ORCA_CONFIRM_DIRECTORY")!,
        bool.Parse(Environment.GetEnvironmentVariable("ORCA_CONFIRM_STALE")!),
        bool.Parse(Environment.GetEnvironmentVariable("ORCA_CONFIRM_KEEP_WINNER_ALIVE")!),
        TestContext.Current.CancellationToken);
}
