param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*$')]
    [string] $Version,
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $SourceCommit,
    [ValidateRange(1, 3600)][int] $TimeoutSeconds = 1200,
    [ValidateRange(1, 60)][int] $PollIntervalSeconds = 15
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$contract = Get-Content -Raw (Join-Path $repoRoot 'tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json') | ConvertFrom-Json
$pending = @($contract.packages.id)
if ($pending.Count -ne 12) { throw 'Expected the twelve-package manifest.' }
$elapsed = [Diagnostics.Stopwatch]::StartNew()

function Get-NuGetDocument([string] $Uri, [string] $Id) {
    $requestTimeout = [int][Math]::Clamp([Math]::Ceiling($TimeoutSeconds - $elapsed.Elapsed.TotalSeconds), 1, 30)
    try { $response = Invoke-WebRequest -Uri $Uri -SkipHttpErrorCheck -TimeoutSec $requestTimeout }
    catch { Write-Warning "NuGet lookup for $Id failed; will retry: $($_.Exception.Message)"; return $null }
    if ($response.StatusCode -eq 200) {
        $text = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
        return $text.TrimStart([char]0xfeff)
    }
    if ($response.StatusCode -notin @(404, 408, 429) -and $response.StatusCode -lt 500) {
        throw "NuGet.org returned HTTP $($response.StatusCode) for $Id."
    }
    return $null
}

while ($pending.Count -gt 0) {
    foreach ($id in @($pending)) {
        if ($elapsed.Elapsed.TotalSeconds -ge $TimeoutSeconds) {
            throw "NuGet.org availability timed out; still pending: $($pending -join ', '). Rerun the existing-release publication to resume."
        }
        $baseUrl = 'https://api.nuget.org/v3-flatcontainer/{0}' -f $id.ToLowerInvariant()
        $index = Get-NuGetDocument -Uri "$baseUrl/index.json" -Id $id
        if ($null -eq $index -or ($index | ConvertFrom-Json).versions -inotcontains $Version) { continue }
        $nuspecText = Get-NuGetDocument -Uri ('{0}/{1}/{2}.nuspec' -f $baseUrl, $Version.ToLowerInvariant(), $id.ToLowerInvariant()) -Id $id
        if ($null -eq $nuspecText) { continue }
        [xml]$nuspec = $nuspecText
        $metadata = $nuspec.package.metadata
        if ($metadata.id -ine $id -or $metadata.version -ine $Version -or
            $metadata.repository.commit -cne $SourceCommit -or
            $metadata.repository.url -cne 'https://github.com/dekrevedko/Workflow-orca') {
            throw "NuGet.org metadata differs from the expected release: $id $Version."
        }
        Write-Output "Available: https://www.nuget.org/packages/$id/$Version"
        $pending = @($pending | Where-Object { $_ -cne $id })
    }
    if ($pending.Count -gt 0) {
        Write-Output "$($pending.Count) packages are still processing on NuGet.org."
        $remainingSeconds = [int][Math]::Ceiling($TimeoutSeconds - $elapsed.Elapsed.TotalSeconds)
        if ($remainingSeconds -gt 0) { Start-Sleep -Seconds ([Math]::Min($PollIntervalSeconds, $remainingSeconds)) }
    }
}
Write-Output "All twelve packages at $Version are available on NuGet.org with source commit $SourceCommit."
