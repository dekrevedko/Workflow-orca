# Wait-ownership pilot verification report

Date: 2026-11-17. Verdict: **FAILED runtime qualification; useful diagnostic pilot**.

The pilot found a source-backed ownership defect, WOP-F001. Caller cancellation
after a successor wait is registered causes rollback to restore its predecessor
without removing the successor. Both same-key occurrences remain active. The
normal model and a deterministic C# regression independently expose this state.
No production runtime correction is included. This branch intentionally fails
normal model checks and one C# assertion until a separately scoped correction is
implemented and validated. It is not ready for qualification or archival.

## Scope and reproducibility

The source baseline is proposal commit
`323d741d4c33178430250729b4fa34f1eeb61703`; production source is inherited unchanged
from `ba2b057d7898f516845e90323b4288fb0c1744d8`. The working branch is
`codex/tla-wait-ownership-pilot`. This report and the retained evidence belong to
the subsequent implementation commit; the run record uses its actual pre-commit
HEAD and dirty-diff hash, not a fabricated clean revision.

The model run's [results](evidence/2026-11-17/model/results.json) record dirty-diff
SHA-256 `670EAA89B80C8C0EDA3DDF2F561FD8479B84DE6825ACA86FBE5B8EC789E7BE9F`.
Its [source manifest](evidence/2026-11-17/model/source-manifest.json) hashes tracked
and untracked inputs. Each profile directory preserves its executed modules,
configuration, and complete TLC output. Report/review/task closeout edits
followed that run; they do not change the executed model, runner, production
source, or C# tests. The evidence manifest records final retained artifact hashes.
Historical absolute command paths in JSON/logs identify the actual local run;
they are not portable invocation instructions.

Bounds: two instances, two occurrence slots per instance, two event identities,
at most two submissions per identity per target, and one escaping failure per
occurrence. Full-key isolation varies definition, contract name, contract version,
and correlation independently. Counters do not wrap. No symmetry or state-space
constraint silently prunes a violating prefix. Consumed history stays far below
the runtime's 10,000-ID capacity; eviction is outside scope.

The [crosswalk](crosswalk.md) maps WOP-001 through WOP-009 to requirements, source
symbols, atomic boundaries, tests, fairness assumptions, and exclusions. Safety
covers state types, unique active occurrence, reservation ownership, single
resolution, exact release, rollback, and key/occurrence isolation. Progress is
conditional on return and scheduling fairness; the pilot does not promise event
arrival, universal workflow termination, or fairness between perpetual claimants.
It is a manually reviewed abstraction, not a mechanized C# refinement proof.

Tools: TLC release v1.7.4 (TLC2 2.19), Temurin JRE 21.0.9+10, PowerShell 7,
.NET SDK 10.0.403, Windows x64. The [toolchain manifest](toolchain.json) pins official
artifact URLs and SHA-256 hashes. Java is private under ignored `.codex-run`;
there is no system Java installation. Linux artifacts are pinned but not locally
executed. The dedicated Windows CI workflow is implemented but has not run hosted.

## Results

All 29 profiles executed with one worker, 1 GiB heap, seed 1, fingerprint polynomial
0, and a 300-second per-profile limit. Total measured checker-process time was
71.660 seconds; the slowest profile was 11.741 seconds. This is comfortably within
the local budget but is not an estimate of exhaustive normal exploration after a
runtime/model correction. The 15-minute CI job budget remains unqualified hosted.

| Group | Result | Meaning |
|---|---|---|
| Six normal safety/progress profiles | 6 failed `UniqueActiveWait` | WOP-F001; no normal qualification |
| Six injected-fault controls | 6 expected counterexamples | Each required property detected |
| Fourteen reachability witnesses | 14 expected counterexamples | Each target scenario reached |
| Two assumption-removal controls | 2 expected temporal counterexamples | Non-progress without return/scheduling |
| Positive progress control | 1 passed | Restricted admitted continuation completes |
| Runner classification self-tests | 6 passed | Correct handling of pass, expected/unrelated failure, parse error, timeout, unrelated temporal property |
| Full ephemeral C# project | 423 passed, 1 failed, 0 skipped | Only new WOP-F001 invariant regression fails |
| OpenSpec/provenance and task guards | 18 passed, 0 failed, 0 skipped | Rebuilt final rerun; [TRX](evidence/2026-11-17/guards.trx) |
| Strict OpenSpec validation | Passed | Structural consistency, not runtime correctness |

