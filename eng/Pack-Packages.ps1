param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*$')]
    [string] $Version,
    [string] $OutputDirectory = 'artifacts/packages',
    [switch] $NoBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = [IO.Path]::GetFullPath($OutputDirectory, $repoRoot)
$contract = Get-Content -Raw (Join-Path $repoRoot 'tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json') | ConvertFrom-Json
$commit = & git -C $repoRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source commit.' }

if (-not $NoBuild) {
    & dotnet build (Join-Path $repoRoot 'OrcaCore.slnx') -c Release -warnaserror --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
}

New-Item -ItemType Directory -Path $output -Force | Out-Null
if (Get-ChildItem -LiteralPath $output -Filter '*.nupkg' -File) {
    throw "Package output must be empty to prevent publishing stale files: '$output'."
}

foreach ($package in $contract.packages) {
    $projects = @(Get-ChildItem (Join-Path $repoRoot 'src') -Recurse -Filter "$($package.id).csproj")
    if ($projects.Count -ne 1) { throw "Expected one source project for '$($package.id)'." }
    & dotnet pack $projects[0].FullName -c Release --no-build --no-restore --nologo --output $output `
        "-p:PackageVersion=$Version" '-p:Authors=OrcaCore contributors' `
        '-p:Description=Pre-production .NET workflow engine. APIs and persistence formats may change. Durable DAG execution is incomplete.' `
        '-p:PackageLicenseExpression=Apache-2.0' `
        '-p:PackageProjectUrl=https://github.com/dekrevedko/Workflow-orca' `
        '-p:RepositoryUrl=https://github.com/dekrevedko/Workflow-orca' `
        '-p:RepositoryType=git' "-p:RepositoryCommit=$commit"
    if ($LASTEXITCODE -ne 0) { throw "Packing '$($package.id)' failed." }
}

$packages = @(Get-ChildItem -LiteralPath $output -Filter '*.nupkg' -File)
$expected = @($contract.packages | ForEach-Object { "$($_.id).$Version.nupkg" } | Sort-Object)
if (Compare-Object $expected @($packages.Name | Sort-Object)) { throw 'Packed inventory differs from the twelve-package manifest.' }
$packages | Sort-Object Name | ForEach-Object {
    $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($_.Name)"
} | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS') -Encoding utf8NoBOM
Write-Output "Packed $($packages.Count) packages at version $Version from $commit."
