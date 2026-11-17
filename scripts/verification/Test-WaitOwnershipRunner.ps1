[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repo ('.codex-run/runner-tests/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$cases = @(
    @{ name = 'Passing'; kind = 'normal'; expected = ''; predicate = 'x <= 1'; next = "IF x = 0 THEN x' = 1 ELSE x' = x"; status = 'passed'; timeout = 20 },
    @{ name = 'Expected'; kind = 'mutation'; expected = 'Check'; predicate = 'x = 0'; next = "IF x = 0 THEN x' = 1 ELSE x' = x"; status = 'expected-counterexample'; timeout = 20 },
    @{ name = 'Unrelated'; kind = 'mutation'; expected = 'DifferentProperty'; predicate = 'x = 0'; next = "x' = 1"; status = 'failed'; timeout = 20 },
    @{ name = 'ParseError'; kind = 'mutation'; expected = 'Check'; predicate = 'this is invalid TLA'; next = "x' = x"; status = 'failed'; timeout = 20 },
    @{ name = 'Timeout'; kind = 'normal'; expected = ''; predicate = 'x >= 0'; next = "x' = x + 1"; status = 'inconclusive'; timeout = 1 },
    @{ name = 'UnrelatedTemporal'; kind = 'assumption'; expected = 'Expected'; predicate = 'x = 0'; next = "x' = x"; status = 'failed'; timeout = 20 }
)
$results = @()
foreach ($case in $cases) {
    $model = Join-Path $OutputDirectory ($case.name + '-input')
    New-Item -ItemType Directory -Force $model | Out-Null
    @"
---------------- MODULE EphemeralWaitOwnership ----------------
EXTENDS Naturals
VARIABLE x
Init == x = 0
Next == $($case.next)
Spec == Init /\ [][Next]_x
Check == $($case.predicate)
Expected == [] (x = 0)
Unrelated == <> (x = 1)
===============================================================
"@ | Set-Content (Join-Path $model 'EphemeralWaitOwnership.tla')
    "SPECIFICATION Spec`nINVARIANT Check" | Set-Content (Join-Path $model 'Check.cfg')
    if ($case.name -eq 'UnrelatedTemporal') {
        "SPECIFICATION Spec`nPROPERTIES Expected`nUnrelated" | Set-Content (Join-Path $model 'Check.cfg')
    }
    ConvertTo-Json -InputObject @(@{name = $case.name; kind = $case.kind; expected = $case.expected; config = 'Check.cfg'}) |
        Set-Content (Join-Path $model 'profiles.json')
    $output = Join-Path $OutputDirectory $case.name
    & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'Invoke-WaitOwnershipModelCheck.ps1') -ModelDirectory $model -OutputDirectory $output -TimeoutSeconds $case.timeout *> (Join-Path $OutputDirectory ($case.name + '.log'))
    $exitCode = $LASTEXITCODE
    $report = Get-Content (Join-Path $output 'results.json') -Raw | ConvertFrom-Json
    $actual = $report.results[0].status
    $expectedExit = if ($case.status -in @('passed','expected-counterexample')) { 0 } else { 1 }
    if ($actual -ne $case.status -or $exitCode -ne $expectedExit) {
        throw "$($case.name): expected $($case.status)/$expectedExit, received $actual/$exitCode"
    }
    $results += @{ case = $case.name; status = $actual; exitCode = $exitCode }
    Write-Host "$($case.name): classification verified ($actual)"
}
$results | ConvertTo-Json | Set-Content (Join-Path $OutputDirectory 'self-test-results.json')
