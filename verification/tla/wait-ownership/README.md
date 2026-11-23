# Ephemeral wait-ownership pilot

The [protocol verification guide](../README.md) lists the models, their bounds, and the requirements alignment review.

This development-only pilot checks a finite model of OrcaCore's ephemeral correlation ownership. It does not verify C# automatically or establish durable guarantees. See [crosswalk.md](crosswalk.md) for assumptions, source/test mapping, and the historical WOP-F001 rollback discrepancy.

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
- `Mutant-*.cfg`: eight intentional faults; only the specified property and an actual counterexample qualify.
- `Witness-*.cfg`: fifteen reachable scenario traces, including committed handover.
- `Control-*.cfg`: a positive completed-progress control and explicit removal of return or scheduling assumptions.
- `profiles.json`: expected result type; normal checks never accept the known runtime violation as success.
- [`../toolchain.json`](../toolchain.json): artifact URLs, versions, and SHA-256 pins.

Outputs go to a fresh ignored `.codex-run/wait-ownership/<timestamp>/` directory. `results.json` records each classification, command, limit, state counts, duration, and violated property. `source-manifest.json` hashes production source, tests, model, configuration, scripts, and planning inputs; the top-level record also stores HEAD and a dirty-diff hash. Each profile retains its exact module/config copies and `tlc.log` trace. Untracked inputs appear in the source manifest, so a clean-HEAD identifier alone is not mistaken for evidence of new files.

Exit code 0 means all selected profiles met their declared expectations; it does not grant whole-program correctness. Exit code 1 includes failed normal checks, wrong negative-control outcomes, and inconclusive time/memory exhaustion. The runner continues through selected profiles to collect all outcomes. Individual source safety failures terminate that TLC search before exhaustive completion; retained state counts are explored prefixes, not total state-space sizes.

## Current implementation

An authored event wait consumes its event on the instance lane when its continuation takes ownership. The structured adapter verifies the exact blocked wait, installs the envelope, and enqueues the fiber before executing the next instruction. The original correlation reservation survives the turn. Caller cancellation before admission leaves A eligible. After acceptance, the instance drives the continuation to its next boundary and returns a routing receipt. Instance cancellation and host stopping still apply.

`AcceptEvent` records successful resolution before `Successor` can make B active. `HandoverOrder` forbids a new active wait while its predecessor event is still uncommitted. The uncommitted-successor mutation violates that property. The two focused profiles start with one matched wait and explore retries, duplicates, fresh delivery, a successor, and a competing registration. Both finished with 5,025 distinct states. The six original broad profiles reached their 300-second limit. Full-bound qualification remains open.

The [pilot report](verification-report.md) and [crosswalk](crosswalk.md) describe the failing baseline. The current same-key and cross-key schedules and the throwing-predicate regression are in [WaitOwnershipModelTests.cs](../../../tests/OrcaCore.Engine.Ephemeral.Tests/Execution/WaitOwnershipModelTests.cs).

Official tool sources: [TLA+ release](https://github.com/tlaplus/tlaplus/releases/tag/v1.7.4), [Temurin release](https://github.com/adoptium/temurin21-binaries/releases/tag/jdk-21.0.9%2B10). These are pinned development tools, not a recommendation to adopt a system Java version.
