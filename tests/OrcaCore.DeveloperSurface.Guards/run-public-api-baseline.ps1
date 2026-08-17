param(
    [switch] $FreshPack
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $PSScriptRoot 'OrcaCore.DeveloperSurface.Guards.csproj'
$contract = Get-Content -Raw (Join-Path $PSScriptRoot 'Fixtures\v1-public-contract.json') | ConvertFrom-Json
$feed = $null
$packageCache = $null
$probeRoot = $null
$baselineClass = 'OrcaCore.DeveloperSurface.Guards.PublicApiBaselineInfrastructureGuards'
$baselineFilter = "FullyQualifiedName~$baselineClass"
$expectedBaselineTests = 14
$approvedTypeCompilerCodes = @('CS0122', 'CS0234', 'CS0246', 'CS0426', 'CS0433')
$approvedMemberCompilerCodes = @('CS0117', 'CS0122')
$approvedNegativeCompilerCodes = @(
    $approvedTypeCompilerCodes + $approvedMemberCompilerCodes | Sort-Object -Unique)
$maximumProbesPerBatch = 12

try {
    $buildTarget = if ($FreshPack) { Join-Path $repoRoot 'OrcaCore.slnx' } else { $project }
    $build = & dotnet build $buildTarget `
        --configuration Release --no-restore --nologo --verbosity quiet 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Current source failed before public API verification.`n$build" }

    if ($FreshPack) {
        $runId = [Guid]::NewGuid().ToString('N')
        $feed = Join-Path $PSScriptRoot "obj\public-api-pack\$runId"
        New-Item -ItemType Directory -Path $feed -Force | Out-Null
        $packageCache = Join-Path $PSScriptRoot "obj\p\$runId"
        $pack = & (Join-Path $PSScriptRoot 'pack-exact-package-feed.ps1') `
            -OutputDirectory $feed -NoBuild 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "Exact current-source package feed failed.`n$pack" }
        $seed = & (Join-Path $PSScriptRoot 'seed-exact-package-cache.ps1') `
            -Feed $feed -PackageCache $packageCache -PackageVersion $contract.packageVersion 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "Exact package cache seeding failed.`n$seed" }
        $env:ORCACORE_PUBLIC_API_PACKAGE_FEED = $feed

        $negativeProject = Join-Path $PSScriptRoot 'CompileFixtures\ProductForbiddenLegacySurface\ProductForbiddenLegacySurface.csproj'
        $negativeSource = Join-Path (Split-Path -Parent $negativeProject) 'ForbiddenLegacySurface.cs'
        $negativeSourceLines = [IO.File]::ReadAllLines($negativeSource)
        $probeMarker = '// FORBIDDEN:'
        $probeRoot = Join-Path $PSScriptRoot "obj\forbidden-probes\$runId"
        [IO.Directory]::CreateDirectory($probeRoot) | Out-Null
        $seenProbes = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $probeRecords = [Collections.Generic.List[object]]::new()
        $probeDirectories = [Collections.Generic.List[string]]::new()
        for ($index = 0; $index -lt $negativeSourceLines.Length; $index++) {
            $sourceLine = $negativeSourceLines[$index]
            $markerIndex = $sourceLine.IndexOf($probeMarker, [StringComparison]::Ordinal)
            if ($markerIndex -lt 0) { continue }

            $probe = $sourceLine.Substring($markerIndex + $probeMarker.Length).Trim()
            if ([string]::IsNullOrWhiteSpace($probe) -or -not $seenProbes.Add($probe)) {
                throw "Forbidden legacy-surface compiler probe is blank or duplicated at line $($index + 1): '$probe'."
            }

            $probeParts = $probe.Split([string[]]@('::'), [StringSplitOptions]::None)
            if ($probeParts.Count -notin 2, 3 -or [string]::IsNullOrWhiteSpace($probeParts[0]) -or
                [string]::IsNullOrWhiteSpace($probeParts[1])) {
                throw "Forbidden legacy-surface marker at line $($index + 1) must be 'Package::Type' or 'Package::Type::Member'."
            }
            $typeIdentity = $probeParts[1]
            $genericMatch = [regex]::Match($typeIdentity, '^(?<name>.+)`(?<arity>[1-9][0-9]*)$')
            if ($genericMatch.Success) {
                $genericArguments = @(
                    1..([int]$genericMatch.Groups['arity'].Value) | ForEach-Object { 'object' }) -join ', '
                $typeReference = "$($genericMatch.Groups['name'].Value)<$genericArguments>"
            }
            else {
                $typeReference = $typeIdentity
            }

            $probeNumber = $probeRecords.Count + 1
            $probeName = 'ForbiddenProbe{0:D3}.cs' -f $probeNumber
            $batchNumber = [int][Math]::Floor(($probeNumber - 1) / $maximumProbesPerBatch)
            $probeDirectory = Join-Path $probeRoot ('batch-{0:D2}' -f $batchNumber)
            if (-not [IO.Directory]::Exists($probeDirectory)) {
                [IO.Directory]::CreateDirectory($probeDirectory) | Out-Null
                $probeDirectories.Add($probeDirectory)
            }
            if ($probeParts.Count -eq 2) {
                $generatedSource = "namespace ProductForbiddenLegacySurface.Generated;`r`n`r`npublic static class ForbiddenProbe$('{0:D3}' -f $probeNumber)`r`n{`r`n    public static System.Type Value => typeof(global::$typeReference);`r`n}`r`n"
                $allowedCodes = $approvedTypeCompilerCodes
            }
            else {
                $memberName = $probeParts[2]
                if ([string]::IsNullOrWhiteSpace($memberName)) {
                    throw "Forbidden legacy-surface member marker at line $($index + 1) has a blank member name."
                }
                $generatedSource = "namespace ProductForbiddenLegacySurface.Generated;`r`n`r`npublic static class ForbiddenProbe$('{0:D3}' -f $probeNumber)`r`n{`r`n    public const string Value = nameof(global::$typeReference.$memberName);`r`n}`r`n"
                $allowedCodes = $approvedMemberCompilerCodes
            }

            [IO.File]::WriteAllText(
                (Join-Path $probeDirectory $probeName),
                $generatedSource,
                [Text.UTF8Encoding]::new($false))
            $probeRecords.Add([pscustomobject]@{
                Name = $probeName
                Symbol = $probe
                Directory = $probeDirectory
                AllowedCodes = $allowedCodes
                DiagnosticOutput = $null
            })
        }
        if ($probeRecords.Count -eq 0) {
            throw "Forbidden legacy-surface fixture contains no '$probeMarker' compiler probes."
        }

        $negativeRestore = & dotnet restore $negativeProject --force --no-cache --nologo --verbosity quiet `
            -p:Phase0PackageFeed=$feed -p:Phase0PackageVersion=$($contract.packageVersion) `
            -p:Phase0PackageCache=$packageCache -p:ForbiddenProbeDirectory=$($probeDirectories[0]) 2>&1 | Out-String
        if ($LASTEXITCODE -ne 0) { throw "Forbidden legacy-surface package fixture failed to restore.`n$negativeRestore" }
        $negativeBuild = ''
        $negativeBuildByDirectory = @{}
        foreach ($probeDirectory in $probeDirectories) {
            $compileItemOutput = & dotnet msbuild $negativeProject -nologo -getItem:Compile `
                -p:ForbiddenProbeDirectory=$probeDirectory 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) {
                throw "Forbidden legacy-surface compile-item discovery failed for '$probeDirectory'.`n$compileItemOutput"
            }
            $actualCompileNames = @((ConvertFrom-Json $compileItemOutput).Items.Compile |
                ForEach-Object { [IO.Path]::GetFileName($_.Identity) } | Sort-Object)
            $expectedCompileNames = @($probeRecords | Where-Object Directory -eq $probeDirectory |
                ForEach-Object Name | Sort-Object)
            if ([string]::Join("`n", $actualCompileNames) -cne [string]::Join("`n", $expectedCompileNames)) {
                throw "Forbidden legacy-surface compile inputs differ for '$probeDirectory'. Expected [$($expectedCompileNames -join ', ')], found [$($actualCompileNames -join ', ')]."
            }

            $savedErrorActionPreference = $ErrorActionPreference
            try {
                # Batches keep the lane fast; any source whose diagnostic is suppressed by the
                # compiler is recompiled alone below before it can receive coverage credit.
                $ErrorActionPreference = 'Continue'
                $batchBuild = & dotnet build $negativeProject --configuration Release --no-restore --no-incremental `
                    --nologo --verbosity quiet -p:Phase0PackageFeed=$feed `
                    -p:Phase0PackageVersion=$($contract.packageVersion) -p:Phase0PackageCache=$packageCache `
                    -p:ForbiddenProbeDirectory=$probeDirectory 2>&1 | Out-String
                $batchBuildExitCode = $LASTEXITCODE
            }
            finally {
                $ErrorActionPreference = $savedErrorActionPreference
            }
            if ($batchBuildExitCode -eq 0) {
                $compiledSources = [string]::Join("`n---`n", @(
                    Get-ChildItem -LiteralPath $probeDirectory -Filter '*.cs' |
                        Sort-Object Name | ForEach-Object { "[$($_.Name)]`n$([IO.File]::ReadAllText($_.FullName))" }))
                throw "Forbidden legacy-surface batch '$([IO.Path]::GetFileName($probeDirectory))' unexpectedly compiled against fresh packages.`n$compiledSources`n$batchBuild"
            }
            $negativeBuild += $batchBuild
            $negativeBuildByDirectory[$probeDirectory] = $batchBuild
        }

        foreach ($record in $probeRecords) {
            $diagnosticPattern = '(?m)' + [regex]::Escape($record.Name) +
                '\(\d+,\d+\): error (?<code>[A-Z]+\d+):'
            $recordOutput = $negativeBuildByDirectory[$record.Directory]
            if ($recordOutput -notmatch $diagnosticPattern) {
                $isolatedDirectory = Join-Path $probeRoot ('isolated-{0}' -f [IO.Path]::GetFileNameWithoutExtension($record.Name))
                [IO.Directory]::CreateDirectory($isolatedDirectory) | Out-Null
                Copy-Item -LiteralPath (Join-Path $record.Directory $record.Name) `
                    -Destination (Join-Path $isolatedDirectory $record.Name)

                $isolatedCompileItemOutput = & dotnet msbuild $negativeProject -nologo -getItem:Compile `
                    -p:ForbiddenProbeDirectory=$isolatedDirectory 2>&1 | Out-String
                if ($LASTEXITCODE -ne 0) {
                    throw "Forbidden legacy-surface isolated compile-item discovery failed for '$($record.Symbol)'.`n$isolatedCompileItemOutput"
                }
                $isolatedCompileNames = @((ConvertFrom-Json $isolatedCompileItemOutput).Items.Compile |
                    ForEach-Object { [IO.Path]::GetFileName($_.Identity) } | Sort-Object)
                if ($isolatedCompileNames.Count -ne 1 -or $isolatedCompileNames[0] -cne $record.Name) {
                    throw "Forbidden legacy-surface isolated compile inputs differ for '$($record.Symbol)'. Expected '$($record.Name)', found [$($isolatedCompileNames -join ', ')]."
                }

                $savedErrorActionPreference = $ErrorActionPreference
                try {
                    $ErrorActionPreference = 'Continue'
                    $isolatedBuild = & dotnet build $negativeProject --configuration Release --no-restore --no-incremental `
                        --nologo --verbosity quiet -p:Phase0PackageFeed=$feed `
                        -p:Phase0PackageVersion=$($contract.packageVersion) -p:Phase0PackageCache=$packageCache `
                        -p:ForbiddenProbeDirectory=$isolatedDirectory 2>&1 | Out-String
                    $isolatedBuildExitCode = $LASTEXITCODE
                }
                finally {
                    $ErrorActionPreference = $savedErrorActionPreference
                }
                if ($isolatedBuildExitCode -eq 0) {
                    $isolatedSource = [IO.File]::ReadAllText((Join-Path $isolatedDirectory $record.Name))
                    throw "Forbidden legacy-surface probe '$($record.Symbol)' unexpectedly compiled in isolation.`n$isolatedSource`n$isolatedBuild"
                }
                $recordOutput = $isolatedBuild
                $negativeBuild += $isolatedBuild
            }
            $record.DiagnosticOutput = $recordOutput
        }

        $negativeErrorCodes = @([regex]::Matches($negativeBuild, '(?m): error (?<code>[A-Z]+\d+):') |
            ForEach-Object { $_.Groups['code'].Value } | Sort-Object -Unique)
        $unapprovedErrorCodes = @($negativeErrorCodes | Where-Object { $approvedNegativeCompilerCodes -notcontains $_ })
        if ($negativeErrorCodes.Count -eq 0 -or $unapprovedErrorCodes.Count -ne 0) {
            throw "Forbidden legacy-surface fixture produced no compiler diagnostics or unapproved codes [$($unapprovedErrorCodes -join ', ')].`n$negativeBuild"
        }

        foreach ($record in $probeRecords) {
            $diagnosticPattern = '(?m)' + [regex]::Escape($record.Name) +
                '\(\d+,\d+\): error (?<code>[A-Z]+\d+):'
            $probeDiagnostics = @([regex]::Matches(
                $record.DiagnosticOutput,
                $diagnosticPattern))
            if ($probeDiagnostics.Count -eq 0) {
                throw "Forbidden packed-consumer probe '$($record.Symbol)' in '$($record.Name)' did not produce its own approved missing/inaccessible-symbol diagnostic.`n$($record.DiagnosticOutput)"
            }
            $probeCodes = @($probeDiagnostics | ForEach-Object { $_.Groups['code'].Value } | Sort-Object -Unique)
            $wrongLevelCodes = @($probeCodes | Where-Object { $record.AllowedCodes -notcontains $_ })
            if ($wrongLevelCodes.Count -ne 0) {
                throw "Forbidden packed-consumer probe '$($record.Symbol)' in '$($record.Name)' produced wrong-level diagnostics [$($wrongLevelCodes -join ', ')]; expected only [$($record.AllowedCodes -join ', ')].`n$($record.DiagnosticOutput)"
            }
        }
        Write-Output "Forbidden fresh-package surface probes: $($probeRecords.Count)/$($probeRecords.Count) produced isolated compiler diagnostics."
    }

    $listedTests = & dotnet test $project --configuration Release --no-build --no-restore --nologo --list-tests `
        --filter $baselineFilter 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Public API baseline test discovery failed.`n$listedTests" }
    $matchedTests = @(($listedTests -split "`r?`n") | Where-Object {
        $_.Trim().StartsWith("$baselineClass.", [StringComparison]::Ordinal)
    })
    if ($matchedTests.Count -ne $expectedBaselineTests) {
        throw "Expected exactly $expectedBaselineTests public API baseline tests, found $($matchedTests.Count).`n$listedTests"
    }

    $output = & dotnet test $project --configuration Release --no-build --no-restore --nologo --verbosity quiet `
        --filter $baselineFilter 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) { throw "Public API baseline verification failed.`n$output" }
    Write-Output $output.Trim()
}
finally {
    Remove-Item Env:ORCACORE_PUBLIC_API_PACKAGE_FEED -ErrorAction SilentlyContinue
    if ($null -ne $feed -and (Test-Path -LiteralPath $feed)) {
        Remove-Item -LiteralPath $feed -Recurse -Force
    }
    if ($null -ne $packageCache -and (Test-Path -LiteralPath $packageCache)) {
        Remove-Item -LiteralPath $packageCache -Recurse -Force
    }
    if ($null -ne $probeRoot -and (Test-Path -LiteralPath $probeRoot)) {
        Remove-Item -LiteralPath $probeRoot -Recurse -Force
    }
}
