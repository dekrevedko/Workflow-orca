param(
    [ValidateSet('Green', 'ExpectedRed')]
    [string] $Disposition = 'Green'
)

$ErrorActionPreference = 'Stop'
$fixtures = Join-Path $PSScriptRoot 'CompileFixtures'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$normativeProject = Join-Path $fixtures 'ExactAuthoring/ExactAuthoring.csproj'
$forbiddenProject = Join-Path $fixtures 'ForbiddenAuthoring/ForbiddenAuthoring.csproj'
$productProject = Join-Path $fixtures 'ProductAuthoring/ProductAuthoring.csproj'
$productForbiddenProject = Join-Path $fixtures 'ProductForbiddenAuthoring/ProductForbiddenAuthoring.csproj'
$productSourceProject = Join-Path $repoRoot 'src/OrcaCore.Core/OrcaCore.Core.csproj'
$packageSourceProject = Join-Path $repoRoot 'src/OrcaCore.Abstractions/OrcaCore.csproj'
$incompleteProject = Join-Path $fixtures 'IncompleteProductPackage/IncompleteProductPackage.csproj'
$section7BProject = Join-Path $fixtures 'Section7BSourceSurface/Section7BSourceSurface.csproj'
$runRoot = Join-Path $PSScriptRoot "obj/compile-fixture-runs/$([Guid]::NewGuid().ToString('N'))"
$artifactsRoot = Join-Path $runRoot 'artifacts'
$negativeFeed = Join-Path $runRoot 'negative-control-feed'
$productFeed = Join-Path $runRoot 'product-feed'
$productPackageCache = Join-Path $runRoot 'product-package-cache'
$forbiddenPackageCache = Join-Path $runRoot 'forbidden-package-cache'
$productVersion = '0.0.0-phase0-local'
$source = Get-Content -Raw (Join-Path $fixtures 'ExactAuthoring/Authoring.cs')

function Invoke-CompileFixtureLane {
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
    'EphemeralWorkflowDefinition', 'DurableWorkflowDefinition',
    'EphemeralWorkflowRef', 'DurableWorkflowRef',
    'EventContractVersion', 'WorkflowEventContract', 'WorkflowEventRoute',
    'WorkflowInboundEvent', 'WorkflowEventAcceptanceResult', 'WorkflowEventAcceptanceRejection',
    'WorkflowOutboundEvent', 'WorkflowEventDispatchResult', 'IWorkflowEventIngress',
    'IWorkflowEventDispatcher', 'OrcaCoreEphemeralEngineBuilder', 'OrcaCoreDurableEngineBuilder'
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
    $positive = & dotnet build $normativeProject --configuration Release --artifacts-path $artifactsRoot --nologo --verbosity quiet -nr:false -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Normative positive authoring fixture failed.`n$positive" }

    $negative = & dotnet build $forbiddenProject --configuration Release --artifacts-path $artifactsRoot --nologo --verbosity quiet -nr:false -p:NuGetAudit=false 2>&1 | Out-String
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

    $buildProduct = & dotnet build $productSourceProject --configuration Release --artifacts-path $artifactsRoot --nologo --verbosity quiet -nr:false -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Current product source failed before packing.`n$buildProduct" }
    $packProduct = & dotnet pack $packageSourceProject --configuration Release --no-build --artifacts-path $artifactsRoot --output $productFeed --nologo --verbosity quiet -nr:false -p:PackageVersion=$productVersion -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Current product source failed to pack.`n$packProduct" }

    $productRestore = & dotnet restore $productProject --source $productFeed --force --no-cache --artifacts-path $artifactsRoot --nologo -nr:false -p:Phase0PackageVersion=$productVersion -p:RestorePackagesPath=$productPackageCache -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Fresh product package failed to restore.`n$productRestore" }
    $productBuild = & dotnet build $productProject --configuration Release --no-restore --artifacts-path $artifactsRoot --nologo --verbosity quiet -nr:false -p:Phase0PackageVersion=$productVersion -p:RestorePackagesPath=$productPackageCache -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Positive authoring source failed against the freshly packed product.`n$productBuild" }

    $productForbiddenRestore = & dotnet restore $productForbiddenProject --source $productFeed --force --no-cache --artifacts-path $artifactsRoot --nologo -nr:false -p:Phase0PackageVersion=$productVersion -p:RestorePackagesPath=$forbiddenPackageCache -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Forbidden product fixture failed to restore.`n$productForbiddenRestore" }
    $productForbidden = & dotnet build $productForbiddenProject --configuration Release --no-restore --artifacts-path $artifactsRoot --nologo --verbosity quiet -nr:false -p:Phase0PackageVersion=$productVersion -p:RestorePackagesPath=$forbiddenPackageCache -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw 'Forbidden authoring source unexpectedly compiled against the freshly packed product.' }
    $forbiddenCount = [regex]::Matches($productForbidden, 'Forbidden\.cs\((\d+),(\d+)\): error CS1061') |
        ForEach-Object { $_.Groups[1].Value + ':' + $_.Groups[2].Value } |
        Sort-Object -Unique
    if ($forbiddenCount.Count -ne 26) {
        throw "Fresh product package did not reject all 26 forbidden calls.`n$productForbidden"
    }

    $pack = & dotnet pack $incompleteProject --configuration Release --artifacts-path $artifactsRoot --output $negativeFeed --nologo --verbosity quiet -nr:false -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Incomplete negative-control package failed to pack.`n$pack" }
    $restore = & dotnet restore $productProject --source $negativeFeed --force --no-cache --artifacts-path $artifactsRoot --nologo -nr:false -p:Phase0PackageVersion=0.0.0-negativecontrol -p:RestorePackagesPath=$productPackageCache -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Product authoring negative control failed during restore rather than compilation.`n$restore" }
    $incomplete = & dotnet build $productProject --configuration Release --no-restore --artifacts-path $artifactsRoot --nologo --verbosity quiet -nr:false -p:Phase0PackageVersion=0.0.0-negativecontrol -p:RestorePackagesPath=$productPackageCache -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -eq 0) { throw 'Product-positive authoring source unexpectedly compiled against the incomplete package.' }
    if ($incomplete -match 'NU1101' -or $incomplete -notmatch 'EphemeralWorkflowBuilder' -or $incomplete -notmatch 'DurableLeaseItemBuilder') {
        throw "Incomplete package did not fail on the exact missing authoring surface.`n$incomplete"
    }

    $section7B = & dotnet build $section7BProject --configuration Release --artifacts-path $artifactsRoot --nologo --verbosity quiet -nr:false -p:NuGetAudit=false 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        throw "Completed Section 7B source-surface fixture failed.`n$section7B"
    }

    Write-Output "Green compile infrastructure: freshly packed current source; compiled the exact, product-positive, and completed Section 7B usages; verified 26 source-fixture and 26 product-package forbidden-member CS1061 diagnostics; rejected a deliberately incomplete package."
    return
}

Write-Output 'Expected-red compile fixtures (0): Section 7B is complete; the source-surface fixture is now part of the green lane.'
}

try {
    Invoke-CompileFixtureLane
}
finally {
    if (Test-Path -LiteralPath $runRoot) {
        Remove-Item -LiteralPath $runRoot -Recurse -Force
    }
}

exit 0
