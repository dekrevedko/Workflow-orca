[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Repository,
    [Parameter(Mandatory)][string]$Revision,
    [string]$Dotnet = "dotnet",
    [ValidateSet("mem", "pg", "sql")][string[]]$Providers = @("mem", "pg", "sql")
)
$ErrorActionPreference = "Stop"
$dotnetExe = (Get-Command $Dotnet -ErrorAction Stop).Source
$repoRoot = (Resolve-Path -LiteralPath $Repository).Path
$commit = (git -C $repoRoot rev-parse --verify "$Revision^{commit}").Trim()
if ($LASTEXITCODE -ne 0) { throw "Unknown commit." }
$scratchRoot = Join-Path ([IO.Path]::GetTempPath()) ("orca-governance-" + [Guid]::NewGuid().ToString("N"))
git clone -q --shared --no-checkout --local $repoRoot $scratchRoot
if ($LASTEXITCODE -ne 0) { throw "Clone failed." }
git -C $scratchRoot checkout -q --detach $commit
if ($LASTEXITCODE -ne 0) { throw "Checkout failed." }
Push-Location $scratchRoot
try {
    $tool = Join-Path $scratchRoot "tools/measure-governance"
    $null = New-Item -Force -ItemType Directory -Path $tool
    foreach ($file in @("Program.cs", "Measure.csproj")) {
        $inputFile = Join-Path $PSScriptRoot $file
        Copy-Item -LiteralPath $inputFile -Destination (Join-Path $tool $file) -Force
    }
    $agg = Join-Path $scratchRoot "src/OrcaCore.Durable.Hosting/ResourceLeases/SerializedResourceGovernanceAggregate.cs"
    $source = [IO.File]::ReadAllText($agg)
    $isLatest = $source.Contains("store.LoadLatestAsync(")
    $projectPath = Join-Path $tool "Measure.csproj"
    $dll = Join-Path $tool "bin/Release/net10.0/Measure.dll"
    foreach ($mode in @("timing", "decode")) {
        if ($mode -eq "decode") {
            # This one-pass counter confirms actual JSON decode events. The timing build has no counter.
            $line = [regex]::Match($source, '(?m)^(?<indent> *)(?:var )?envelope = JsonSerializer\.Deserialize<GovernanceEnvelope>\((?<arg>\w+(?:\.\w+)*)\.Payload\.Span')
            if (-not $line.Success) { throw "Aggregate decoding site changed. Review the meter against this revision." }
            $indent = $line.Groups['indent'].Value
            $arg = $line.Groups['arg'].Value
            $call = 'if (AppContext.GetData("OrcaCore.MeasureGovernanceDecode") is Action<int> measure) measure(' + $arg + '.Payload.Length);'
            [IO.File]::WriteAllText($agg, $source.Insert($line.Index, ($indent + $call + [Environment]::NewLine)))
        }
        & $dotnetExe build $projectPath -c Release --nologo -v quiet -p:UseSharedCompilation=false "-p:MeasureLatest=$isLatest"
        if ($LASTEXITCODE -ne 0) { throw "Build failed." }
        foreach ($providerCase in $Providers) {
            $log = Join-Path $scratchRoot "$mode-$providerCase.log"
            & $dotnetExe $dll $providerCase $mode *> $log
            if ($LASTEXITCODE -ne 0) { throw "Probe failed. See $log." }
            Write-Host "$mode / $providerCase complete. $log"
        }
    }
} finally {
    if ($null -ne $source) { [IO.File]::WriteAllText($agg, $source) }
    Pop-Location
}
Write-Host "Scratch checkout and raw logs retained at $scratchRoot."
