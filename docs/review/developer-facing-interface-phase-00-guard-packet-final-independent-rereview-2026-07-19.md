# Phase 0 guard packet: final independent re-review

**Review date:** 2026-07-19  
**Verdict:** **APPROVE**  
**Gate disposition:** The complete Phase 0 guard packet is approved. Task 3.12 may be closed after
this verdict is recorded. Task 4.0 remains blocked until task 3.12 is actually closed and its stated
canonical-amendment/review preconditions are satisfied.

## Reviewed snapshot

- Baseline and `HEAD`: `8c2dd712284f3b638f9bf812ad2172f24d0a8863`.
- Baseline tree: `caaf9dec4e7ce4066525dfddeef06e9a82f3018f`.
- Review target: that baseline plus the exact 120 paths frozen in
  [`developer-facing-interface-phase-00-guard-packet-remediation-dirty-manifest-2026-07-19.txt`](developer-facing-interface-phase-00-guard-packet-remediation-dirty-manifest-2026-07-19.txt).
- Manifest SHA-256: `39B15216CE9BEC47EEDFDB07E512CC0BBE54C7A0A4202EBC109D232FE85A51C2`.
- Pre-review manifest comparison: 120 expected porcelain entries, 120 actual entries, zero missing,
  and zero extra. This new immutable verdict is intentionally outside that frozen target.
- Product-source scope: `git diff --name-only -- src` returned no path.
- OpenSpec task state during review: 29 done, 83 pending, 112 total. Tasks 3.12 and 4.0 remained
  unchecked throughout the review.

The current request is
[`developer-facing-interface-phase-00-guard-packet-remediation-rereview-request-2026-07-19.md`](developer-facing-interface-phase-00-guard-packet-remediation-rereview-request-2026-07-19.md).
This review also inspected the immutable first
[`REJECT`](developer-facing-interface-phase-00-guard-packet-independent-review-2026-07-19.md)
and remediation
[`REJECT`](developer-facing-interface-phase-00-guard-packet-remediation-independent-rereview-2026-07-19.md)
reviews rather than treating later status prose as self-approval.

## Findings

- **P0:** none.
- **P1:** none.
- **P2:** none.
- **P3:** none required for gate disposition.

## Remediation verification

### Full public surface, tiers, friends, and recursive leaks

The frozen contract drives eleven exact product assemblies and all nine audiences. Infrastructure
guards explicitly cover application, internal, engine, runtime-protocol, provider-authoring,
durable-hosting, DAG, DAG-hosting, and outward-companion classification
(`tests/OrcaCore.DeveloperSurface.Guards/InfrastructureGuards.cs:11-25`). Product guards require
one project per exact package row, exact PackageId/assembly/direct edges, the two approved friend
edges only, complete future assembly discovery, and recursive public-signature traversal
(`tests/OrcaCore.DeveloperSurface.Guards/NormativeContractGuards.cs:208-233,263-268`). A conforming
future graph can turn these guards green without changing their inventory or tier logic.

### Exact package-only authoring compile gate

The product-positive fixture compiles only `PositiveUsage.cs` against package `OrcaCore`; it does
not link the companion declarations. Warnings are errors, restore is restricted to the local Phase
0 feed, and the package cache is fixture-owned and cleaned. The infrastructure lane separately
compiles the exact companion and full positive usage, verifies 26 precise forbidden CS1061 sites,
and proves the same positive usage rejects a deliberately incomplete fixture package. This closes
the prior local-declaration and global-cache false-green paths
(`tests/OrcaCore.DeveloperSurface.Guards/AuthoringContractGuards.cs:87-105` and
`tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1:41-91`).

### Executable behavior certification

The behavior ledger contains exactly 95 unique scenarios and a matching scenario-specific
certification row for each. Those rows resolve through 72 frozen complete signatures that identify
assembly, declaring type, member, generic arity, parameter types, and return type. Each required
call must be expressed as exactly one direct method/property/constructor expression. The
guard-owned context resolves and invokes it, records its runtime call ID, and mints the only
observation accepted by `Phase0Assert`. Blocks, wrappers, uninvoked lambdas, stored/discarded
results, wrong real members, wrong overloads, and calls outside the observation context cannot mint
credit.

