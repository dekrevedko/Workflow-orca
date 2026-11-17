using System.Diagnostics;
using AwesomeAssertions;

namespace OrcaCore.ProviderCertification;

public static class DagProcessRestartCertification
{
    public static async Task RunAsync(string provider, string connection, string schema, CancellationToken token)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "OrcaCore.slnx"))) root = root.Parent;
        root.Should().NotBeNull();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var probe = Path.Combine(root!.FullName, "tests", "OrcaCore.Dag.ProcessProbe", "bin", configuration, "net10.0", "OrcaCore.Dag.ProcessProbe.dll");
        File.Exists(probe).Should().BeTrue("the referenced process fixture must be built");
        var statePath = Path.Combine(Path.GetTempPath(), "dag-restart-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            foreach (var phase in new[] { "create", "incompatible", "cancel", "inspect-cancel" })
            {
                var start = new ProcessStartInfo("dotnet")
                { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
                foreach (var argument in new[] { probe, phase, provider, schema, statePath }) start.ArgumentList.Add(argument);
                start.Environment["ORCA_DAG_PROBE_CONNECTION"] = connection;
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync(token);
                var errors = process.StandardError.ReadToEndAsync(token);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
                deadline.CancelAfter(TimeSpan.FromSeconds(110));
                try { await process.WaitForExitAsync(deadline.Token); }
                catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
                process.ExitCode.Should().Be(0, "phase {0}: {1}\n{2}", phase, await output, await errors);
                (await output).Should().Contain("DAG_RESTART_PASS phase=" + phase);
            }
        }
        finally { if (File.Exists(statePath)) File.Delete(statePath); }
    }
}
