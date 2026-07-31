# Section 4 remediation: independent exit re-review request

**Date:** 2026-07-21  
**Requested verdict:** approve or reject Section 4 exit after independently verifying the six
reopened tasks and release blockers in the immutable
[Codex exit review](developer-facing-interface-section-04-exit-review-codex-2026-07-21.md).
Section 5 remains blocked; this request does not authorize task 5.0 or any Section-5 source work.

## Review snapshot

- Baseline: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- `HEAD`: `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`; tree
  `2264e670493ecc76359d42ee5273028eb287a566`.
- The baseline is an ancestor of `HEAD`; the stable commit is unchanged from the prior review.
- Review target: `HEAD` plus every entry in the
  [remediation dirty manifest](developer-facing-interface-section-04-remediation-dirty-manifest-2026-07-21.txt).
- Frozen manifest: 322 entries (284 modified, 5 deleted, 33 untracked); SHA-256
  `854903DCE160AF1E2D6CD3285DED6903851A4E9A8C4CDE1D3965ADF9D30E0CE7`.
- OpenSpec task state: 45 done, 67 pending, 112 total. All tasks 4.0 through 4.14 are checked;
  task 5.0 remains open.
- The two prior independent review files and all earlier manifests are immutable inputs. They were
  read but not edited.

## Release-blocker remediation

| Reopened task | Remediation to verify |
|---|---|
| 4.3 | The exact positive consumer uses statically typed metadata access. The green compile lane builds current `OrcaCore.Core`, packs current `OrcaCore` into a fresh fixture-local feed/cache, compiles the exact contract and product consumer, verifies 26 precise forbidden calls in both source and package fixtures, and rejects an incomplete-package mutation. No ignored repository `.nupkg` supplies the proof. |
| 4.5 | `ResultfulEnd_AfterHostReplacementPersistsOutputStatusAndOutcomeInOneTerminalCheckpoint` starts and parks a public durable workflow on one runtime, resumes it through a replacement runtime/registry, then reopens the persisted checkpoint and verifies typed output, terminal status, and fixed outcome together. |
| 4.7 | `PublicDurableNamedStep_IsActivatedFromHostServicesWithConstructorDependencies` authors a public durable `Then<TStep>()`, resolves the constructor-dependent step through the durable host service provider, executes it, and verifies dependency use. |
| 4.8 | The branch `Parallel` entry point is removed. The compiler rejects hand-built branch/item nested structured scopes as root-only capability violations, and focused compiler coverage exercises both placements. Obsolete nested-fan-out execution tests were removed rather than preserving a forbidden capability. |
| 4.12 | `LegacyWorkflowBuilder<TState>`, `WorkflowBuilder.cs`, `CompiledWorkflowPlan.CreateLegacyTestPlan`, `ContainsDurableOnlyNodes`, `requiresDurableEngine`, Boolean mode inference, and their duplicate consumers are deleted. A source guard rejects those exact symbols. Remaining tests author an explicit `Workflow.Ephemeral` or `Workflow.Durable` mode. |
| 4.14 | Behavior enforcement is per scenario, not per JSON fixture. Four Section-4 state/codec drivers are independently green; the remaining 91 behavior cases stay individually named expected-red. Passing authoring, strong-value, and state/codec checks moved to infrastructure. Redundant coarse presence gates were removed, and the package-journey gate now requires every exact referenced package rather than one stale artifact. |

The structural-fingerprint driver exposed an additional contract defect during remediation. The
compiler fingerprint now covers inspectable structure and configuration but uses opaque markers
for selector/projector/merge/output/condition bodies and captured values. Core regressions prove
that graph/compiler/partitioner changes alter the fingerprint while opaque captured changes do not.

## Reproduced evidence

| Lane | Result |
|---|---:|
| `dotnet build OrcaCore.slnx --no-restore` | succeeded, 0 warnings, 0 errors |
| Core | 404 passed, 0 failed, 0 skipped |
| Ephemeral | 148 passed, 0 failed, 0 skipped |
| Durable | 277 passed, 0 failed, 0 skipped |
| Guard infrastructure | 53 passed, 0 failed, 0 skipped |
| Expected-red guard lane | 0 passed, 104 intentional individually named failures, 0 skipped; 91 are executable behavior cases owned by Sections 5-9 |
| Green compile fixtures | fresh current-source package compiled; exact consumer compiled; 26 source and 26 package forbidden-member diagnostics verified; incomplete package rejected |
| Expected-red compile fixtures | 0 remaining; Section-4 authoring package proof reports green |
| Strict OpenSpec validation | `reshape-developer-facing-interfaces` and `add-runtime-concurrency-limits` valid |
| Whitespace | `git diff --check` passed with only benign LF-to-CRLF notices |

The expected-red test command returns nonzero by design. Review every named failure and reject any
setup, discovery, restore, or assertion error that is not the frozen later-section product gap.

## Commands

```powershell
dotnet build OrcaCore.slnx --no-restore -v minimal
dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-restore --no-build -v minimal
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --filter "Disposition=Infrastructure" -v minimal
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj --no-restore --no-build --filter "Disposition=ExpectedRed" -v minimal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
git diff --check
```

## Independent review questions

1. Is every finding in both immutable Section-4 reviews resolved in source and executable
   regression coverage, without relying on stale package artifacts or dynamic metadata access?
2. Can any Section-4 scenario or already-satisfied contract remain in expected-red, or can any
   later scenario turn green through a whole-fixture/coarse-presence shortcut?
3. Does nested fan-out fail both static authoring and compiler defense, including manually
   constructed branch/item representations?
4. Is all mixed-mode fallback/inference code and every duplicate consumer actually gone?
5. Does replacement-host recovery preserve output, status, and fixed outcome, and does durable
   named-step execution prove host-DI construction?
6. Does the structural fingerprint exclude opaque code identity while retaining inspectable graph,
   strong-value, referenced-type, static-request, fixed-codec, and compiler-option structure?
7. Does the frozen manifest reproduce exactly before the reviewer adds one new immutable verdict?

Record provenance, the exact frozen manifest/checksum, every command/result, findings, and verdict
in one new immutable dated review file. Only an approval with no release blocker may authorize
task 5.0. Do not edit reviewed source, tests, tasks, specs, or existing review artifacts.
