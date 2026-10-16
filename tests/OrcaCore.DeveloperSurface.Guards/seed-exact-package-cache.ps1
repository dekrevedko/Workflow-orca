param(
    [Parameter(Mandatory = $true)]
    [string] $Feed,
    [Parameter(Mandatory = $true)]
    [string] $PackageCache,
    [Parameter(Mandatory = $true)]
    [string] $PackageVersion
)

$ErrorActionPreference = 'Stop'
$resolvedFeed = (Resolve-Path -LiteralPath $Feed).Path
$resolvedCache = [IO.Path]::GetFullPath($PackageCache)
$cacheParent = Split-Path -Parent $resolvedCache
if ([string]::IsNullOrWhiteSpace($cacheParent) -or $resolvedCache -eq [IO.Path]::GetPathRoot($resolvedCache)) {
    throw "Exact package cache path is unsafe: '$resolvedCache'."
}

if (Test-Path -LiteralPath $resolvedCache) {
    Remove-Item -LiteralPath $resolvedCache -Recurse -Force
}
[IO.Directory]::CreateDirectory($resolvedCache) | Out-Null

# NuGet fallback folders are still needed for third-party dependencies. Seed every
# freshly packed OrcaCore package into the isolated primary cache so a same-version
# package in the host cache can never shadow the reviewed feed.
Add-Type -AssemblyName System.IO.Compression.FileSystem
$packages = @(Get-ChildItem -LiteralPath $resolvedFeed -Filter "OrcaCore*.$PackageVersion.nupkg")
if ($packages.Count -eq 0) {
    throw "Exact package feed '$resolvedFeed' contains no OrcaCore $PackageVersion packages."
}

foreach ($package in $packages) {
    $suffix = ".$PackageVersion.nupkg"
    $id = $package.Name.Substring(0, $package.Name.Length - $suffix.Length)
    $normalizedId = $id.ToLowerInvariant()
    $target = Join-Path $resolvedCache "$normalizedId\$PackageVersion"
    [IO.Directory]::CreateDirectory($target) | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($package.FullName, $target)
    $nuspec = Get-ChildItem -LiteralPath $target -Filter '*.nuspec' | Select-Object -First 1
    if ($null -eq $nuspec) { throw "Package '$($package.FullName)' contains no nuspec." }
    Move-Item -LiteralPath $nuspec.FullName -Destination (Join-Path $target "$normalizedId.nuspec") -Force
    Copy-Item -LiteralPath $package.FullName -Destination (Join-Path $target "$normalizedId.$PackageVersion.nupkg")

    $sha512 = [Security.Cryptography.SHA512]::Create()
    try {
        $stream = [IO.File]::OpenRead($package.FullName)
        try { $hash = [Convert]::ToBase64String($sha512.ComputeHash($stream)) }
        finally { $stream.Dispose() }
    }
    finally { $sha512.Dispose() }

    [IO.File]::WriteAllText((Join-Path $target "$normalizedId.$PackageVersion.nupkg.sha512"), $hash)
    $metadata = [ordered]@{
        version = 2
        contentHash = $hash
        source = $resolvedFeed
    } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $target '.nupkg.metadata'), $metadata)
}

Write-Output "Seeded $($packages.Count) exact OrcaCore packages into '$resolvedCache'."
