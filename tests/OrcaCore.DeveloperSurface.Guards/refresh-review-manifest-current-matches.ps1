[CmdletBinding()]
param(
    [switch] $Check,
    [switch] $Force
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$fixturePath = Join-Path $PSScriptRoot 'Fixtures\review-manifest-provenance.json'
$checkpoint = Get-Content -LiteralPath $fixturePath -Raw | ConvertFrom-Json
$changed = $false

foreach ($entry in $checkpoint.entries) {
    $matches = [System.Collections.Generic.List[string]]::new()
    foreach ($row in $entry.historicalDirtyContentRecordRows) {
        $candidatePath = Join-Path $repositoryRoot ($row.path.Replace('/', [IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $candidatePath -PathType Leaf)) {
            continue
        }

        $bytes = [IO.File]::ReadAllBytes($candidatePath)
        $sha256 = [Convert]::ToHexString(
            [Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        if ($bytes.Length -eq $row.bytes -and $sha256 -eq $row.sha256) {
            $matches.Add($row.path)
        }
    }

    $recorded = @($entry.currentWorktreeMatchPaths)
    $actual = $matches.ToArray()
    $isEqual = $recorded.Count -eq $actual.Count
    for ($index = 0; $isEqual -and $index -lt $recorded.Count; $index++) {
        $isEqual = $recorded[$index] -ceq $actual[$index]
    }

    if (-not $isEqual) {
        if ($Check) {
            throw "Task $($entry.task) current-worktree pins are stale. " +
                "Recorded: [$($recorded -join ', ')]. Actual: [$($actual -join ', ')]."
        }

        $entry.currentWorktreeMatchPaths = $actual
        $changed = $true
    }

    Write-Host "Task $($entry.task): $($actual.Count) current historical rows remain byte-verifiable."
}

if ($Check -or (-not $changed -and -not $Force)) {
    return
}

$json = ($checkpoint | ConvertTo-Json -Depth 100).Replace("`r`n", "`n")
[IO.File]::WriteAllText(
    $fixturePath,
    $json + "`n",
    [Text.UTF8Encoding]::new($false))
