param(
    [ValidateSet('Green', 'ExpectedRed')]
    [string] $Disposition = 'Green',
    [int] $CompletedSection = 7,
    [string] $CompletedTask = '7.23',
    [string] $GuardTask = ''
)

$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot 'PackageFixtures'
$definitions = Get-Content -Raw (Join-Path $PSScriptRoot 'Fixtures/package-consumer-fixtures.json') | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()
$packageCache = Join-Path $root 'obj/package-cache'
$feed = Join-Path $PSScriptRoot '..\..\artifacts\phase0-packages'
$version = '0.0.0-phase0'

if (Test-Path -LiteralPath $packageCache) {
    Remove-Item -LiteralPath $packageCache -Recurse -Force
}
New-Item -ItemType Directory -Path $packageCache -Force | Out-Null

# Seed the isolated cache from the reviewed local feed so a same-version package in
# the host fallback cache can never shadow the freshly packed product.
Add-Type -AssemblyName System.IO.Compression.FileSystem
foreach ($package in Get-ChildItem -LiteralPath $feed -Filter "OrcaCore*.$version.nupkg") {
    $suffix = ".$version.nupkg"
    $id = $package.Name.Substring(0, $package.Name.Length - $suffix.Length)
    $normalizedId = $id.ToLowerInvariant()
    $target = Join-Path $packageCache "$normalizedId\$version"
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    [System.IO.Compression.ZipFile]::ExtractToDirectory($package.FullName, $target)
    $nuspec = Get-ChildItem -LiteralPath $target -Filter '*.nuspec' | Select-Object -First 1
    Move-Item -LiteralPath $nuspec.FullName -Destination (Join-Path $target "$normalizedId.nuspec") -Force
    Copy-Item -LiteralPath $package.FullName -Destination (Join-Path $target "$normalizedId.$version.nupkg")
    $sha512 = [Security.Cryptography.SHA512]::Create()
    try {
        $stream = [IO.File]::OpenRead($package.FullName)
        try { $hash = [Convert]::ToBase64String($sha512.ComputeHash($stream)) }
        finally { $stream.Dispose() }
    }
    finally { $sha512.Dispose() }
    [IO.File]::WriteAllText((Join-Path $target "$normalizedId.$version.nupkg.sha512"), $hash)
    $metadata = [ordered]@{
        version = 2
        contentHash = $hash
        source = (Resolve-Path -LiteralPath $feed).Path
    } | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $target '.nupkg.metadata'), $metadata)
}

$completedTaskVersion = [version]$CompletedTask
$selectedDefinitions = @($definitions | Where-Object {
    if (-not [string]::IsNullOrWhiteSpace($GuardTask) -and $_.guardTask -ne $GuardTask) {
        return $false
    }
    $isComplete = if ($null -ne $_.turnsGreenTask) {
        if ($_.turnsGreenSection -lt $CompletedSection) { $true }
        elseif ($_.turnsGreenSection -gt $CompletedSection) { $false }
        else { [version]$_.turnsGreenTask -le $completedTaskVersion }
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
