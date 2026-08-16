param(
    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory,
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$contract = Get-Content -Raw (Join-Path $PSScriptRoot 'Fixtures\v1-public-contract.json') | ConvertFrom-Json
$feed = [IO.Path]::GetFullPath($OutputDirectory)
$repositoryPrefix = $repoRoot.TrimEnd('\') + '\'
if (-not ($feed.TrimEnd('\') + '\').StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Exact package feed must be inside the repository: '$feed'."
}

if (-not $NoBuild) {
    $build = & dotnet build (Join-Path $repoRoot 'OrcaCore.slnx') `
        --configuration Release --no-restore --nologo --verbosity quiet 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Current source failed before exact package packing.`n$build" }
}

New-Item -ItemType Directory -Path $feed -Force | Out-Null
Get-ChildItem -LiteralPath $feed -Filter "OrcaCore*.$($contract.packageVersion).nupkg" -File |
    Remove-Item -Force

foreach ($package in $contract.packages) {
    $sourceProject = @(Get-ChildItem (Join-Path $repoRoot 'src') -Recurse -Filter "$($package.id).csproj")
    if ($sourceProject.Count -ne 1) {
        throw "Expected one project named '$($package.id).csproj', found $($sourceProject.Count)."
    }
    $pack = & dotnet pack $sourceProject[0].FullName --configuration Release --no-build `
        --output $feed --nologo --verbosity quiet -p:PackageVersion=$($contract.packageVersion) 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Package '$($package.id)' failed to pack.`n$pack" }
}

Write-Output "Packed $(@($contract.packages).Count) exact current-source packages to '$feed'."
