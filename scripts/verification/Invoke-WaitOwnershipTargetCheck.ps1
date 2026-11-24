[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('SafetySmall','SafetyIsolation-definition','SafetyIsolation-name',
        'SafetyIsolation-version','SafetyIsolation-correlation','ProgressSmall')][string]$Profile,
    [Parameter(Mandatory)][ValidateRange(1,2)][int]$Observer,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateRange(1,3600)][int]$TimeoutSeconds = 3600
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Join-Path $PSScriptRoot '../../verification/tla/wait-ownership'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw "Choose a fresh evidence directory: $OutputDirectory" }
$inputs = New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'inputs') -Force
# The arbitrary-peer frame argument in composition.md is reviewed against these definitions.
$pins = @{
    'EphemeralWaitOwnership.tla' = '3DB53489C65A355C107DD8C00E1E6908A5254DC89A41C89045654BF6EFDF197E'
    'WaitOwnershipContract.tla' = '4B74C3957FC5793EE2127FD34D92008D5F9A8F79303DABA0CD3A82023C7754E4'
    'WaitOwnershipByTarget.tla' = '264A3FF39F7631D5BDC5ABC217275CA8C2AE3DC0A637EFA86A794C684E525212'
}
foreach ($name in $pins.Keys) {
    $path = Join-Path $root $name
    $bytes = [IO.File]::ReadAllBytes($path)
    $lfText = [Text.Encoding]::UTF8.GetString($bytes).Replace("`r`n","`n")
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($lfText)))
    if ($hash -ne $pins[$name]) { throw "Review the compositional argument for the changed $name." }
    [IO.File]::WriteAllBytes((Join-Path $inputs $name), $bytes)
}

# Derive the literal target configuration from the corresponding joint input.
$progress = $Profile -eq 'ProgressSmall'
$jointSpec = if ($progress) { 'ProgressSpec' } else { 'Spec' }
$targetSpec = if ($progress) { 'FactorProgress' } else { 'FactorSpec' }
$lines = @(Get-Content -LiteralPath (Join-Path $root "$Profile.cfg"))
$isolation = if ($Profile.StartsWith('SafetyIsolation-')) { $Profile.Substring(16) } else { 'none' }
$required = @("SPECIFICATION $jointSpec", 'CONSTANTS', 'Fault = "none"', "Isolation = `"$isolation`"",
    'Witness = "none"', 'INVARIANTS TypeOK UniqueActiveWait HandoverOrder ReservationOwnership SingleResolution ExactRelease RollbackConsistency KeyAndOccurrenceIsolation')
if ($progress) { $required += 'PROPERTIES ConditionalSettlement ConditionalReuse' }
if (($lines.Trim() -join "`n") -cne ($required -join "`n")) {
    throw "Review the projection for the changed joint configuration $Profile."
}
$lines[0] = "SPECIFICATION $targetSpec"
$lines[1] = "CONSTANTS`n  Observer = $Observer"
if ($progress) { $lines[-1] = 'PROPERTIES Settlement Reuse' }
$profileName = "$Profile-target-$Observer"
$lines + @('VIEW FactorView','INVARIANT LocalCanAdvance','PROPERTY InterfacePreserved') |
    Set-Content (Join-Path $inputs "$profileName.cfg") -Encoding utf8NoBOM
$manifest = Join-Path $inputs 'profiles.json'
ConvertTo-Json -AsArray -InputObject ([ordered]@{name=$profileName;kind='normal';expected='';config="$profileName.cfg"}) |
    Set-Content $manifest -Encoding utf8NoBOM
& (Join-Path $PSScriptRoot 'Invoke-TlaModelCheck.ps1') -ModelDirectory $inputs.FullName -ManifestPath $manifest `
    -ModuleName WaitOwnershipByTarget -Profile $profileName -OutputDirectory $OutputDirectory `
    -HeapMb 2048 -TimeoutSeconds $TimeoutSeconds -CheckLivenessAtEnd:$progress
exit $LASTEXITCODE
