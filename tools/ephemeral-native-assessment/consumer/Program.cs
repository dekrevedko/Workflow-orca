using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using OrcaCore;

// Report I/O deliberately uses Utf8JsonWriter. It cannot configure the product codec.
var mode = args.Length == 0 ? "--all" : args[0];
var selected = mode == "--case" ? [args[1]] : mode == "--measure"
    ? Cases.Measured : Cases.Ids;
var results = new List<CaseResult>();
Owner? bootstrap = null;
try
{
    if (mode == "--measure")
    {
        bootstrap = new Owner(new Session());
        bootstrap.Registry.Register(Cases.ReferenceDefinition()).GetHandleOrThrow();
        Console.WriteLine("ORCA_READY");
        Console.Out.Flush();
    }
    foreach (var id in selected)
    {
        Probe.Phase = "Setup";
        Probe.Checks = 0;
        Probe.Observations = [];
        try
        {
            await Cases.Run(id).WaitAsync(TimeSpan.FromSeconds(45));
            results.Add(new(id, "Passed", Probe.Checks, Probe.Phase, Probe.Observations.ToArray(), null));
        }
        catch (Exception error)
        {
            results.Add(new(id, "FailedBehavior", Probe.Checks, Probe.Phase, Probe.Observations.ToArray(), error));
        }
    }
    if (bootstrap is not null)
    {
        await bootstrap.Join();
        Console.WriteLine("ORCA_QUIESCENT");
        Console.Out.Flush();
    }
    using var output = new MemoryStream();
    using (var json = new Utf8JsonWriter(output))
    {
        json.WriteStartObject();
        json.WriteNumber("schemaVersion", 1);
        json.WriteBoolean("jsonReflectionEnabled", JsonSerializer.IsReflectionEnabledByDefault);
        json.WriteString("mode", mode);
        json.WriteStartArray("cases");
        foreach (var result in results)
        {
            json.WriteStartObject();
            json.WriteString("id", result.Id); json.WriteString("outcome", result.Outcome);
            json.WriteNumber("assertions", result.Checks); json.WriteString("phase", result.Phase);
            json.WriteStartArray("observations");
            foreach (var observation in result.Observations) json.WriteStringValue(observation);
            json.WriteEndArray();
            json.WriteStartArray("exceptions");
            for (var error = result.Error; error is not null; error = error.InnerException)
            {
                json.WriteStartObject();
                json.WriteString("type", error.GetType().FullName);
                json.WriteString("message", error.Message);
                json.WriteString("code", (error as OrcaCoreException)?.Code);
                json.WriteString("detail", error.ToString());
                json.WriteEndObject();
            }
            json.WriteEndArray(); json.WriteEndObject();
        }
        json.WriteEndArray();
        using var process = Process.GetCurrentProcess();
        process.Refresh();
        json.WriteStartObject("quiescentCounters");
        json.WriteString("phase", mode == "--measure"
            ? "physical join complete, bootstrap provider alive, before disposal"
            : "after individual case cleanup; informational counters, no retained bootstrap owner");
        json.WriteNumber("cumulativeUserPlusKernelCpuMs", process.TotalProcessorTime.TotalMilliseconds);
        json.WriteNumber("peakWorkingSetBytes", process.PeakWorkingSet64);
        json.WriteNumber("workingSetBytes", process.WorkingSet64);
        if (OperatingSystem.IsWindows()) json.WriteNumber("windowsPrivateCommitBytes", process.PrivateMemorySize64);
        if (OperatingSystem.IsLinux())
        {
            var privateBytes = File.ReadLines("/proc/self/smaps_rollup")
                .Where(line => line.StartsWith("Private_Clean:", StringComparison.Ordinal) || line.StartsWith("Private_Dirty:", StringComparison.Ordinal))
                .Sum(line => long.Parse(line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1], CultureInfo.InvariantCulture) * 1024);
            json.WriteNumber("linuxPrivateResidentBytes", privateBytes);
        }
        json.WriteNumber("estimatedManagedBytesNoForcedGc", GC.GetTotalMemory(false));
        json.WriteNumber("lastCollectionHeapBytes", GC.GetGCMemoryInfo().HeapSizeBytes);
        json.WriteStartArray("collectionCounts");
        for (var generation = 0; generation <= GC.MaxGeneration; generation++) json.WriteNumberValue(GC.CollectionCount(generation));
        json.WriteEndArray(); json.WriteEndObject();
        json.WriteStartArray("loadedModulePaths");
        foreach (ProcessModule module in process.Modules) json.WriteStringValue(module.FileName);
        json.WriteEndArray(); json.WriteEndObject();
    }
    Console.WriteLine("ORCA_REPORT " + System.Text.Encoding.UTF8.GetString(output.ToArray()));
    Console.Out.Flush();
    if (mode == "--measure" && Console.ReadLine() != "release") throw new InvalidOperationException("Runner did not release the retained owner.");
    if (bootstrap is not null) await bootstrap.DisposeAsync();
    return results.All(result => result.Outcome == "Passed") ? 0 : 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    if (bootstrap is not null) await bootstrap.DisposeAsync();
    return 2;
}

internal sealed record CaseResult(string Id, string Outcome, int Checks, string Phase, string[] Observations, Exception? Error);

public static class Probe
{
    public static string Phase = "Setup";
    public static int Checks;
    public static List<string> Observations = [];
    public static readonly TimeSpan Guard = TimeSpan.FromSeconds(20);
    public static void Check(bool value, string assertion)
    {
        if (!value) throw new InvalidOperationException("ASSERTION: " + assertion);
        Checks++;
    }
    public static async Task<Exception?> Error(Func<Task> action)
    {
        try { await action(); return null; } catch (Exception error) { return error; }
    }
}