The context supports successful and throwing sync, `Task`, `Task<T>`, `ValueTask`, and
`ValueTask<T>` shapes. Required deterministic time/barrier credit is recorded only while an exact
observed call is active and a frozen v1 product-assembly frame is present, allowing legitimate
facade delegation without crediting unrelated driver activity. Infrastructure mutations reject
system time and no-op barriers, while positive mutations certify direct async, generated throwing,
and every exception shape. The empty approved scenario host therefore leaves all 95 implementation
drivers intentionally red; future owning implementation tasks can add those drivers without
changing certification code (`tests/OrcaCore.DeveloperSurface.Guards/ExecutableBehaviorContractGuards.cs:24-198`).

### DAG failure ownership

Failure ownership derives from the frozen ownership rows. Typed `DAG-*` failures resolve to
namespace and assembly `OrcaCore.Dag`; workflow failures and shared bases resolve to `OrcaCore`
(`tests/OrcaCore.DeveloperSurface.Guards/NormativeContractGuards.cs:272-299`). The earlier
application-only owner defect is removed.

## Reproduced commands and results

The guard project is an xUnit v3 executable, so discovery and execution were reproduced with its
direct runner rather than inferred from `dotnet test` build behavior.

| Command / lane | Exit | Reproduced result |
|---|---:|---|
| `dotnet run --project tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-restore -- -trait "Disposition=Infrastructure" -noColor` | 0 | xUnit v3.2.2; 43 total, 43 passed, 0 failed, 0 skipped. |
| Same executable with `Disposition=ExpectedRed` | 1, intentional | 28 total, 0 passed, 28 failed, 0 skipped. Ten behavior theories remain red because all 95 future drivers are absent; the other product gaps are the named exact implementation/package gaps. |
| `run-compile-fixtures.ps1 -Disposition Green` | 0 | Exact companion and positive usage compiled; 26 precise forbidden-member diagnostics verified; deliberately incomplete package rejected. |
| `run-compile-fixtures.ps1 -Disposition ExpectedRed` | 1, intentional | One named product red: the full positive authoring usage cannot compile because real `OrcaCore 0.0.0-phase0` is absent. |
| `run-package-fixtures.ps1 -Disposition ExpectedRed` | 1, intentional | Eight named reds: primary-package, minimal-ephemeral, PostgreSQL durable, callback ingress, in-memory durable, DAG hosting, provider/custom host, and Kubernetes companion lack exact packages. |
| `dotnet build OrcaCore.slnx -c Release --no-restore --nologo -v minimal` | 0 | Build succeeded with 0 warnings and 0 errors. |
| `openspec.cmd validate reshape-developer-facing-interfaces --strict` | 0 | Valid. |
| `openspec.cmd validate add-runtime-concurrency-limits --strict` | 0 | Valid. |
| `git diff --check` | 0 | No whitespace error; only line-ending conversion warnings. |
| `git diff --name-only -- src` | 0 | No output. |
| Manifest/status comparison via `git status --porcelain=v1 -uall` | 0 | 120 expected, 120 actual, zero differences. |
| Relative-link scan over changed Markdown paths in the frozen manifest | 0 | 11 Markdown files checked; zero missing relative targets. |

Every expected-red process failed for its recorded future product/package/driver gap, not because of
restore setup, compiler infrastructure, adapter discovery, or an unrelated assertion failure.

## Final gate decision

Tasks 3.1 through 3.11d now form an authorable and executable Phase 0 packet that a conforming
implementation can turn green without rewriting the guards. The full target graph, package-only
consumer shape, exact authoring surface, scenario-specific behavioral contracts, deterministic
race provenance, provider/friend seams, and distinct DAG ownership are all represented by concrete
gates. The earlier P1 findings are fully remediated.

**APPROVE Phase 0 guard-packet exit.** This verdict authorizes closing task 3.12. It does not itself
edit the task ledger, does not start task 4.0, and does not authorize bypassing task 4.0's remaining
explicit prerequisites.
