[CmdletBinding()]
param(
    [string[]]$Profile = @('*'),
    [ValidateRange(1,300)][int]$TimeoutSeconds = 300,
    [ValidateRange(128,4096)][int]$HeapMb = 1024,
    [string]$OutputDirectory,
    [Parameter(Mandatory)][string]$ModelDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z][A-Za-z0-9_]*$')][string]$ModuleName,
    [string]$ManifestPath
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$ModelDirectory = [IO.Path]::GetFullPath($ModelDirectory)
if (!(Test-Path -LiteralPath (Join-Path $ModelDirectory "$ModuleName.tla") -PathType Leaf)) { throw "Missing module $ModuleName.tla in $ModelDirectory" }
if (!$ManifestPath) { $ManifestPath = Join-Path $ModelDirectory 'profiles.json' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo ('.codex-run/' + $ModuleName + '/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath (Join-Path $OutputDirectory 'results.json')) { throw 'Evidence directory already contains results; choose a new directory.' }
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$tools = & (Join-Path $PSScriptRoot 'Initialize-TlaTools.ps1')
$manifest = @(Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json)
$selected = @($manifest | Where-Object { $n = $_.name; @($Profile | Where-Object { $n -like $_ }).Count -gt 0 })
if ($selected.Count -eq 0) { throw 'No profiles matched.' }
foreach ($pattern in $Profile) {
    if (@($manifest | Where-Object name -Like $pattern).Count -eq 0) { throw "Unknown profile: $pattern" }
}
$revision = (& git -C $repo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source revision.' }
$dirty = (& git -C $repo diff --binary HEAD | Out-String)
$dirtyHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($dirty)))
$sourceFiles = @(& git -C $repo ls-files --cached --others --exclude-standard -- src/OrcaCore.Engine.Ephemeral src/OrcaCore.Abstractions src/OrcaCore.Core tests/OrcaCore.Engine.Ephemeral.Tests tests/OrcaCore.TestSupport verification/tla scripts/verification .github/workflows/wait-ownership.yml .github/workflows/start-idempotency.yml openspec/changes/verify-ephemeral-wait-ownership-with-tla docs/specs/05-requirements-events-waits-timers.md docs/specs/09-requirements-management-operations.md openspec/specs/event-routing-and-waits openspec/specs/management-and-querying Directory.* global.json)
$hashes = @($sourceFiles | Sort-Object -Unique | Where-Object { $_ -notlike '*/evidence/*' -and $_ -notlike '*_TTrace*' -and (Test-Path -LiteralPath (Join-Path $repo $_) -PathType Leaf) } | ForEach-Object {
    [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath (Join-Path $repo $_) -Algorithm SHA256).Hash }
})
$hashes | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $OutputDirectory 'source-manifest.json')
$results = [Collections.Generic.List[object]]::new()
foreach ($entry in $selected) {
    if ($entry.name -notmatch '^[A-Za-z0-9-]+$' -or $entry.config -notmatch '^[A-Za-z0-9-]+\.cfg$') { throw 'Invalid profile name/config.' }
    $run = Join-Path $OutputDirectory $entry.name
    New-Item -ItemType Directory -Force $run | Out-Null
    Get-ChildItem -LiteralPath $ModelDirectory -Filter '*.tla' | Where-Object Name -NotLike '*_TTrace*' | Copy-Item -Destination $run
    Copy-Item -LiteralPath (Join-Path $ModelDirectory $entry.config) -Destination $run
    $arguments = @("-Xmx${HeapMb}m", '-XX:+UseParallelGC', '-cp', $tools.Jar, 'tlc2.TLC', '-workers', '1', '-seed', '1', '-fp', '0', '-metadir', (Join-Path $run 'states'), '-config', $entry.config, "$ModuleName.tla")
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $tools.Java
    $info.WorkingDirectory = $run
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    foreach ($arg in $arguments) { $info.ArgumentList.Add($arg) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $watch = [Diagnostics.Stopwatch]::StartNew()
    if (!$process.Start()) { throw 'Could not start TLC.' }
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    $finished = $process.WaitForExit($TimeoutSeconds * 1000)
    if (!$finished) { $process.Kill($true); $process.WaitForExit() }
    $log = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
    $watch.Stop()
    $exitCode = $process.ExitCode
    $process.Dispose()
    $log | Set-Content (Join-Path $run 'tlc.log')
    $status = 'failed'
    $reason = 'Unexpected checker outcome.'
    $violations = @([regex]::Matches($log, 'Invariant (\w+) is violated\.') | ForEach-Object { $_.Groups[1].Value })
    $temporal = $log.Contains('Temporal properties were violated.')
    $trace = $log.Contains('The behavior up to this point is:') -or $log.Contains('The following behavior constitutes a counter-example:')
    $toolError = $log -match 'Parsing or semantic analysis failed|OutOfMemoryError|Error: Deadlock|Error: TLC threw|Error: Attempted|Error: Evaluating'
    if (!$finished -or $log -match 'OutOfMemoryError') { $status = 'inconclusive'; $reason = 'Time or memory budget exhausted.' }
    elseif ($entry.kind -in @('normal','positive-control')) {
        if ($exitCode -eq 0 -and $log.Contains('Model checking completed. No error has been found.') -and !$toolError -and !$temporal -and !$violations.Count) {
            $status = 'passed'; $reason = 'Complete exploration; all configured properties passed.'
        } elseif ($violations.Count) { $reason = 'Invariant violation: ' + ($violations -join ', ') }
    } elseif ($entry.kind -in @('mutation','witness')) {
        if ($exitCode -ne 0 -and !$toolError -and $trace -and $violations.Count -eq 1 -and $violations[0] -eq $entry.expected) {
            $status = 'expected-counterexample'; $reason = "Detected $($entry.expected)."
        }
    } elseif ($entry.kind -eq 'assumption') {
        $config = Get-Content -LiteralPath (Join-Path $run $entry.config) -Raw
        $config = [regex]::Replace($config, '(?s)\(\*.*?\*\)', '')
        $config = [regex]::Replace($config, '(?m)\\\*.*$', '')
        $propertyBlocks = [regex]::Matches($config, '(?ms)^[ \t]*PROPERT(?:Y|IES)[ \t]+(?<body>.*?)(?=^[ \t]*(?:SPECIFICATION|INIT|NEXT|CONSTANTS?|INVARIANTS?|PROPERT(?:Y|IES)|CONSTRAINTS?|ACTION_CONSTRAINTS?|SYMMETRY|VIEW|ALIAS|CHECK_DEADLOCK|POSTCONDITION)[ \t]|\z)')
        $soleExpectedProperty = $propertyBlocks.Count -eq 1 -and $propertyBlocks[0].Groups['body'].Value.Trim() -ceq $entry.expected
        if ($exitCode -ne 0 -and !$toolError -and !$violations.Count -and $temporal -and $trace -and
            $soleExpectedProperty) {
            $status = 'expected-counterexample'; $reason = "Temporal counterexample for sole property $($entry.expected)."
        } elseif (!$soleExpectedProperty) {
            $reason = 'Assumption control must configure exactly one temporal property, matching its declared expectation.'
        }
    } else { throw "Unknown profile kind: $($entry.kind)" }
    $stateCount = [regex]::Match($log, '([\d,]+) states generated, ([\d,]+) distinct states found, ([\d,]+) states left on queue')
    $results.Add([ordered]@{
        profile = $entry.name; kind = $entry.kind; expected = $entry.expected; status = $status; reason = $reason
        exitCode = $exitCode; elapsedSeconds = [Math]::Round($watch.Elapsed.TotalSeconds,3)
        timeoutSeconds = $TimeoutSeconds; heapMb = $HeapMb; command = @($tools.Java) + $arguments
        configSha256 = (Get-FileHash (Join-Path $run $entry.config)).Hash
        stateCounts = $stateCount.Value; violatedProperties = $violations; temporalViolation = $temporal
        log = "$($entry.name)/tlc.log"
    })
    Write-Host "$($entry.name): $status ($($watch.Elapsed.TotalSeconds.ToString('F2'))s) $reason"
    [ordered]@{ sourceRevision = $revision; dirtyDiffSha256 = $dirtyHash; sourceManifest = 'source-manifest.json'; javaVersion = $tools.JavaVersion; tlcRelease = $tools.TlcRelease; jarSha256 = (Get-FileHash $tools.Jar).Hash; results = @($results.ToArray()) } |
        ConvertTo-Json -Depth 8 | Set-Content (Join-Path $OutputDirectory 'results.json')
}
Write-Host "Evidence: $OutputDirectory"
if (@($results | Where-Object { $_.status -in @('failed','inconclusive') }).Count) { exit 1 }
