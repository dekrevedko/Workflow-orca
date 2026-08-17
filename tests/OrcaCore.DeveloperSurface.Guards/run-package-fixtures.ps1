param(
    [ValidateSet('Green', 'ExpectedRed')]
    [string] $Disposition = 'Green',
    [int] $CompletedSection = 7,
    [string] $CompletedTask = '7.34',
    [string] $GuardTask = ''
)

$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'PackageFixtures'
$definitions = Get-Content -Raw (Join-Path $PSScriptRoot 'Fixtures/package-consumer-fixtures.json') | ConvertFrom-Json
$contract = Get-Content -Raw (Join-Path $PSScriptRoot 'Fixtures/v1-public-contract.json') | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()
$packageCache = Join-Path $root 'obj/package-cache'
$feed = Join-Path $PSScriptRoot '..\..\artifacts\phase0-packages'
$version = $contract.packageVersion
$provenanceTest = 'OrcaCore.DeveloperSurface.Guards.PublicApiBaselineInfrastructureGuards.EveryManifestPackage_MatchesTheApprovedSourceRecord_AndAnyProvidedFeedMatchesCurrentAssemblies'

$pack = & (Join-Path $PSScriptRoot 'pack-exact-package-feed.ps1') -OutputDirectory $feed 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { throw "Exact current-source package feed failed.`n$pack" }

$env:ORCACORE_PUBLIC_API_PACKAGE_FEED = (Resolve-Path -LiteralPath $feed).Path
try {
    $listedTests = & dotnet test (Join-Path $PSScriptRoot 'OrcaCore.DeveloperSurface.Guards.csproj') `
        --configuration Release --no-build --no-restore --nologo --list-tests `
        --filter "FullyQualifiedName=$provenanceTest" 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Package provenance test discovery failed.`n$listedTests"
    }
    $matchedTests = @(($listedTests -split "`r?`n") | Where-Object {
        $_.Trim().Equals($provenanceTest, [StringComparison]::Ordinal)
    })
    if ($matchedTests.Count -ne 1) {
        throw "Expected exactly one package provenance test named '$provenanceTest', found $($matchedTests.Count).`n$listedTests"
    }
    $provenance = & dotnet test (Join-Path $PSScriptRoot 'OrcaCore.DeveloperSurface.Guards.csproj') `
        --configuration Release --no-build --no-restore --nologo --verbosity quiet `
        --filter "FullyQualifiedName=$provenanceTest" 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Exact package feed is not tied to the approved package source record.`n$provenance"
    }
}
finally {
    Remove-Item Env:ORCACORE_PUBLIC_API_PACKAGE_FEED -ErrorAction SilentlyContinue
}

function ConvertTo-TaskOrder([string] $TaskId) {
    if ($TaskId -notmatch '^(?<major>\d+)\.(?<minor>\d+)(?<suffix>[a-z]?)$') {
        throw "Unsupported task identifier '$TaskId'."
    }
    $suffixOrder = if ([string]::IsNullOrEmpty($Matches.suffix)) {
        0
    }
    else {
        [int][char]$Matches.suffix - [int][char]'a' + 1
    }
    return [version]::new([int]$Matches.major, [int]$Matches.minor, $suffixOrder)
}

$seed = & (Join-Path $PSScriptRoot 'seed-exact-package-cache.ps1') `
    -Feed $feed -PackageCache $packageCache -PackageVersion $version 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) { throw "Exact package cache seeding failed.`n$seed" }

$completedTaskVersion = ConvertTo-TaskOrder $CompletedTask
$selectedDefinitions = @($definitions | Where-Object {
    if (-not [string]::IsNullOrWhiteSpace($GuardTask) -and $_.guardTask -ne $GuardTask) {
        return $false
    }
    $isComplete = if ($null -ne $_.turnsGreenTask) {
        if ($_.turnsGreenSection -lt $CompletedSection) { $true }
        elseif ($_.turnsGreenSection -gt $CompletedSection) { $false }
        else { (ConvertTo-TaskOrder $_.turnsGreenTask) -le $completedTaskVersion }
    }
    else {
        $_.turnsGreenSection -le $CompletedSection
    }
    if ($Disposition -eq 'Green') {
        $isComplete
    }
    else {
        -not $isComplete
    }
})

foreach ($fixture in $selectedDefinitions) {
    $project = Join-Path $root $fixture.project
    $restore = & dotnet restore $project --force --no-cache --nologo --verbosity quiet 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Package fixture '$($fixture.id)' failed to restore from the exact local feed.`n$restore"
    }
    $output = & dotnet build $project --configuration Release --no-restore --nologo --verbosity quiet 2>&1 | Out-String
    if ($Disposition -eq 'Green') {
        if ($LASTEXITCODE -ne 0) { throw "Package fixture '$($fixture.id)' failed unexpectedly.`n$output" }
        continue
    }

    if ($LASTEXITCODE -eq 0) { throw "Expected-red package fixture '$($fixture.id)' unexpectedly built." }
    if ($output -match 'api.nuget.org|Unable to load the service index|NU1301') {
        throw "Package fixture '$($fixture.id)' escaped the local feed.`n$output"
    }
    $diagnosticText = (($output -split "`r?`n") | Where-Object { $_ -match 'error CS\d{4}:' } | ForEach-Object {
        $message = $_ -replace '^.*?error CS\d{4}:\s*', ''
        $message -replace '\s+\[[^\]]+\]\s*$', ''
    }) -join "`n"
    if ($null -ne $fixture.expectedAnyMissingSymbols) {
        if ($output -notmatch 'CS0234|CS0246|CS1061|CS0117|CS1501|CS1503|CS0426|CS8121') {
            throw "Package fixture '$($fixture.id)' did not fail on its declared Section 7B application surface.`n$output"
        }
        # The compiler may stop after a namespace/type failure, so one declared target symbol is sufficient.
        # Match diagnostic messages only: fixture/project paths are not evidence for a missing product symbol.
        $matchedSymbols = @($fixture.expectedAnyMissingSymbols | Where-Object {
            $diagnosticText -match [regex]::Escape($_)
        })
        if ($matchedSymbols.Count -eq 0) {
            throw "Package fixture '$($fixture.id)' did not report any declared missing Section 7B symbol.`n$output"
        }
        $failures.Add($fixture.id)
        continue
    }
    if ($null -ne $fixture.expectedMissingSymbols) {
        if ($output -notmatch 'CS0234|CS0246|CS1061') {
            throw "Package fixture '$($fixture.id)' did not fail on its declared missing application surface.`n$output"
        }
        foreach ($symbol in $fixture.expectedMissingSymbols) {
            if ($diagnosticText -notmatch [regex]::Escape($symbol)) {
                throw "Package fixture '$($fixture.id)' did not report missing symbol '$symbol'.`n$output"
            }
        }
        $failures.Add($fixture.id)
        continue
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
    Write-Output "Expected product reds ($($failures.Count)): $($failures -join ', ') remain incomplete against the exact 0.0.0-phase0 package/application contract [CompletedSection=$CompletedSection; CompletedTask=$CompletedTask; GuardTask=$GuardTask]."
    exit 1
}

Write-Output "Green package fixtures ($($selectedDefinitions.Count)): $($selectedDefinitions.id -join ', ') built from the repository-local feed [CompletedSection=$CompletedSection; CompletedTask=$CompletedTask; GuardTask=$GuardTask]."
