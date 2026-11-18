[CmdletBinding()]
param(
    [string[]]$Profile = @('*'),
    [ValidateRange(1,300)][int]$TimeoutSeconds = 300,
    [ValidateRange(128,4096)][int]$HeapMb = 1024,
    [string]$OutputDirectory,
    [string]$ModelDirectory,
    [string]$ManifestPath
)
if (!$ModelDirectory) { $ModelDirectory = Join-Path $PSScriptRoot '../../verification/tla/wait-ownership' }
$PSBoundParameters['ModelDirectory'] = $ModelDirectory
if (!$OutputDirectory) {
    $PSBoundParameters['OutputDirectory'] = Join-Path $PSScriptRoot ('../../.codex-run/wait-ownership/' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff'))
}
& (Join-Path $PSScriptRoot 'Invoke-TlaModelCheck.ps1') @PSBoundParameters -ModuleName EphemeralWaitOwnership
exit $LASTEXITCODE
