# Ephemeral wait-ownership pilot

This development-only pilot checks a finite model of OrcaCore's ephemeral correlation ownership. It does not verify C# automatically or establish durable guarantees. See [crosswalk.md](crosswalk.md) for assumptions, source/test mapping, and the confirmed WOP-F001 rollback discrepancy.

## Run

From the repository root with PowerShell 7:

```powershell
./scripts/verification/Invoke-WaitOwnershipModelCheck.ps1
./scripts/verification/Test-WaitOwnershipRunner.ps1
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj -c Release
```

The runner downloads checksum-pinned TLC 1.7.4 and Temurin JRE 21.0.9+10 into the ignored `.codex-run/tla-tools/` directory. It makes no machine-wide Java installation, registry, PATH, NuGet, or runtime-package change. Windows/Linux x64 are pinned; local validation was on Windows. The adopted workflow uses this private runtime locally and in CI.

Select profiles from an existing PowerShell session:

```powershell
./scripts/verification/Invoke-WaitOwnershipModelCheck.ps1 -Profile @('SafetySmall','Mutant-*')
```

Default limits are one worker, 1 GiB heap, and 300 seconds per profile. A deterministic TLC seed/fingerprint polynomial makes counterexample output easier to compare. The CI job has a 15-minute overall budget and retains evidence on failure. It does not alter branch protection.

## Files and outcomes

- `WaitOwnershipContract.tla`: independently written contract predicates.
- `EphemeralWaitOwnership.tla`: source-oriented transition protocol and separate assumption-control specifications.
- `SafetySmall.cfg`, `SafetyIsolation-*.cfg`, `ProgressSmall.cfg`: normal source checks. Any invariant, temporal, parse, or resource failure prevents a pass.
- `Mutant-*.cfg`: six intentional faults; only the specified property and an actual counterexample qualify.
- `Witness-*.cfg`: fourteen reachable scenario traces, including successor-then-failure.
- `Control-*.cfg`: a positive completed-progress control and explicit removal of return or scheduling assumptions.
- `profiles.json`: expected result type; normal checks never accept the known runtime violation as success.
- [`../toolchain.json`](../toolchain.json): artifact URLs, versions, and SHA-256 pins.

Outputs go to a fresh ignored `.codex-run/wait-ownership/<timestamp>/` directory. `results.json` records each classification, command, limit, state counts, duration, and violated property. `source-manifest.json` hashes production source, tests, model, configuration, scripts, and planning inputs; the top-level record also stores HEAD and a dirty-diff hash. Each profile retains its exact module/config copies and `tlc.log` trace. Untracked inputs appear in the source manifest, so a clean-HEAD identifier alone is not mistaken for evidence of new files.

Exit code 0 means all selected profiles met their declared expectations; it does not grant whole-program correctness. Exit code 1 includes failed normal checks, wrong negative-control outcomes, and inconclusive time/memory exhaustion. The runner continues through selected profiles to collect all outcomes. Individual source safety failures terminate that TLC search before exhaustive completion; retained state counts are explored prefixes, not total state-space sizes.

## Current finding

WOP-F001: caller cancellation after successor registration restores the old wait while retaining the new same-key wait. The model and executable invariant regression both fail as intended on the baseline. The pilot must report failed qualification until the separately scoped runtime correction is validated. Normal profiles and the C# suite deliberately remain red; no skip or weakened assertion disguises the defect.

Official tool sources: [TLA+ release](https://github.com/tlaplus/tlaplus/releases/tag/v1.7.4), [Temurin release](https://github.com/adoptium/temurin21-binaries/releases/tag/jdk-21.0.9%2B10). These are pinned development tools, not a recommendation to adopt a system Java version.
