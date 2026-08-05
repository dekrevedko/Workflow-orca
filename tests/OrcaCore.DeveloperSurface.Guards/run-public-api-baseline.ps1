param(
    [switch] $FreshPack
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $PSScriptRoot 'OrcaCore.DeveloperSurface.Guards.csproj'
$contract = Get-Content -Raw (Join-Path $PSScriptRoot 'Fixtures\v1-public-contract.json') | ConvertFrom-Json
$feed = $null
$packageCache = $null

try {
    $buildTarget = if ($FreshPack) { Join-Path $repoRoot 'OrcaCore.slnx' } else { $project }
    $build = & dotnet build $buildTarget `
        --configuration Release --nologo --verbosity quiet 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Current source failed before public API verification.`n$build" }

    if ($FreshPack) {
        $feed = Join-Path $PSScriptRoot "obj\public-api-pack\$([Guid]::NewGuid().ToString('N'))"
        New-Item -ItemType Directory -Path $feed -Force | Out-Null
        $packageCache = Join-Path $feed 'negative-package-cache'

        foreach ($package in $contract.packages) {
            $sourceProject = @(Get-ChildItem (Join-Path $repoRoot 'src') -Recurse -Filter "$($package.id).csproj")
            if ($sourceProject.Count -ne 1) {
                throw "Expected one project named '$($package.id).csproj', found $($sourceProject.Count)."
            }
            $pack = & dotnet pack $sourceProject[0].FullName --configuration Release --no-build `
                --output $feed --nologo --verbosity quiet -p:PackageVersion=$contract.packageVersion 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "Package '$($package.id)' failed to pack.`n$pack" }
        }
        $env:ORCACORE_PUBLIC_API_PACKAGE_FEED = $feed

        $negativeProject = Join-Path $PSScriptRoot 'CompileFixtures\ProductForbiddenLegacySurface\ProductForbiddenLegacySurface.csproj'
        $negativeRestore = & dotnet restore $negativeProject --force --no-cache --nologo --verbosity quiet `
            -p:Phase0PackageFeed=$feed -p:Phase0PackageVersion=$contract.packageVersion `
            -p:Phase0PackageCache=$packageCache 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "Forbidden legacy-surface package fixture failed to restore.`n$negativeRestore" }
        $savedErrorActionPreference = $ErrorActionPreference
        try {
            # This fixture must fail to compile; retain its diagnostics as evidence instead
            # of allowing Windows PowerShell to promote native stderr into a terminating error.
            $ErrorActionPreference = 'Continue'
            $negativeBuild = & dotnet build $negativeProject --configuration Release --no-restore --nologo --verbosity quiet `
                -p:Phase0PackageFeed=$feed -p:Phase0PackageVersion=$contract.packageVersion `
                -p:Phase0PackageCache=$packageCache 2>&1 | Out-String
            $negativeBuildExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $savedErrorActionPreference
        }
        if ($negativeBuildExitCode -eq 0) { throw 'Forbidden legacy-surface source unexpectedly compiled against fresh packages.' }
        if ($negativeBuild -notmatch 'CS0234|CS0246|CS0122|CS1061') {
            throw "Forbidden legacy-surface fixture failed for an unintended reason.`n$negativeBuild"
        }
        foreach ($symbol in @(
            'WorkflowInstanceSnapshot', 'WorkflowStatus', 'SagaAuditSnapshot', 'RunChildrenJoinPolicy',
            'CompensateChildGroupCommand', 'Management', 'IWorkflowRetentionStore', 'RetentionPolicy',
            'ArchiveResult', 'PurgeResult', 'WorkflowProjectionQuery', 'WorkflowProjectionStatistics',
            'ProviderCommitPolicy', 'SerializedPayload', 'ProviderJsonSerializerContext',
            'IResourceLeaseGovernanceStore', 'ForEachItemContext', 'RunExternalJobCommand',
            'RaiseEventToDefinitionAsync', 'RaiseEventByDefinitionAsync',
            'CheckpointActiveChild', 'CheckpointActiveChildGroup', 'CheckpointActiveExternalJob',
            'CheckpointSagaForwardAction', 'CheckpointSagaCompensationAction',
            'CheckpointSagaRecoveryIntervention', 'CheckpointBufferedDelivery', 'CheckpointBufferedTimer',
            'WorkflowDeliveryBufferedEvent',
            'WorkflowTimerBufferedEvent', 'WorkflowDeliveryDiscardedEvent', 'IWorkflowPayloadSerializer',
            'IWorkflowPayloadCodec', 'IStructuredValueCodec', 'IEphemeralStateSnapshotter', 'StateSnapshotter',
            'WorkflowPayloadSerializationOptions', 'OrcaCoreServiceCollectionExtensions',
            'OrcaCoreOpenTelemetryServiceCollectionExtensions', 'DurableYieldCommand', 'EngineYieldStepResult',
            'EngineContinueAsNewStepResult', 'EngineExternalJobStepResult',
            'EngineAcquireResourcesStepResult', 'WorkflowRuntimeBridge',
            'PublicAuthoringContracts')) {
            if ($negativeBuild -notmatch [regex]::Escape($symbol)) {
                throw "Forbidden legacy-surface package fixture omitted compiler evidence for '$symbol'.`n$negativeBuild"
            }
        }
    }

    $output = & dotnet test $project --configuration Release --no-build --no-restore --nologo --verbosity quiet `
        --filter 'FullyQualifiedName~PublicApiBaselineInfrastructureGuards' 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Public API baseline verification failed.`n$output" }
    Write-Output $output.Trim()
}
finally {
    Remove-Item Env:ORCACORE_PUBLIC_API_PACKAGE_FEED -ErrorAction SilentlyContinue
    if ($null -ne $feed -and (Test-Path -LiteralPath $feed)) {
        Remove-Item -LiteralPath $feed -Recurse -Force
    }
}
