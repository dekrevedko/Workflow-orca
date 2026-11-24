[CmdletBinding()]
param(
    [string]$Repository = (Get-Location).Path,
    [Parameter(Mandatory)][string]$Revision,
    [string]$Dotnet = 'dotnet',
    [ValidateRange(1, 4)][int]$Rounds = 2
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path -LiteralPath $Repository).Path
$runtimeExe = (Get-Command $Dotnet -ErrorAction Stop).Source
$commit = (git -C $repoRoot rev-parse --verify "$Revision^{commit}").Trim()
if ($LASTEXITCODE) { throw 'Cannot resolve the source revision.' }
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('orca-retention-' + [guid]::NewGuid().ToString('N'))
git clone --quiet --shared --local --no-checkout $repoRoot $scratch
if ($LASTEXITCODE) { throw 'Cannot create the measurement checkout.' }
git -C $scratch checkout --quiet --detach $commit
if ($LASTEXITCODE) { throw 'Cannot check out the selected revision.' }

function Replace-Once([string]$Text, [string]$From, [string]$To) {
    if ($Text.IndexOf($From, [StringComparison]::Ordinal) -lt 0 -or
        $Text.IndexOf($From, [StringComparison]::Ordinal) -ne $Text.LastIndexOf($From, [StringComparison]::Ordinal)) {
        throw "Measurement anchor changed. Review the fixture against the selected revision: $From"
    }
    return $Text.Replace($From, $To)
}

Push-Location $scratch
try {
    $engine = Join-Path $scratch 'src/OrcaCore.Engine.Ephemeral'
    $project = Join-Path $engine 'OrcaCore.Engine.Ephemeral.csproj'
    $coordinator = Join-Path $engine 'Execution/EphemeralRetentionCoordinator.cs'
    $code = [IO.File]::ReadAllText($coordinator).Replace("`r`n", "`n")
    $projectText = [IO.File]::ReadAllText($project)
    $indexPath = Join-Path $engine 'Execution/EphemeralRetentionIndex.cs'
    $indexed = Test-Path -LiteralPath $indexPath
    $constants = if ($indexed) { '<DefineConstants>$(DefineConstants);INDEXED</DefineConstants>' } else { '' }
    [IO.File]::WriteAllText($project, (Replace-Once $projectText '<PropertyGroup>' ("<PropertyGroup><OutputType>Exe</OutputType><TieredCompilation>false</TieredCompilation>" + $constants)))
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RetentionProbe.cs') -Destination (Join-Path $engine 'RetentionProbe.cs')
    # Leave timestamp/ordinal assignment intact. Only bulk fixture construction defers selection.
    if ($indexed) {
        $indexCode = [IO.File]::ReadAllText($indexPath).Replace("`r`n", "`n")
        $base = $code
        $inside = '            cleanup = index?.Select(now, minimumAge, policy.MaxRetainedInstances);'
    } else {
        $base = Replace-Once $code "`n        Enforce();`n" "`n        if (!RetentionProbe.Seeding) Enforce();`n"
        $inside = "            if (disposed || policy is null) return;`n            var eligible"
    }
    $anchor = "        lock (Gate)`n        {`n$inside"
    $timed = Replace-Once $base $anchor ("        var waiting = System.Diagnostics.Stopwatch.GetTimestamp();`n" +
        "        lock (Gate)`n        {`n            using var sample = RetentionProbe.EnterSelection(waiting);`n$inside")
    foreach ($mode in 'timing', 'counts') {
        $instrumented = $timed
        if ($mode -eq 'counts' -and $indexed) {
            # Count tree comparisons with the same default value comparison on both orderings.
            $counted = Replace-Once $indexCode 'byOrdinal = [];' 'byOrdinal = new(new RetentionProbe.CountingComparer<long>());'
            $counted = Replace-Once $counted 'byAge = [];' 'byAge = new(new RetentionProbe.CountingComparer<(ulong Timestamp, long Ordinal)>());'
            $counted = Replace-Once $counted 'byOrdinal.Add(entry.Ordinal, entry);' "RetentionProbe.IndexAdd();`n        byOrdinal.Add(entry.Ordinal, entry);"
            $counted = Replace-Once $counted 'if (byOrdinal.Remove(entry.Ordinal))' "RetentionProbe.IndexRemove();`n        if (RetentionProbe.Removed(byOrdinal.Remove(entry.Ordinal)))"
            if ($counted.Contains('byAge.Clear();')) {
                $counted = Replace-Once $counted 'byAge.Clear();' "RetentionProbe.BulkClear();`n                byAge.Clear();"
                $counted = Replace-Once $counted 'if (unchecked(now - (long)entry.Timestamp)' "RetentionProbe.CheckDenseEntry();`n                    if (unchecked(now - (long)entry.Timestamp)"
                $counted = Replace-Once $counted 'var minimum = byAge.Min.Timestamp;' "RetentionProbe.AgeRange();`n        var minimum = byAge.Min.Timestamp;"
            } else {
                $counted = Replace-Once $counted 'if (byAge.Min.Timestamp > last' "RetentionProbe.AgeRange();`n        if (byAge.Min.Timestamp > last"
            }
            $counted = Replace-Once $counted '(expired ??= []).Add(key.Ordinal);' '(expired ??= []).Add(RetentionProbe.ExpiredKey(key.Ordinal));'
            $counted = Replace-Once $counted '(selected ??= []).Add((entry.Ordinal, release));' "RetentionProbe.Selected();`n            (selected ??= []).Add((entry.Ordinal, release));"
            [IO.File]::WriteAllText($indexPath, $counted)
        } elseif ($mode -eq 'counts') {
            $instrumented = Replace-Once $instrumented 'e => e.Ordinal != 0 && e.Lifetime.IsEligible' 'e => RetentionProbe.Visit() && e.Ordinal != 0 && RetentionProbe.CheckEligibility() && e.Lifetime.IsEligible'
            $instrumented = Replace-Once $instrumented '.OrderBy(e => e.Ordinal)' '.OrderBy(e => RetentionProbe.SortKey(e.Ordinal))'
            $instrumented = Replace-Once $instrumented 'eligible.Where(e => (decimal)' 'eligible.Where(e => RetentionProbe.CheckAge() && (decimal)'
            $instrumented = Replace-Once $instrumented 'if (!expired.Contains(entry) &&' "RetentionProbe.ConsiderRetirement();`n                if (!expired.Contains(entry) &&"
            $instrumented = Replace-Once $instrumented 'cleanup.Add(release);' "RetentionProbe.Selected();`n                cleanup.Add(release);"
        }
        [IO.File]::WriteAllText($coordinator, $instrumented)
        & $runtimeExe build $project -c Release -warnaserror --nologo -v quiet
        if ($LASTEXITCODE) { throw 'Measurement build failed.' }
        $dll = Join-Path $engine 'bin/Release/net10.0/OrcaCore.Engine.Ephemeral.dll'
        $repeats = if ($mode -eq 'timing') { $Rounds } else { 1 }
        foreach ($run in 1..$repeats) {
            $log = Join-Path $scratch "$mode-$run.jsonl"
            & $runtimeExe $dll $mode $commit > $log
            if ($LASTEXITCODE) { throw "Measurement failed. Inspect $log" }
            Write-Host "$mode run $run completed. $log"
        }
    }
} finally { Pop-Location }
Write-Host "Source $commit. Raw rows and scratch instrumentation retained at $scratch."
