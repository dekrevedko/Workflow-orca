[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$BaselineRevision,
    [Parameter(Mandatory)][string]$CandidateRevision,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Dotnet = 'dotnet',
    [string]$ScratchParent = [IO.Path]::GetTempPath(),
    [string]$CandidateCoordinatorSource
)
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath $Repository).ProviderPath
$exe = (Get-Command $Dotnet).Source
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw "Choose a new output directory: $outputRoot" }
function Replace-Once([string]$file,[string]$old,[string]$new) {
    $source = [IO.File]::ReadAllText($file)
    $first = $source.IndexOf($old, [StringComparison]::Ordinal)
    if ($first -lt 0 -or $first -ne $source.LastIndexOf($old, [StringComparison]::Ordinal)) {
        throw "Review hook in $file for $old"
    }
    [IO.File]::WriteAllText($file,$source.Replace($old,$new))
}
$paths = @{}
$previousRoot = $env:DOTNET_ROOT
$previousPath = $env:PATH
$previousOutput = $env:ORCA_WORKER_PROBE
try {
    $env:DOTNET_ROOT = Split-Path $exe -Parent
    $env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH
    foreach ($side in 'baseline','final') {
        $revision = if ($side -eq 'baseline') { $BaselineRevision } else { $CandidateRevision }
        $parameters = @{ Repository=$repo; Revision=$revision; OutputDirectory=(Join-Path $outputRoot $side);
            Dotnet=$exe; ScratchParent=$ScratchParent; PrepareOnly=$true }
        if ($side -eq 'final' -and $CandidateCoordinatorSource) { $parameters.CoordinatorSource=$CandidateCoordinatorSource }
        & "$PSScriptRoot/Measure.ps1" @parameters
        $scratch = (Get-Content -LiteralPath (Join-Path $parameters.OutputDirectory 'environment.json') -Raw | ConvertFrom-Json).scratch
        $paths[$side] = $scratch
        $coord = Join-Path $scratch 'src/OrcaCore.Dag.Hosting/DagCoordinator.cs'
        $turns = Join-Path $scratch 'src/OrcaCore.Durable.Hosting/Dag/DurableDagRunTurns.cs'
        $probe = Join-Path $scratch 'tests/OrcaCore.Hosting.Tests/WorkerMeasurement.cs'
        $guard = 'if (result.Outcome is DurableDagTurnOutcomeV1.StaleFence or DurableDagTurnOutcomeV1.LocalQueueFull)'
        Replace-Once $coord $guard ('(AppContext.GetData("WorkerProbe.TurnRetry") as Action<Guid,string>)?.Invoke(id,"Admission."+result.Outcome);' + [Environment]::NewLine + '                '+$guard)
        $heartbeat = 'await Task.Delay(WaiterHeartbeat, time, token).ConfigureAwait(false);'
        Replace-Once $turns $heartbeat ('(AppContext.GetData("WorkerProbe.TurnRetry") as Action<Guid,string>)?.Invoke(runId,request.Purpose+"."+result.Outcome);' + [Environment]::NewLine + '                '+$heartbeat)
        $measure = @('var delays = new ConcurrentQueue<(Guid Run, string Reason, long Timestamp)>();',
            '        AppContext.SetData("WorkerProbe.TurnRetry", (Action<Guid,string>)((id,reason) =>',
            '            delays.Enqueue((id,reason,Stopwatch.GetTimestamp()))));',
            '        foreach (var chain in new[]{ false })') -join [Environment]::NewLine
        Replace-Once $probe 'foreach (var chain in new[]{ false, true })' $measure
        $count = @('admissionMs, nextAdmissionMs, predecessorJoinToAdmissionMs,',
            '                        retryCounts = delays.Where(d => d.Run == id && d.Timestamp <= events[(id,nodes-1)])',
            '                            .GroupBy(d => d.Reason).ToDictionary(g => g.Key,g => g.Count()) });') -join [Environment]::NewLine
        Replace-Once $probe 'admissionMs, nextAdmissionMs, predecessorJoinToAdmissionMs });' $count
        Push-Location $scratch
        try {
            & $exe build tests/OrcaCore.Hosting.Tests -c Release --nologo -v:q
            if ($LASTEXITCODE -ne 0) { throw "Build failed: $side" }
        } finally { Pop-Location }
    }
    $sequence = 'baseline','final','final','baseline','final','baseline','baseline','final'
    for ($index = 0; $index -lt $sequence.Count; $index++) {
        $side = $sequence[$index]
        Push-Location $paths[$side]
        try {
            $env:ORCA_WORKER_PROBE = Join-Path $outputRoot ("{0:D2}-{1}.jsonl" -f ($index+1),$side)
            & $exe test tests/OrcaCore.Hosting.Tests -c Release --no-build --nologo -v:q --filter 'FullyQualifiedName~DagSchedulerTests.MeasureWorkerRapid'
            if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $env:ORCA_WORKER_PROBE)) { throw "Failure at $index $side" }
        } finally { Pop-Location }
    }
} finally {
    $env:DOTNET_ROOT=$previousRoot
    $env:PATH=$previousPath
    $env:ORCA_WORKER_PROBE=$previousOutput
}
