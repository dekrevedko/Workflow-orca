[CmdletBinding()]
param([string]$CacheDirectory)
& (Join-Path $PSScriptRoot 'Initialize-TlaTools.ps1') @PSBoundParameters
