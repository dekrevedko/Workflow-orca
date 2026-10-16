# Phase 0 developer-surface guard packet: independent review

**Date:** 2026-07-19

**Verdict:** **REJECT**

**Release decision:** Phase 0 may not exit. Task 3.12 remains open. Task 4.0 and all product
source work remain blocked pending remediation and a new independent review.

This is an immutable review of the snapshot identified below. Later remediation or re-review
must be recorded in a new dated artifact rather than rewriting this verdict.

## Reviewed snapshot

- Baseline and `HEAD`: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- Review target: that commit plus the uncommitted paths frozen in
  [`developer-facing-interface-phase-00-guard-packet-dirty-manifest-2026-07-19.txt`](developer-facing-interface-phase-00-guard-packet-dirty-manifest-2026-07-19.txt).
- Manifest SHA-256: `68A4886A290F8B1512FC06BC51D688E7E5349F8FB7844DEE90C6E1E518FDC86C`.
- Pre-review manifest comparison: 98 expected entries, 98 current porcelain entries, 0
  differences. The new review artifact itself was created after that comparison and is not part
  of the frozen implementation target.
- Product-source scope: `git diff --name-only -- src` returned no paths.
- OpenSpec state before verdict: 29 done, 83 pending, 112 total; 3.12 and 4.0 were both open.

## Release-blocking findings

### P1-1 - The public-surface catalog still models the legacy assembly graph and only four tiers

[`PublicSurfaceCatalog.cs`](../../tests/OrcaCore.DeveloperSurface.Guards/PublicSurfaceCatalog.cs)
defines only `Application`, `ProviderAuthoring`, `RuntimeProtocol`, and `Internal` at lines 5-10,
then loads the legacy assembly list at lines 17-30. It does not model or load the required
`OrcaCore`, `OrcaCore.Dag`, `OrcaCore.Dag.Hosting`, `OrcaCore.Durable.Hosting`,
`OrcaCore.Runtime.Protocol`, or `OrcaCore.Provider.Abstractions` target assemblies, and it has no
distinct DAG, DAG-hosting, or companion classification. The guard project likewise references
the legacy project set.

Consequently, the green classification result proves only that every currently loaded legacy
type fits one of four legacy buckets. The friend-metadata and recursive-leak product guards also
inspect that same incomplete assembly set. They cannot certify the exact task-3.1 tier/assembly
contract or turn green against the approved package graph without changing the guard packet.

Required remediation: make the target assembly inventory and all required audience tiers derive
from the frozen v1 contract, ensure the future exact assemblies are inspected, and add explicit
coverage proving DAG, DAG-hosting, companion, provider, protocol, application, and internal
classification plus cross-tier rejection.

### P1-2 - The exact-authoring compile lane does not compile or exercise the exact authoring surface

[`CompileFixtures/ExactAuthoring/Authoring.cs`](../../tests/OrcaCore.DeveloperSurface.Guards/CompileFixtures/ExactAuthoring/Authoring.cs)
contains only `typeof(...)` references to type families. It invokes none of the companion's
factory, init, root, nested, branch, item, leased, scope, join, completion, definition, or
reference members. In
[`run-compile-fixtures.ps1`](../../tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1),
the `Green` branch at lines 36-39 exits after string-inventory checks and never runs
`dotnet build`; the expected-red branch accepts a failure mentioning only
`EphemeralWorkflowInitBuilder` and `DurableLeaseItemBuilder` at lines 41-47.

The runtime authoring red at
[`AuthoringContractGuards.cs`](../../tests/OrcaCore.DeveloperSurface.Guards/AuthoringContractGuards.cs)
lines 110-121 checks only six root/completion exported type names. The companion hash freezes the
planning file, not the implemented CLR surface. A product with incorrect overloads, nullability,
return builders, generic arities outside those six checks, or forbidden nested/leased members can
therefore satisfy the product-red gate.

Required remediation: add compile fixtures that actually author against every exact companion
signature and negative fixtures for every forbidden mode/location/root-only/leased member. The
green harness must execute its claimed compile validation, and the product gate must not turn
green until all exact positive and negative cases are satisfied.

### P1-3 - Detailed behavior ledgers are prose, while product gates are token/type-presence probes

The JSON scenario files are deserialized only to check IDs, counts, task IDs, uniqueness, and
nonempty text. No guard executes their setup/assertion schedules. Representative examples are
[`DeadlineRetryContractGuards.cs`](../../tests/OrcaCore.DeveloperSurface.Guards/DeadlineRetryContractGuards.cs)
lines 9-19 and
[`LeaseDiscoveryAndGovernanceContractGuards.cs`](../../tests/OrcaCore.DeveloperSurface.Guards/LeaseDiscoveryAndGovernanceContractGuards.cs)
lines 9-20. Their product-red counterparts can turn green from a few source strings or exported
type names: deadline/retry checks one diagnostic string and `StepOperationId` at lines 39-46;
lease authoring/exit checks `SFE-RUN-002`, `AmbiguousHeld`, and `ResourceLeaseRequest` in
[`LeaseAuthoringAndExitContractGuards.cs`](../../tests/OrcaCore.DeveloperSurface.Guards/LeaseAuthoringAndExitContractGuards.cs)
lines 41-49; discovery/governance checks three type names and four source strings at lines 43-61.

