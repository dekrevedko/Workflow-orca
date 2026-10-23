param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*$')]
    [string] $Version,
    [string] $PackageDirectory = 'artifacts/packages',
    [switch] $NuGetOrg,
    [switch] $GitHubPackages,
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9-]{0,38}$')]
    [string] $GitHubOwner = 'dekrevedko',
    [string] $ExpectedReleaseDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
if ($NuGetOrg -and $GitHubPackages) { throw 'Choose one remote package registry.' }
if ($GitHubPackages -and [string]::IsNullOrWhiteSpace($ExpectedReleaseDirectory)) {
    throw 'GitHub verification requires the expected release directory.'
}
if (-not $GitHubPackages -and $ExpectedReleaseDirectory) { throw 'Release comparison is only supported for GitHub Packages.' }
$githubSource = "https://nuget.pkg.github.com/$GitHubOwner/index.json"
$releaseHashes = @{}
if ($GitHubPackages) {
    $expectedFeed = [IO.Path]::GetFullPath($ExpectedReleaseDirectory, $repoRoot)
    foreach ($line in Get-Content -LiteralPath (Join-Path $expectedFeed 'SHA256SUMS')) {
        if ($line -cnotmatch '^([0-9a-f]{64})  ([A-Za-z0-9.-]+\.nupkg)$') { throw 'Malformed release checksum.' }
        if ($releaseHashes.ContainsKey($Matches[2])) { throw 'Duplicate release checksum.' }
        $releaseHashes.Add($Matches[2], $Matches[1])
    }
}
$feed = [IO.Path]::GetFullPath($PackageDirectory, $repoRoot)
$contract = Get-Content -Raw (Join-Path $repoRoot 'tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json') | ConvertFrom-Json
if (@($contract.packages).Count -ne 12) { throw 'Expected the twelve-package manifest.' }
# Outside the repository: no inherited build props, project references or pre-existing package cache.
$consumer = Join-Path ([IO.Path]::GetTempPath()) "orcacore-consumer-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $consumer | Out-Null

$references = ($contract.packages | ForEach-Object {
    "    <PackageReference Include=`"$($_.id)`" Version=`"$Version`" />"
}) -join "`n"
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
$references
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj') -Encoding utf8NoBOM

$config = [xml]'<configuration><packageSources><clear /></packageSources></configuration>'
$sources = @()
if ($GitHubPackages) { $sources += @{ key = 'github'; value = $githubSource } }
elseif (-not $NuGetOrg) { $sources += @{ key = 'release'; value = $feed } }
$sources += @{ key = 'nuget.org'; value = 'https://api.nuget.org/v3/index.json' }
foreach ($source in $sources) {
    $entry = $config.CreateElement('add')
    $entry.SetAttribute('key', $source.key)
    $entry.SetAttribute('value', $source.value)
    [void]$config.configuration.packageSources.AppendChild($entry)
}
if ($GitHubPackages) {
    $mapping = $config.CreateElement('packageSourceMapping')
    foreach ($source in @(@{ key = 'github'; patterns = @('OrcaCore', 'OrcaCore.*') }, @{ key = 'nuget.org'; patterns = @('*') })) {
        $mapped = $config.CreateElement('packageSource')
        $mapped.SetAttribute('key', $source.key)
        foreach ($pattern in $source.patterns) {
            $package = $config.CreateElement('package')
            $package.SetAttribute('pattern', $pattern)
            [void]$mapped.AppendChild($package)
        }
        [void]$mapping.AppendChild($mapped)
    }
    [void]$config.configuration.AppendChild($mapping)
}
$config.Save((Join-Path $consumer 'NuGet.Config'))
$example = Get-Content -Raw (Join-Path $repoRoot 'samples/OrcaCore.Examples/ExampleRunner.cs')
$example = "using OrcaCore;`n" + $example
$example | Set-Content -LiteralPath (Join-Path $consumer 'ExampleRunner.cs') -Encoding utf8NoBOM
@'
await OrcaCore.Examples.ExampleRunner.RunAllAsync(CancellationToken.None);
'@ | Set-Content -LiteralPath (Join-Path $consumer 'Program.cs') -Encoding utf8NoBOM

Push-Location $consumer
try {
    & dotnet restore Consumer.csproj --configfile NuGet.Config --packages packages --no-http-cache --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Clean package-only restore failed.' }
    if ($GitHubPackages) {
        if ($releaseHashes.Count -ne 12) { throw 'Expected exactly twelve release checksums.' }
        foreach ($package in $contract.packages) {
            $id = $package.id.ToLowerInvariant()
            $cache = Join-Path $consumer "packages/$id/$($Version.ToLowerInvariant())"
            $metadata = Get-Content -Raw -LiteralPath (Join-Path $cache '.nupkg.metadata') | ConvertFrom-Json
            if ($metadata.source -ine $githubSource) { throw "Package did not come from GitHub: $($package.id)" }
            $name = "$($package.id).$Version.nupkg"
            $hash = (Get-FileHash -LiteralPath (Join-Path $cache "$id.$($Version.ToLowerInvariant()).nupkg") -Algorithm SHA256).Hash.ToLowerInvariant()
            if (-not $releaseHashes.ContainsKey($name) -or $hash -cne $releaseHashes[$name]) { throw "GitHub release-byte mismatch: $name" }
            Write-Output "GitHub source and release SHA-256 verified: $name"
        }
    }
    $run = & dotnet run --project Consumer.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Package consumer build/execution failed.' }
    $run | Write-Output
    if ($run -notcontains '  result: 42') { throw 'The packaged workflow returned an unexpected result.' }
}
finally { Pop-Location }
Write-Output "All twelve packages restored into a fresh cache; typed workflow executed from '$consumer'."