Normal `SafetySmall` and `ProgressSmall` each explored 68,890 generated / 25,649
distinct states with 14,737 queued when the violation stopped search. Each of the
four isolation profiles explored 279,387 / 87,245 with 52,092 queued. These are
explored prefixes, not complete state-space sizes. The positive control completed
with five generated / four distinct states and zero queued. Per-profile counts,
commands, limits, durations, property failures, and traces are in the JSON/logs.

Mutation controls restrict escape to before successor registration solely to
isolate seeded faults from WOP-F001. They are not a repaired runtime model.
Witnesses require concrete winner/loser handling and live cancellation cleanup;
the stale callback check retains occurrence identity and compares before/after
state. The progress controls use a separate admitted-continuation initial state
and fairness only for their enabled actions. Their passing positive control
does not rescue the failed normal `ProgressSmall` profile.

## Executable source correspondence

The [TRX](evidence/2026-11-17/ephemeral.trx) includes ten new test cases: nine pass
and `CallerCancellationAfterSuccessorRegistration_PreservesUniqueActiveWait`
fails by observing two actual active waits where the contract requires one.
The schedule suspends the second correlation selector, cancels the ingress
caller's token from another thread, then releases the selector. No sleep orders
the race. Existing friend access inspects runtime state because the immediately
published snapshot can hide the restored predecessor.

The tests exercise public authoring through the established TestSupport router
bridge into production internals. They do not claim a currently shipped public
ephemeral ingress API. Other deterministic schedules cover reservation handover,
retry after escape, queued duplicates, stale callbacks, all four isolation fields,
and cancellation serialized behind the continuation. `WaitMatchingTests.cs` is
excluded from compilation and earns no executable-test credit.

WOP-F001 is a runtime discrepancy, not a reason to weaken WOP-002 or remove the
escape transition. A follow-up must define rollback of structured execution and
successor ownership, implement the correction, update the source-oriented model,
retain the existing invariant regression, and rerun all normal profiles and
controls to completion. Adding a cancellation check alone must not be assumed to
cover every escaping-continuation schedule. Durable ownership remains separate.

## Commands and retained evidence

Run from the repository root with PowerShell 7:

```powershell
./scripts/verification/Invoke-WaitOwnershipModelCheck.ps1 -OutputDirectory .codex-run/pilot-commit-candidate
./scripts/verification/Test-WaitOwnershipRunner.ps1 -OutputDirectory .codex-run/pilot-runner-reviewed
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj -c Release --logger 'trx;LogFileName=wait-ownership-final.trx' --results-directory .codex-run/tla-tests
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --filter 'FullyQualifiedName~OpenSpecCorpusGuards|FullyQualifiedName~TaskAccountingGuards' --logger 'trx;LogFileName=wait-ownership-guards-verified.trx' --results-directory .codex-run/tla-tests
openspec validate verify-ephemeral-wait-ownership-with-tla --strict
```

The first and third commands currently return failure intentionally. The runner
continues after a failed profile to preserve all evidence; its failure is not
converted into success. The self-test command succeeds only when all six result
classifications, including rejection of an unrelated temporal failure, are correct.
Retained evidence excludes downloaded binaries, TLC state databases, and build
outputs. The provenance TSV accounts for six new development requirements and one
active capability directory while preserving all previous 219 rows, canonical
content, and historical approvals. Guard logic is unchanged.

## Independent review and recommendation

Independent agents received scoped files and review questions, rather than the
entire implementation conversation. Source mapping, model/design, qualification,
runner/CI, and executable source correspondence were reviewed separately. Review
found and corrected weak race/cancellation witnesses, fairness applied to actions
outside a control relation, missing stale-callback behavior, and a runner false
positive for an unrelated temporal property. A later inventory review caught an
encoding change in historical guard assertions; the source was restored and the
patch narrowed to provenance pins/catalog metadata. Those scoped reviews reported
no remaining findings after correction. Final evidence review is recorded in
[implementation review record](../../../openspec/changes/verify-ephemeral-wait-ownership-with-tla/artifacts/implementation-review-record.md).

Recommendation: **extend narrowly with a separately scoped WOP-F001 correction**,
then rerun the unchanged contract obligations. The pilot already improved code
diagnostics and requirement precision by exposing a reachable rollback hole and
distinguishing reservation overlap from duplicate active waits. The measured
checker cost is low; the substantial cost was accurate abstraction, deterministic
source reproduction, and review. No reliable engineering-hour total was recorded.
Do not expand to durable handoff or install system Java merely because controls
pass. Adoption should follow a green corrected baseline and a hosted CI run.
