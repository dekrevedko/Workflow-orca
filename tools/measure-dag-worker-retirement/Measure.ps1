[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Revision,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$Dotnet = 'dotnet',
    [ValidateRange(1, 100)][int]$Rounds = 3,
    [string]$ScratchParent = [IO.Path]::GetTempPath(),
    [switch]$PrepareOnly,
    [string]$CoordinatorSource
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath $Repository).ProviderPath
$scratchRoot = (Resolve-Path -LiteralPath $ScratchParent).ProviderPath
$exe = (Get-Command $Dotnet).Source
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $outputRoot) { throw "Choose a new output directory: $outputRoot" }
$source = if ($CoordinatorSource) { (Resolve-Path -LiteralPath $CoordinatorSource).ProviderPath }
$gitTarget = (git -C $repo rev-parse --verify "$Revision^{commit}")
if ($LASTEXITCODE -ne 0) { throw "Unknown Git revision: $Revision" }
$target = $gitTarget.Trim()
$scratch = Join-Path $scratchRoot ("orca-idle-" + ([guid]::NewGuid().ToString("N")).Substring(0, 12))

New-Item -ItemType Directory -Path $outputRoot -ErrorAction Stop | Out-Null
git clone --quiet --local --shared --no-checkout -c core.longpaths=true -- $repo $scratch
if ($LASTEXITCODE -ne 0) { throw 'Scratch clone failed' }
git -C $scratch checkout --quiet --detach $target
if ($LASTEXITCODE -ne 0) { throw 'Scratch checkout failed' }
$test = Join-Path $scratch 'tests/OrcaCore.Hosting.Tests'
$fixture = Join-Path $test 'DagSchedulerTests.cs'
$fixtureText = [IO.File]::ReadAllText($fixture)
$declaration = 'public sealed class DagSchedulerTests'
$partial = 'public sealed partial class DagSchedulerTests'
if ($fixtureText.Contains($declaration)) {
    [IO.File]::WriteAllText($fixture, $fixtureText.Replace($declaration, $partial))
} elseif (!$fixtureText.Contains($partial)) { throw 'Hosting scheduler fixture changed' }
$sourceTarget = Join-Path $scratch 'src/OrcaCore.Dag.Hosting/DagCoordinator.cs'
if ($source) { Copy-Item -LiteralPath $source -Destination $sourceTarget }
$overlayTarget = Join-Path $test 'WorkerMeasurement.cs'
if (Test-Path -LiteralPath $overlayTarget) { throw "Scratch overlay target already exists: $overlayTarget" }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Probe.cs') -Destination $overlayTarget

$previousOutput = $env:ORCA_WORKER_PROBE
$previousRoot = $env:DOTNET_ROOT
$previousPath = $env:PATH
Push-Location $scratch
try {
    $env:DOTNET_ROOT = Split-Path $exe -Parent
    $env:PATH = $env:DOTNET_ROOT + [IO.Path]::PathSeparator + $env:PATH
    $coordinatorBlob = (git hash-object 'src/OrcaCore.Dag.Hosting/DagCoordinator.cs').Trim()
    $cohort = [ordered]@{
        revision = $target
        coordinatorBlob = $coordinatorBlob
        buildSdk = ((& $exe --version) | Out-String).Trim()
        os = [Environment]::OSVersion.VersionString
        processors = [Environment]::ProcessorCount
        scratch = $scratch
    }
    $cohort | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $outputRoot 'environment.json')
    if ($PrepareOnly) { return }
    & $exe build $test -c Release -v:q --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Probe build failed' }
    for ($round = 1; $round -le $Rounds; $round++) {
        foreach ($case in 'Idle', 'Rapid', 'Burst') {
            $file = if ($case -eq 'Burst') { "burst-$round.jsonl" } else { "runs-$round.jsonl" }
            $env:ORCA_WORKER_PROBE = Join-Path $outputRoot $file
            & $exe test $test -c Release --no-build --nologo -v:q --filter ("FullyQualifiedName~DagSchedulerTests.MeasureWorker" + $case)
            if ($LASTEXITCODE -ne 0) { throw "Failed round $round $case" }
            if (!(Test-Path -LiteralPath $env:ORCA_WORKER_PROBE)) { throw "No observations from $case" }
        }
    }
    Write-Output "Observations at $outputRoot. Disposable checkout at $scratch"
} finally {
    Pop-Location
    $env:ORCA_WORKER_PROBE = $previousOutput
    $env:DOTNET_ROOT = $previousRoot
    $env:PATH = $previousPath
}
