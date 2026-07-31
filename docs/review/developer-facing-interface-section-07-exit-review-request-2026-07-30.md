# Section 7 independent exit review request

**Date:** 2026-07-30  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** Section 7 exit and the later start of task `8.0`

This is an implementation-owner request, not an approval. It preserves every earlier request,
manifest, and verdict as immutable evidence. Section 6 was approved by
`developer-facing-interface-section-06-ownership-ddl-scanner-remediation-independent-rereview-verdict-2026-07-30.md`.
Section 7 is implemented against that approved target. Task `8.0` remains open and blocked until an
independent reviewer approves this exact frozen target without a release blocker.

## 1. Frozen target provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Review target | `HEAD` plus every entry in the self-inclusive Section 7 manifest |
| Expanded porcelain entries | 678 |
| Entry classes | 396 modified / 56 deleted / 226 untracked |
| Raw-manifest SHA-256 | `D74115178F29FD03ADC20178E02B802688346954BD79C207F801CF8924B7D17A` |
| LF-normalized sorted-status SHA-256 | `8A8581080C11D2DF3FE13755113DD184791C3D3B4C045CB0171D84045E45F378` |
| Reshape tasks | 102 complete / 30 pending / 132 total; 0 duplicate IDs |
| Runtime-governance tasks | 16 complete / 0 pending / 16 total; 0 duplicate IDs |

The authoritative self-inclusive manifest is
`developer-facing-interface-section-07-exit-review-dirty-manifest-2026-07-30.txt`. It uses
`git status --porcelain=v1 -uall`, so every untracked path is explicit rather than collapsed to a
directory. Reproduce it before reading conclusions and again after validation. Any ordered
path/status difference invalidates the review.

## 2. Section 7 claims to re-derive

### 2.1 Exact package and architecture contract

1. The local feed contains exactly the 11 first-release package identities and each package owns
   one matching assembly/project.
2. `OrcaCore` is the primary application contract and authoring package, not a meta-package.
3. Direct dependencies, application/advanced/runtime/provider tiers, and the two friend edges match
   the frozen contract with no test friend or reverse dependency.
4. Public application signatures do not leak compiled IR, protocol, provider, engine, or companion
   integration types.
5. The retired catch-all hosting and provisional package identities do not remain as compatibility
   shims.

### 2.2 Application facade, events, handles, and projections

1. `IWorkflowDefinitionRegistry` exposes the four typed handle families and closed registration and
   start results, deterministic compatibility failures, structural conflicts, and fixed-codec
   idempotency binding.
2. `IWorkflowEventClient` exposes exactly the instance/correlation route names and payloadless/typed
   overloads, with deduplication, conflict, terminal/no-wait, ambiguous-pair, and atomic
   continuation behavior owned by each engine.
3. Only the two instance-handle families expose snapshot/state/nonpolling output wait,
   cancellation-request, and termination operations.
4. Committed projections are detached, typed, and opaque to compiled plans and private
   branch/item/fiber/scope state.
5. The management surface is reduced to the approved pool list/get/resize and trusted recovery and
   diagnostics contracts; it exposes no force-release or broad instance-query compatibility API.

### 2.3 Split hosting, providers, and durable governance

1. The six exact split-owner registration methods validate copied options and reject missing,
   partial, duplicate, and mixed role ownership.
2. The in-memory and PostgreSQL packages provide complete role sets; PostgreSQL is not composed
   from partial fallback ports.
3. One serialized resource-governance aggregate per partition enforces expected-version whole-batch
   append, definition agreement, FIFO multi-pool atomicity, cancellation/crash barriers,
   stale-command idempotence, ownership equality, capacity conservation/restoration, resize debt,
   confirmation/tombstones, quarantine, and contention.
4. Provider certification is reusable through the exact provider-abstractions package and host
   replacement uses a genuinely new service provider.
5. The approved greenfield rule still holds: no compatibility migration or provisional durable
   schema is retained.

### 2.4 Codec, telemetry, and deferred boundary

1. `orcacore-json-v1` permits exactly the sanctioned sequence and map collection representations
   with the specified deterministic enumeration semantics and rejects every other declared/runtime
   collection shape before commit.
2. Product packages emit only BCL diagnostics; SDK/exporter registration remains host-owned and
   provider-native packages remain isolated.
3. Quarantined units, oldest quarantined-obligation age, and physically running fenced bodies are
   covered by operational instruments and dashboard documentation.
4. Exactly 37 current-physical Section 7 behavior scenarios execute. The 14 ExpectedRed scenarios
   and two package fixtures belong to Section 8 and are not credited as passing tests.
5. Task `8.0` and every Section 8 task remain unchecked.

## 3. Explicit coverage reduction requiring independent judgment

