param(
    [Parameter(Mandatory)][string] $Version,
    [Parameter(Mandatory)][string] $SourceCommit,
    [string] $PackageDirectory = 'artifacts/packages'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$feed = [IO.Path]::GetFullPath($PackageDirectory, $repoRoot)
$verify = Join-Path $PSScriptRoot 'Test-ReleasePackages.ps1'
$wait = Join-Path $PSScriptRoot 'Wait-NuGetPackages.ps1'
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
$fixture = Join-Path $temporaryRoot "orcacore-publishing-$([Guid]::NewGuid().ToString('N'))"

function Assert-Rejected([scriptblock] $Action, [string] $ExpectedMessage) {
    try { & $Action }
    catch {
        if ($_.Exception.Message -notlike "*$ExpectedMessage*") { throw }
        Write-Output "PASS: rejected $ExpectedMessage"
        return
    }
    throw "Expected rejection containing '$ExpectedMessage'."
}

try {
    & $verify -Version $Version -SourceCommit $SourceCommit -PackageDirectory $feed
    New-Item -ItemType Directory -Path $fixture | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $feed -File) {
        Copy-Item -LiteralPath $file.FullName -Destination $fixture
    }
    $differentCommit = if ($SourceCommit -ceq ('0' * 40)) { '1' * 40 } else { '0' * 40 }
    Assert-Rejected { & $verify -Version $Version -SourceCommit $differentCommit -PackageDirectory $fixture } 'Unexpected source commit'

    $checksumPath = Join-Path $fixture 'SHA256SUMS'
    $originalChecksums = Get-Content -Raw -LiteralPath $checksumPath
    [regex]::Replace($originalChecksums, '^[0-9a-f]{64}', ('0' * 64)) |
        Set-Content -LiteralPath $checksumPath -NoNewline -Encoding utf8NoBOM
    Assert-Rejected { & $verify -Version $Version -SourceCommit $SourceCommit -PackageDirectory $fixture } 'Checksum mismatch'
    $originalChecksums | Set-Content -LiteralPath $checksumPath -NoNewline -Encoding utf8NoBOM

    New-Item -ItemType File -Path (Join-Path $fixture 'Unexpected.nupkg') | Out-Null
    Assert-Rejected { & $verify -Version $Version -SourceCommit $SourceCommit -PackageDirectory $fixture } 'package inventory'

    # This function replaces only the HTTP boundary for the availability probe below.
    # These checks make no registry requests and never obtain a publishing credential.
    $publishingProbe = [pscustomobject]@{ Mode = 'retry'; Count = 0; Commit = $SourceCommit; MetadataRequests = 0 }
    function Invoke-WebRequest([string] $Uri, [switch] $SkipHttpErrorCheck, [int] $TimeoutSec) {
        $publishingProbe.Count++
        if ($publishingProbe.Mode -eq 'pending' -or ($publishingProbe.Mode -eq 'retry' -and $publishingProbe.Count -eq 1)) {
            return [pscustomobject]@{ StatusCode = 404; Content = '' }
        }
        $parts = ([Uri]$Uri).AbsolutePath.Trim('/').Split('/')
        if ($parts[-1] -eq 'index.json') {
            $listedVersion = if ($publishingProbe.Mode -eq 'index-pending') { '0.0.0-preview.other' } else { $Version }
            return [pscustomobject]@{ StatusCode = 200; Content = (@{ versions = @($listedVersion) } | ConvertTo-Json -Compress) }
        }
        $publishingProbe.MetadataRequests++
        return [pscustomobject]@{
            StatusCode = 200
            Content = '<package><metadata><id>{0}</id><version>{1}</version><repository url="https://github.com/dekrevedko/Workflow-orca" commit="{2}" /></metadata></package>' -f $parts[1], $parts[2], $publishingProbe.Commit
        }
    }
    $observed = @(& $wait -Version $Version -SourceCommit $SourceCommit -TimeoutSeconds 10 -PollIntervalSeconds 1)
    if ($publishingProbe.Count -ne 25 -or @($observed | Where-Object { $_ -like 'Available:*' }).Count -ne 12) {
        throw 'Availability verification did not retry the pending package and verify all twelve.'
    }
    Write-Output 'PASS: public availability retries processing and checks all twelve package identities.'

    $publishingProbe.Mode = 'available'
    $publishingProbe.Commit = $differentCommit
    Assert-Rejected { & $wait -Version $Version -SourceCommit $SourceCommit -TimeoutSeconds 10 -PollIntervalSeconds 1 } 'metadata differs'
    $publishingProbe.Mode = 'pending'
    Assert-Rejected { & $wait -Version $Version -SourceCommit $SourceCommit -TimeoutSeconds 1 -PollIntervalSeconds 1 } 'timed out'
    $publishingProbe.Mode = 'index-pending'
    $publishingProbe.MetadataRequests = 0
    Assert-Rejected { & $wait -Version $Version -SourceCommit $SourceCommit -TimeoutSeconds 1 -PollIntervalSeconds 1 } 'timed out'
    if ($publishingProbe.MetadataRequests -ne 0) { throw 'Availability accepted files before their version was listed.' }
    Write-Output 'All eight package publication checks passed.'
}
finally {
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $expectedPrefix = $temporaryRoot + [IO.Path]::DirectorySeparatorChar
    if (-not $resolvedFixture.StartsWith($expectedPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $resolvedFixture -Leaf) -cnotmatch '^orcacore-publishing-[0-9a-f]{32}$') {
        throw 'Refusing to clean up an unexpected publishing fixture path.'
    }
    if (Test-Path -LiteralPath $resolvedFixture) { Remove-Item -LiteralPath $resolvedFixture -Recurse -Force }
}