The same pattern affects state/codec, structured fan-out, facade/hosting, application, and DAG
behavior. Package programs establish useful compile-time consumer shapes, but the package runner
builds rather than runs them; it does not execute deduplication, split-host, replay, deadline,
retry, DAG, lease, cancellation, confirmation, or accounting schedules.

These checks can go green before the semantic contracts in tasks 3.5 through 3.11d are
implemented. That violates the requirement for explicit regression guards for each actionable
contract and prevents the fixtures from serving as implementation exit gates.

Required remediation: turn each semantic scenario into an executable deterministic guard (or a
fixture consumed by such a guard), and make the corresponding product lane remain red until the
scenario's behavioral assertion passes. Keep package compilation as a separate consumer-shape
lane.

### P1-4 - The failure-ownership guard contradicts the approved DAG ownership

The frozen contract and matrix assign DAG authoring, handles, results, and errors to namespace
and assembly `OrcaCore.Dag`; see
[`v1-public-contract.json`](../../tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json)
lines 99-107 and document 17's ownership row. However,
`ProductBuiltInFailures_HaveOneCanonicalApplicationDeclaration` in
[`NormativeContractGuards.cs`](../../tests/OrcaCore.DeveloperSurface.Guards/NormativeContractGuards.cs)
lines 264-283 requires every named failure owner, including `DagRunNotFoundException` and the
other DAG exceptions, to live in namespace and assembly `OrcaCore`.

This is not an approved missing-product red: a conforming DAG implementation would remain red.

Required remediation: assert workflow failure owners against `OrcaCore` and DAG failure owners
against `OrcaCore.Dag`, preferably by using the frozen ownership catalog rather than one global
hard-coded owner.

## Required review questions

1. **Semantic ledger coverage: yes.** The 14 unique entries map exactly to tasks 3.1-3.10 and
   3.11a-3.11d. Manual review found no active paused-routing, public-job, lease-expiry, query-
   statistics, or other superseded scenario. References to the outward Kubernetes Job journey
   and explicit absence checks are within the approved contract.
2. **Green guard completeness: no.** Matrix-row dependency checks, catalog integrity, companion
   hashing, authored-location grammar, and provider fixture structure are useful. Complete tier
   classification, future compiled friend inspection, exact product authoring, and compile-time
   root-only/leased absence are not yet proved for the approved target.
3. **Fixture concreteness: no.** The provider-author compile fixture is concrete, and application
   and DAG package sources provide useful compile-time journeys. Application behavior,
   deadline/retry, and all lease/governance schedules remain non-executable descriptions, so
   they can turn nominally green without proving the protocol.
4. **All 18 product failures intentional/actionable/mapped: no.** The observed 18 failures are
   reproducible and broadly correspond to future work, but P1-4 encodes a wrong target, and the
   presence-only failures do not gate the detailed later-task semantics claimed by their ledger
   entries.
5. **Task 4.0 blocked everywhere: yes.** The live task file, quality delta, active index,
   implementation routing, and current status all retain the block. This rejection preserves it.

## Reproduced evidence

| Command/lane | Exit | Result |
|---|---:|---|
| `dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore --filter "Disposition=Infrastructure" --logger "console;verbosity=minimal"` | 0 | 36 passed, 0 failed, 0 skipped |
| Same project, `Disposition=ExpectedRed` | 1, expected | 0 passed, 18 failed, 0 skipped; all 18 named failures inspected |
| `run-compile-fixtures.ps1 -Disposition Green` | 0 | Reported 25 family names; P1-2 explains why this is not an actual compile proof |
| `run-compile-fixtures.ps1 -Disposition ExpectedRed` | 1, expected | 1 named product red: exact staged authoring families not implemented |
| `run-package-fixtures.ps1 -Disposition ExpectedRed` | 1, expected | 8 named product reds: primary, ephemeral, PostgreSQL, callback, in-memory, DAG, provider-author, and Kubernetes companion packages absent from the local feed |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | 0 | Valid |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | 0 | Valid |
| `git diff --check` | 0 | No whitespace error; line-ending conversion warnings only |
| `git diff --name-only -- src` | 0 | No output |

The expected-red exit codes above are intentional process results and were assessed by their
named failures. They do not override the release-blocking design defects in this review.

## Gate disposition

- Do not close task 3.12.
- Do not check or start task 4.0.
- Remediate P1-1 through P1-4 without changing product source, refresh the frozen manifest and
  all evidence, then request a new independent review.