Read
`developer-facing-interface-section-07-provisional-test-retirement-2026-07-30.md`.
The exact friend/package contract retires 128 old white-box files containing 707 prior xUnit cases:
144 Core, 171 Ephemeral, 295 Durable, 65 Acceptance, 16 Hosting, and 16 PostgreSQL. They targeted
deleted provisional internals, compiled IR, saga/DAG, catch-all hosting, or superseded
management/projection APIs.

These 707 cases are retired, not passing. The surviving suite counts are therefore much lower.
Independently decide whether the 158 infrastructure guards, 37 Section 7 behavior drivers, exact
package/compile lanes, 78 provider-certification cases, and 39 live PostgreSQL cases provide
sufficient replacement evidence. Treat unjustified exclusions, lost supported behavior, or a
test-only weakening of the contract as release blockers.

Also review the application-authoring proxy exception certification. The harness accepts an
`OrcaCore.Core` kernel frame for an exact `OrcaCore` facade expression because
`ExceptionDispatchInfo` can preserve the kernel stack while an inlined facade/proxy frame is
absent. Verify the expression validator still prevents unrelated or wrapper calls from minting an
observation.

## 4. Owner-run evidence

| Lane | Result |
|---|---:|
| Clean Release solution build | succeeded; 0 warnings / 0 errors |
| Core | 267 passed / 0 failed / 0 skipped |
| Ephemeral | 2 passed / 0 failed / 0 skipped |
| Durable | 35 passed / 0 failed / 0 skipped |
| Hosting | 1 passed / 0 failed / 0 skipped |
| Acceptance | 5 passed / 0 failed / 0 skipped |
| Provider certification | 78 passed / 0 failed / 0 skipped |
| PostgreSQL provider, Docker-backed | 39 passed / 0 failed / 0 skipped |
| Section 7 behavior drivers | 37 passed / 0 failed |
| Infrastructure guards | 158 passed / 0 failed on three consecutive isolated runs |
| Expected-red guards | 0 passed / 14 intentional named failures / 0 skipped |
| Green package fixtures | 6 built from the exact 11-package local feed |
| Package ExpectedRed | exactly `dag-hosting` and `kubernetes-companion` failed as designed |
| Green compile fixtures | exact and product consumers compiled; 26 + 26 forbidden CS1061 calls rejected; incomplete control rejected |
| Expected-red compile fixtures | 0 gaps |
| Exact local packages | 11 packed at `0.0.0-phase0` |
| Strict OpenSpec | both active changes valid; 17/17 all items valid |
| Task accounting | reshape 102/30/132; governance 16/0/16; no duplicate IDs |
| NuGet vulnerability audit | no vulnerable packages in all 23 active solution projects |
| Whitespace | `git diff --check` exit 0; line-ending notices only |

The 14 intentional scenario failures are:

`scheduler-post-wait-lease-validation`, `friend-and-child-opacity`,
`missing-duplicate-mapinput`, `ordered-snapshot-output-wait-cancel`,
`ephemeral-cast-free-output`, `typed-registration-start-reopen`, `fixed-codec-input-once`,
`terminal-report-inside-lease`, `durable-wait-output`, `parked-node-admission`,
`valid-independent-node`, `opaque-direct-output-validation`,
`ambiguous-create-or-observe`, and `stable-child-reattachment`.

## 5. Minimum independent commands

Run from `X:\Projects\GitHub\Workflow-orca`:

```powershell
git status --porcelain=v1 -uall
git rev-parse HEAD
git rev-parse 'HEAD^{tree}'
git merge-base --is-ancestor 8c2dd712284f3b638f9bf812ad2172f24d0a8863 HEAD

dotnet build OrcaCore.slnx --configuration Release --no-incremental --nologo --verbosity minimal

dotnet test tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Acceptance.Tests/OrcaCore.Acceptance.Tests.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj -c Release --no-build --no-restore
dotnet test tests/OrcaCore.Providers.PostgreSql.Tests/OrcaCore.Providers.PostgreSql.Tests.csproj -c Release --no-build --no-restore

dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~ExecutableBehaviorInfrastructureGuards.Section7Scenario"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=ExpectedRed"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition Green -CompletedSection 7
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition ExpectedRed -CompletedSection 7

openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
openspec.cmd validate --all --strict
dotnet list OrcaCore.slnx package --vulnerable --include-transitive
git diff --check
```

Run infrastructure three consecutive times. Run the Docker-backed suite separately from package
and compile fixture builds. ExpectedRed guards and the package-red command must return nonzero with
exactly the named intentional failures.

## 6. Verdict instructions

Write exactly one new dated immutable verdict under `docs/review/`. Record provenance, exact
manifest comparison before and after, every command/result, independent source and test derivation,
the explicit 707-test retirement judgment, and `APPROVE` or `REJECT`.

Do not edit reviewed source, tests, tasks, specs, plans, documentation, manifests, requests, or
existing review artifacts. Approval authorizes the implementation owner to begin task `8.0` in a
later turn; it does not mark task `8.0` complete. Rejection keeps Section 8 blocked.
