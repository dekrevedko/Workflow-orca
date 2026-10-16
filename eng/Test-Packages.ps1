param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*$')]
    [string] $Version,
    [string] $PackageDirectory = 'artifacts/packages'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$feed = [IO.Path]::GetFullPath($PackageDirectory, $repoRoot)
$contract = Get-Content -Raw (Join-Path $repoRoot 'tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json') | ConvertFrom-Json
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
foreach ($source in @(@{ key = 'release'; value = $feed }, @{ key = 'nuget.org'; value = 'https://api.nuget.org/v3/index.json' })) {
    $entry = $config.CreateElement('add')
    $entry.SetAttribute('key', $source.key)
    $entry.SetAttribute('value', $source.value)
    [void]$config.configuration.packageSources.AppendChild($entry)
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
    $run = & dotnet run --project Consumer.csproj -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Package consumer build/execution failed.' }
    $run | Write-Output
    if ($run -notcontains '  result: 42') { throw 'The packaged workflow returned an unexpected result.' }
}
finally { Pop-Location }
Write-Output "All twelve packages restored into a fresh cache; typed workflow executed from '$consumer'."
