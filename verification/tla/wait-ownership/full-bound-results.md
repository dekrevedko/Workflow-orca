# Full-bound ephemeral wait-ownership qualification

**Passed within the pilot bounds on 2026-11-23.** Both literal observers completed all six configurations on Windows x64 at source commit `66f0bc15d5f29c977a669a755ecf2ef2ac7804ac`. Every queue emptied. Both progress searches also finished checking all five temporal branches. This qualification covers the finite ephemeral protocol and the selected C# schedules.

| Joint configuration projected | Target 1 distinct states | Target 2 distinct states | Wall seconds, target 1 / target 2 |
|---|---:|---:|---:|
| SafetySmall | 895,311 | 895,311 | 72.1 / 75.2 |
| SafetyIsolation-definition | 1,583,379 | 1,583,379 | 146.2 / 151.6 |
| SafetyIsolation-name | 1,583,379 | 1,583,379 | 149.4 / 165.2 |
| SafetyIsolation-version | 1,583,379 | 1,583,379 | 135.9 / 138.2 |
| SafetyIsolation-correlation | 1,583,379 | 1,583,379 | 148.0 / 149.7 |
| ProgressSmall | 895,311 | 895,311 | 447.4 / 449.8 |

Each search retains two instances, two occurrence slots per instance, two event identities, at most two delivery attempts per event per target, and one escaping failure per occurrence. The observer runs the source actions. Its peer can exercise every shared reservation change visible to the observer. The peer model admits additional histories. The table therefore reports states of each projection separately. Progress assumes weakly fair internal work and eventual continuation return. [Coverage argument and action mapping](composition.md)

The original six joint runs remain inconclusive at 300 seconds. In a further 600-second, four-worker safety experiment on the original joint model at `80a6a922` the last periodic report still had 26,288,102 queued states after visiting 67,079,000 distinct states. The private JRE and pinned TLC also ran the original focused and diagnostic configurations on the qualification source. Both handover searches passed at 5,025 distinct states apiece. All fifteen reachability witnesses, eight seeded faults and two removed-fairness controls produced their expected traces. The positive progress control passed. All six runner self-test classifications matched expectations, and all 452 tests in the ephemeral engine project passed without skips.

## Evidence

The [qualification archive](evidence/2026-11-23/wait-ownership-qualification.zip) contains all twelve target configurations, TLC logs and result records, the longer joint safety log, all focused and control configurations and logs, the three model modules, the source manifest, the runner self-test results and the C# TRX. Its SHA-256 is `af689697f81573cfece31860cda04eb647f3b6ef49b67f93e951a4606e28c52d`. All twelve target runs and all twenty-eight focused and control runs recorded the qualification revision and an empty tracked diff. All target runs used a single worker, a 2 GiB heap, TLA+ tools release v1.7.4 (TLC 2.19) and the private Temurin 21.0.9+10 JRE.

## Independent review

Separate design and implementation reviews accepted the two-target coverage argument and the executable interface, local-deadlock and fairness checks. The evidence reviewer approved closing task 2.5 at `66f0bc15` after checking all 639 manifest entries against Git, the toolchain pins, the twelve literal configurations and both completed temporal analyses. The reviewer also checked the control traces and the executed C# tests for same-key and cross-key handover, exact release and failure after successor registration.

## Reproduce

Run the following in PowerShell 7 from the repository root. Use a fresh evidence directory for each invocation. CI runs the same twelve profile/observer pairs in a matrix. The one-hour per-profile ceiling leaves room for hosted runner variation. Each progress check performs its temporal analysis after constructing the complete graph.

```powershell
$profiles = 'SafetySmall', 'SafetyIsolation-definition', 'SafetyIsolation-name',
            'SafetyIsolation-version', 'SafetyIsolation-correlation', 'ProgressSmall'
foreach ($profile in $profiles) {
    foreach ($observer in 1, 2) {
        ./scripts/verification/Invoke-WaitOwnershipTargetCheck.ps1 -Profile $profile -Observer $observer `
            -OutputDirectory ".codex-run/qualification/$profile-target-$observer"
        if ($LASTEXITCODE) { throw "Failed $profile for target $observer" }
    }
}
./scripts/verification/Invoke-WaitOwnershipModelCheck.ps1 -Profile @('Handover*','Mutant-*','Control-*','Witness-*')
./scripts/verification/Test-WaitOwnershipRunner.ps1
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests -c Release
```
