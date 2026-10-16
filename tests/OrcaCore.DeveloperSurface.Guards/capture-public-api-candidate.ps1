param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,
    [string] $PackageFeed
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$approved = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Fixtures\PublicApi\v1')).TrimEnd('\') + '\'
$candidate = [IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\') + '\'
$contract = Get-Content -Raw (Join-Path $PSScriptRoot 'Fixtures\v1-public-contract.json') | ConvertFrom-Json
$expectedAssemblyCount = @($contract.packages).Count
if ($candidate.StartsWith($approved, [StringComparison]::OrdinalIgnoreCase) -or
    $approved.StartsWith($candidate, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Candidate capture refuses to write into or above the checked-in approved baseline directory.'
}
if ((Test-Path -LiteralPath $candidate) -and
    @(Get-ChildItem -LiteralPath $candidate -Force).Count -ne 0) {
    throw "Candidate capture destination must be absent or empty: '$candidate'."
}
if (-not [string]::IsNullOrWhiteSpace($PackageFeed)) {
    $env:ORCACORE_PUBLIC_API_PACKAGE_FEED = [IO.Path]::GetFullPath($PackageFeed)
}

try {
    $gate = & dotnet test (Join-Path $PSScriptRoot 'OrcaCore.DeveloperSurface.Guards.csproj') `
        --configuration Release --no-build --no-restore --nologo --verbosity quiet `
        --filter 'FullyQualifiedName~RemovedDeferredAndWrongOwnerPublicSymbols_AreAbsentBeforeBaselineApproval|FullyQualifiedName~RemovedInternalStepResultPlaceholder|FullyQualifiedName~ReplaceableStructuredValueCodec_HasNoProductMetadataType' 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Candidate capture refused a product containing forbidden public or internal placeholders.`n$gate"
    }

    $env:ORCACORE_PUBLIC_API_CANDIDATE_DIR = $candidate
    $savedErrorActionPreference = $ErrorActionPreference
    try {
        # Candidate capture intentionally makes this test fail after it writes the files.
        # Windows PowerShell otherwise promotes dotnet's stderr into a terminating error.
        $ErrorActionPreference = 'Continue'
        $output = & dotnet test (Join-Path $PSScriptRoot 'OrcaCore.DeveloperSurface.Guards.csproj') `
            --configuration Release --no-build --no-restore --nologo --verbosity quiet `
            --filter 'FullyQualifiedName~EveryTargetAssembly_MatchesTheApprovedExactPublicApiBaseline' 2>&1 | Out-String
        $candidateExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $savedErrorActionPreference
    }
    if ($candidateExitCode -eq 0) {
        throw "Candidate capture unexpectedly passed the approval comparison instead of stopping after capture.`n$output"
    }
    $files = @(Get-ChildItem -LiteralPath $candidate -Filter '*.api.txt')
    if ($files.Count -ne $expectedAssemblyCount) {
        throw "Candidate capture did not produce all $expectedAssemblyCount assembly baselines.`n$output"
    }
    Write-Output "Captured $expectedAssemblyCount unapproved public API candidates in '$candidate'."
    Write-Output 'Review and apply their diff manually; this command cannot modify the approved baseline.'
}
finally {
    Remove-Item Env:ORCACORE_PUBLIC_API_CANDIDATE_DIR -ErrorAction SilentlyContinue
    Remove-Item Env:ORCACORE_PUBLIC_API_PACKAGE_FEED -ErrorAction SilentlyContinue
}
