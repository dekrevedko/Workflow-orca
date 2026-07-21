param(
    [ValidateSet('Green', 'ExpectedRed')]
    [string] $Disposition = 'Green'
)

$ErrorActionPreference = 'Stop'
$fixtures = Join-Path $PSScriptRoot 'CompileFixtures'
$normativeProject = Join-Path $fixtures 'ExactAuthoring/ExactAuthoring.csproj'
$forbiddenProject = Join-Path $fixtures 'ForbiddenAuthoring/ForbiddenAuthoring.csproj'
$productProject = Join-Path $fixtures 'ProductAuthoring/ProductAuthoring.csproj'
$incompleteProject = Join-Path $fixtures 'IncompleteProductPackage/IncompleteProductPackage.csproj'
$negativeFeed = Join-Path $fixtures 'IncompleteProductPackage/obj/negative-control-feed'
$productPackageCache = Join-Path $fixtures 'ProductAuthoring/obj/package-cache'
$source = Get-Content -Raw (Join-Path $fixtures 'ExactAuthoring/Authoring.cs')

$requiredFamilies = @(
    'EphemeralWorkflowInitBuilder', 'DurableWorkflowInitBuilder',
    'EphemeralWorkflowBuilder', 'DurableWorkflowBuilder',
    'EphemeralNestedBuilder', 'DurableNestedBuilder',
    'EphemeralBranchBuilder', 'DurableBranchBuilder',
    'EphemeralItemBuilder', 'DurableItemBuilder',
    'DurableLeaseWorkflowBuilder', 'DurableLeaseNestedBuilder',
    'DurableLeaseBranchBuilder', 'DurableLeaseItemBuilder',
    'EphemeralWorkflowParallelBranchScopeBuilder', 'DurableWorkflowParallelBranchScopeBuilder',
    'EphemeralWorkflowParallelJoinBuilder', 'DurableWorkflowParallelJoinBuilder',
    'EphemeralForEachJoinBuilder', 'DurableForEachJoinBuilder',
    'EphemeralWorkflowCompletionBuilder', 'DurableWorkflowCompletionBuilder',
    'EphemeralWorkflowDefinition', 'DurableWorkflowDefinition', 'DurableWorkflowRef'
)

foreach ($family in $requiredFamilies) {
    if ($source -notmatch [regex]::Escape($family)) {
        throw "Exact authoring compile fixture omits '$family'."
    }
}

if ($source -match 'WaitLong|Yield|RunExternalJob|RunChild|RunChildren|WhenFirst|WithPoolKey') {
    throw 'Exact authoring compile fixture contains a removed, deferred, or superseded member.'
}

if ($Disposition -eq 'Green') {
    $positive = & dotnet build $normativeProject --configuration Release --nologo --verbosity quiet 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Normative positive authoring fixture failed.`n$positive" }

    $negative = & dotnet build $forbiddenProject --configuration Release --nologo --verbosity quiet 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw 'Forbidden authoring fixture unexpectedly compiled.' }
    foreach ($expected in @(
        'EphemeralNestedBuilder<object, object>', 'DurableNestedBuilder<object, object>',
        'EphemeralBranchBuilder<object, object>', 'DurableBranchBuilder<object, object>',
        'EphemeralItemBuilder<object, object>', 'DurableItemBuilder<object, object>',
        'DurableLeaseWorkflowBuilder<object, object>', 'DurableLeaseNestedBuilder<object, object>',
        'DurableLeaseBranchBuilder<object, object>', 'DurableLeaseItemBuilder<object, object>',
        "definition for 'Parallel'", "definition for 'ForEach'", "definition for 'While'",
        "definition for 'AcquireResources'", "definition for 'ContinueAsNew'",
        "definition for 'WaitLong'", "definition for 'Yield'", "definition for 'WhenFirst'",
        "definition for 'RunChild'", "definition for 'RunExternalJob'")) {
        if ($negative -notmatch [regex]::Escape($expected)) {
            throw "Forbidden authoring fixture omitted precise CS1061 evidence for '$expected'.`n$negative"
        }
    }

    if (Test-Path -LiteralPath $productPackageCache) {
        Remove-Item -LiteralPath $productPackageCache -Recurse -Force
    }
    $pack = & dotnet pack $incompleteProject --configuration Release --output $negativeFeed --nologo --verbosity quiet 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Incomplete negative-control package failed to pack.`n$pack" }
    $restore = & dotnet restore $productProject --source $negativeFeed --force --no-cache --nologo -p:Phase0PackageVersion=0.0.0-negativecontrol 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Product authoring negative control failed during restore rather than compilation.`n$restore" }
    $incomplete = & dotnet build $productProject --configuration Release --no-restore --nologo --verbosity quiet -p:Phase0PackageVersion=0.0.0-negativecontrol 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw 'Product-positive authoring source unexpectedly compiled against the incomplete package.' }
    if ($incomplete -match 'NU1101' -or $incomplete -notmatch 'EphemeralWorkflowBuilder' -or $incomplete -notmatch 'DurableLeaseItemBuilder') {
        throw "Incomplete package did not fail on the exact missing authoring surface.`n$incomplete"
    }

    Write-Output "Green compile infrastructure: compiled the exact companion and positive usages; verified 26 precise forbidden-member CS1061 diagnostics; rejected a deliberately incomplete product package."
    exit 0
}

if (Test-Path -LiteralPath $productPackageCache) {
    Remove-Item -LiteralPath $productPackageCache -Recurse -Force
}
$output = & dotnet build $productProject --configuration Release --nologo --verbosity quiet 2>&1 | Out-String
if ($LASTEXITCODE -eq 0) {
    throw 'Expected-red exact authoring fixture unexpectedly compiled.'
}
if ($output -notmatch 'NU1101.*OrcaCore' -and
    ($output -notmatch 'EphemeralWorkflowInitBuilder' -or $output -notmatch 'DurableLeaseItemBuilder')) {
    throw "Exact authoring fixture failed for an unintended reason.`n$output"
}

Write-Output 'Expected product red (1): the full positive authoring usage fixture does not compile against OrcaCore 0.0.0-phase0.'
exit 1
