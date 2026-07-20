param(
    [ValidateSet('Green', 'ExpectedRed')]
    [string] $Disposition = 'Green'
)

$ErrorActionPreference = 'Stop'
$fixtures = Join-Path $PSScriptRoot 'CompileFixtures'

function Invoke-RequiredBuild([string] $relativeProject) {
    $project = Join-Path $fixtures $relativeProject
    & dotnet build $project --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Compile fixture '$relativeProject' failed unexpectedly."
    }
}

function Assert-CompilerRejection([string] $relativeProject, [string[]] $members) {
    $project = Join-Path $fixtures $relativeProject
    $output = & dotnet build $project --configuration Release --nologo 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) {
        throw "Negative compile fixture '$relativeProject' compiled successfully.`n$output"
    }

    foreach ($member in $members) {
        if ($output -notmatch "CS1061.*$member") {
            throw "Negative compile fixture '$relativeProject' did not reject '$member' with CS1061.`n$output"
        }
    }
}

if ($Disposition -eq 'Green') {
    @(
        'Consumers/MinimalEphemeral/MinimalEphemeral.csproj',
        'Consumers/InMemoryDurable/InMemoryDurable.csproj',
        'Consumers/ProviderBackedDurable/ProviderBackedDurable.csproj',
        'Consumers/MetaPackage/MetaPackage.csproj',
        'ProviderAuthor/ProviderAuthor.csproj',
        'PositiveAuthoring/PositiveAuthoring.csproj'
    ) | ForEach-Object { Invoke-RequiredBuild $_ }

    Assert-CompilerRejection 'DurableForEachMustNotCompile/DurableForEachMustNotCompile.csproj' @('ForEach')
    Assert-CompilerRejection 'EphemeralWaitLongMustNotCompile/EphemeralWaitLongMustNotCompile.csproj' @('WaitLong')
    Assert-CompilerRejection 'EphemeralContinueAsNewMustNotCompile/EphemeralContinueAsNewMustNotCompile.csproj' @('ContinueAsNew')
    Assert-CompilerRejection 'DurablePoolMustNotCompile/DurablePoolMustNotCompile.csproj' @('WithPoolKey')
    Assert-CompilerRejection 'NestedCapabilitiesMustNotCompile/NestedCapabilitiesMustNotCompile.csproj' @(
        'Init',
        'End',
        'If',
        'While',
        'ForEach',
        'WaitLong',
        'RunChild',
        'RunChildren',
        'ContinueAsNew',
        'RunExternalJob',
        'AcquireResources'
    )

    Write-Output 'Green compile fixtures: 11 passed (6 consumer/authoring builds, 5 capability-rejection projects).'
    exit 0
}

$productReds = [System.Collections.Generic.List[string]]::new()

$modeProject = Join-Path $fixtures 'ModeSpecificRegistrationExpectedRed/ModeSpecificRegistrationExpectedRed.csproj'
$modeOutput = & dotnet build $modeProject --configuration Release --nologo 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    if ($modeOutput -notmatch 'EphemeralWorkflowDefinition' -or $modeOutput -notmatch 'DurableWorkflowDefinition') {
        throw "Mode-specific registration fixture failed for an unintended reason.`n$modeOutput"
    }
    $productReds.Add('mode-specific definition types and typed durable registration are not implemented')
}

$nestedProject = Join-Path $fixtures 'DurableNestedPoolMustNotCompile/DurableNestedPoolMustNotCompile.csproj'
$nestedOutput = & dotnet build $nestedProject --configuration Release --nologo 2>&1 | Out-String
if ($LASTEXITCODE -eq 0) {
    $productReds.Add('durable nested branches still expose WithPoolKey')
}
elseif ($nestedOutput -notmatch 'CS1061.*WithPoolKey') {
    throw "Nested-pool fixture failed for an unintended reason.`n$nestedOutput"
}

if ($productReds.Count -gt 0) {
    Write-Output ("Expected product reds ({0}): {1}" -f $productReds.Count, ($productReds -join '; '))
    exit 1
}

Write-Output 'Expected-red compile fixtures have turned green.'
exit 0
