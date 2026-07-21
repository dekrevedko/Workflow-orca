param(
    [ValidateSet('Green', 'ExpectedRed')]
    [string] $Disposition = 'Green'
)

$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'PackageFixtures'
$definitions = Get-Content -Raw (Join-Path $PSScriptRoot 'Fixtures/package-consumer-fixtures.json') | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()

foreach ($fixture in $definitions) {
    $project = Join-Path $root $fixture.project
    $output = & dotnet build $project --configuration Release --nologo --verbosity quiet 2>&1 | Out-String
    if ($Disposition -eq 'Green') {
        if ($LASTEXITCODE -ne 0) { throw "Package fixture '$($fixture.id)' failed unexpectedly.`n$output" }
        continue
    }

    if ($LASTEXITCODE -eq 0) { throw "Expected-red package fixture '$($fixture.id)' unexpectedly built." }
    if ($output -match 'api.nuget.org|Unable to load the service index|NU1301') {
        throw "Package fixture '$($fixture.id)' escaped the local feed.`n$output"
    }
    if ($output -notmatch 'NU1101|NU1102') {
        throw "Package fixture '$($fixture.id)' failed for an unintended reason.`n$output"
    }
    foreach ($package in $fixture.packages) {
        if ($output -notmatch [regex]::Escape($package)) {
            throw "Package fixture '$($fixture.id)' did not report missing declared package '$package'.`n$output"
        }
    }
    $failures.Add($fixture.id)
}

if ($Disposition -eq 'ExpectedRed') {
    Write-Output "Expected product reds ($($failures.Count)): $($failures -join ', ') lack packed 0.0.0-phase0 packages."
    exit 1
}

Write-Output "Green package fixtures: $($definitions.Count) built from the repository-local feed."
