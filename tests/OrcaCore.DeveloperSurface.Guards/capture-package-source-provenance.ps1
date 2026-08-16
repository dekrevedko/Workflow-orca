param(
    [Parameter(Mandatory = $true)]
    [string] $OutputPath,
    [string] $PackageFeed = ''
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$approved = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Fixtures\package-source-provenance.json'))
$candidate = [IO.Path]::GetFullPath($OutputPath)
if ($candidate.Equals($approved, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Candidate capture refuses to overwrite the approved package provenance.'
}
if (Test-Path -LiteralPath $candidate) {
    throw "Candidate package provenance path already exists: '$candidate'."
}

$feed = if ([string]::IsNullOrWhiteSpace($PackageFeed)) {
    Join-Path $repoRoot 'artifacts\phase0-packages'
}
else {
    [IO.Path]::GetFullPath($PackageFeed)
}

$env:ORCACORE_PUBLIC_API_PACKAGE_FEED = $feed
$env:ORCACORE_PACKAGE_SOURCE_PROVENANCE_CANDIDATE = $candidate
try {
    $provenanceTest = 'OrcaCore.DeveloperSurface.Guards.PublicApiBaselineInfrastructureGuards.EveryManifestPackage_MatchesTheApprovedSourceRecord_AndAnyProvidedFeedMatchesCurrentAssemblies'
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
    $savedErrorActionPreference = $ErrorActionPreference
    try {
        # Candidate capture intentionally fails after writing the unapproved record.
        $ErrorActionPreference = 'Continue'
        $output = & dotnet test (Join-Path $PSScriptRoot 'OrcaCore.DeveloperSurface.Guards.csproj') `
            --configuration Release --no-build --no-restore --nologo --verbosity quiet `
            --filter "FullyQualifiedName=$provenanceTest" 2>&1 | Out-String
        $candidateExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedErrorActionPreference
    }
    if ($candidateExitCode -eq 0) {
        throw "Package provenance candidate test unexpectedly passed instead of stopping after capture.`n$output"
    }
    if (-not (Test-Path -LiteralPath $candidate)) {
        throw "Package provenance candidate test failed before writing its record.`n$output"
    }
    Write-Output "Captured unapproved package source provenance in '$candidate'."
    Write-Output 'Review and apply its diff manually; this command cannot modify the approved record.'
}
finally {
    Remove-Item Env:ORCACORE_PACKAGE_SOURCE_PROVENANCE_CANDIDATE -ErrorAction SilentlyContinue
    Remove-Item Env:ORCACORE_PUBLIC_API_PACKAGE_FEED -ErrorAction SilentlyContinue
}
