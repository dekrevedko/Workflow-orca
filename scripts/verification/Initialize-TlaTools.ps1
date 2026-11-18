[CmdletBinding()]
param([string]$CacheDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!$CacheDirectory) { $CacheDirectory = Join-Path $repo '.codex-run/tla-tools' }
$CacheDirectory = [IO.Path]::GetFullPath($CacheDirectory)
$pin = Get-Content (Join-Path $repo 'verification/tla/toolchain.json') -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Force $CacheDirectory | Out-Null
function Get-VerifiedArtifact($Url, $Path, $Sha256) {
    if (!(Test-Path -LiteralPath $Path)) { Invoke-WebRequest -Uri $Url -OutFile $Path }
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -ne $Sha256) {
        throw "Checksum mismatch: $Path. Remove the corrupt cache file explicitly before retrying."
    }
}
$jar = Join-Path $CacheDirectory 'tla2tools.jar'
Get-VerifiedArtifact $pin.tlc.url $jar $pin.tlc.sha256
if ($IsWindows) {
    $archive = Join-Path $CacheDirectory 'java.zip'
    Get-VerifiedArtifact $pin.java.windows.url $archive $pin.java.windows.sha256
    $java = Join-Path $CacheDirectory "$($pin.java.directory)/bin/java.exe"
    if (!(Test-Path -LiteralPath $java)) { Expand-Archive -LiteralPath $archive -DestinationPath $CacheDirectory }
} elseif ($IsLinux) {
    $archive = Join-Path $CacheDirectory 'java.tar.gz'
    Get-VerifiedArtifact $pin.java.linux.url $archive $pin.java.linux.sha256
    $java = Join-Path $CacheDirectory "$($pin.java.directory)/bin/java"
    if (!(Test-Path -LiteralPath $java)) {
        & tar -xzf $archive -C $CacheDirectory
        if ($LASTEXITCODE -ne 0) { throw 'Java extraction failed.' }
    }
} else { throw 'The pilot pins Windows/Linux x64 only.' }
$version = (& $java -version 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or !$version.Contains($pin.java.build)) { throw "Unexpected Java build: $version" }
[pscustomobject]@{ Java = $java; Jar = $jar; JavaVersion = $version.Trim(); TlcRelease = $pin.tlc.release }
