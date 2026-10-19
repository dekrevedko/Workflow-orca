param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*$')]
    [string] $Version,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $SourceCommit,
    [string] $PackageDirectory = 'artifacts/packages'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$feed = (Resolve-Path -LiteralPath ([IO.Path]::GetFullPath($PackageDirectory, $repoRoot))).Path
$contract = Get-Content -Raw (Join-Path $repoRoot 'tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json') | ConvertFrom-Json
$expected = @($contract.packages | ForEach-Object { "$($_.id).$Version.nupkg" } | Sort-Object)
$packages = @(Get-ChildItem -LiteralPath $feed -Filter '*.nupkg' -File | Sort-Object Name)
if ($expected.Count -ne 12 -or (Compare-Object $expected @($packages.Name))) {
    throw 'Release package inventory differs from the twelve-package manifest.'
}

$checksumNames = @()
foreach ($line in Get-Content -LiteralPath (Join-Path $feed 'SHA256SUMS')) {
    if ($line -cnotmatch '^([0-9a-f]{64})  ([A-Za-z0-9.-]+\.nupkg)$') { throw "Malformed checksum entry: $line" }
    $expectedHash = $Matches[1]
    $name = $Matches[2]
    if ($name -cnotin $expected) { throw "Unexpected checksum entry: $name" }
    if ((Get-FileHash -LiteralPath (Join-Path $feed $name) -Algorithm SHA256).Hash.ToLowerInvariant() -cne $expectedHash) {
        throw "Checksum mismatch: $name"
    }
    $checksumNames += $name
}
if ($checksumNames.Count -ne $expected.Count -or (Compare-Object $expected @($checksumNames | Sort-Object -Unique))) {
    throw 'Checksum inventory differs from the release packages.'
}

foreach ($package in $packages) {
    $archive = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $entries = @($archive.Entries | Where-Object FullName -Like '*.nuspec')
        if ($entries.Count -ne 1) { throw "Expected one nuspec in $($package.Name)." }
        $reader = [IO.StreamReader]::new($entries[0].Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $metadata = $nuspec.package.metadata
        if ($metadata.version -cne $Version) { throw "Unexpected version in $($package.Name)." }
        if ($metadata.repository.commit -cne $SourceCommit) { throw "Unexpected source commit in $($package.Name)." }
        if ($metadata.repository.url -cne 'https://github.com/dekrevedko/Workflow-orca') {
            throw "Unexpected source repository in $($package.Name)."
        }
        if ($package.Name -cne "$($metadata.id).$Version.nupkg") { throw "Package identity mismatch in $($package.Name)." }
    }
    finally { $archive.Dispose() }
}
Write-Output "Verified twelve release packages: SHA-256, identity, version $Version, and source $SourceCommit."
