# Harmonization Task 5.3 independent review request

**Date:** 2026-08-22  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** create the coherent Task 5.3 checkpoint commit

This request freezes Task 5.3 on top of the fully activated Task 5.2 approval chain. The target is
`HEAD` plus every entry in
`harmonize-downstream-capability-specs-task-5-3-dirty-manifest-2026-08-22.txt`. The exact HEAD/tree,
raw and normalized porcelain hashes, capability inventory, and content-record hash are supplied with
the handoff after this request and its self-inclusive manifest are written; they are intentionally
not embedded here to avoid a content-hash self-reference.

An `APPROVE` verdict authorizes only the Task 5.3 checkpoint. It does not archive the harmonization
change, complete its later sections, or authorize reshape Task 8.0.

## Prior approval transition to verify

Task 5.2 was independently approved at `c996e3a08f55697e1814cf84a7581c9f05473142` /
tree `d2d5bcc496bf618dbae7a960aef20754b0c0c8cb`. Its three review observations were fixed before
Task 5.3 began:

1. **O-1:** Task 5.2 content discovery now catches a commit touching any owned canonical path on the
   current pre-approval lineage, without scanning unrelated refs or requiring all three paths in one
   commit.
2. **O-2:** every non-missing review state names an exact registered `stateEvidencePath` of the
   required disposition, and the exact independently reviewed commit/tree is recorded separately
   from the historical dirty manifest.
3. **O-3:** `refresh-review-manifest-current-matches.ps1` deterministically refreshes only maximal
   opportunistic byte pins; `-Check` is non-mutating, and forced/default writes are LF-only and
   byte-idempotent.

Checkpoint `80cc5065e02ddd6674a1e1633c191efc487c98ea` preserves the external Task 5.2 verdict in the
green `ApprovalAwaitingEvidenceCommit` state. Mechanical activation
`5e8e25b94010965130b3b98ede2392346da2a9cd` records that existing SHA and changes only the registry,
task note, and remediation artifact to `Approved`. Independently verify both commits and confirm no
immutable request or verdict was rewritten.

## Task 5.3 claims to re-derive

- Enumerate all four active changes and all 176 active delta requirement headings from their actual
  capability directories.
- Require exactly one active owner for
  `repository-foundation :: Dependency direction remains one-way` and
  `durable-runtime :: Durable resource governance is one serialized provider aggregate`.
- Require both owners to be `reshape-developer-facing-interfaces`.
- Compare the exact normative body after each heading against every other active delta requirement.
  A byte-identical copy under a renamed heading or another capability is still a competing owner;
  it must not be deduplicated as harmless redundancy.
- Confirm Task 5.3's completion artifact reproduces the two block/body SHA-256 records and that no
  canonical spec or product source changed.

## Mutation evidence to repeat

The implementation copied the complete friend-topology body under a renamed requirement in
`add-runtime-concurrency-limits/specs/runtime-resource-governance/spec.md`. The focused guard failed
and named:

`add-runtime-concurrency-limits :: runtime-resource-governance :: Temporary copied topology mutation`

After byte-exact restoration the focused gate returned green. Repeat this negative case, and also
change either target's `Change` owner or add a second same-heading owner; each must fail.

## Accounting and validation

The new infrastructure fact changes the maintained Section 7 recovery crosswalk from 1,382 to
1,383 physical declarations and from 694 to 695 active declarations. The
`OpenSpecCorpusGuards.cs` row changes from three to four declarations; no other inventory row moves.
The harmonization ledger is 18 complete / 16 open / 34 total.

Reproduce:

```powershell
dotnet build OrcaCore.slnx -c Release --no-incremental -m:1 -warnaserror
dotnet build OrcaCore.slnx -c Debug --no-incremental -m:1 -warnaserror

dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --filter "FullyQualifiedName~Task53_ReshapeSolelyOwnsFriendTopologyAndDurableGovernanceAggregate" -m:1
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --filter "Disposition=Infrastructure" -m:1
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --filter "Disposition=ExpectedRed" -m:1
dotnet test tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj -c Release --no-build -m:1

& .\tests\OrcaCore.DeveloperSurface.Guards\refresh-review-manifest-current-matches.ps1 -Check
openspec.cmd validate --all --strict
git diff --check
```

Expected: Debug and Release 0 warnings / 0 errors; focused Task 5.3 1/1; Infrastructure 215/215;
exactly 14 intentional `ExecutableBehaviorExpectedRedGuards` failures; ProviderCertification 96/96;
OpenSpec 18/18; crosswalk/accounting guards green; ledger 18/16/34; and no diff-check finding.

## Verdict instructions

Write one immutable verdict at:

`docs/review/harmonize-downstream-capability-specs-task-5-3-independent-review-verdict-2026-08-22.md`

Record the exact frozen anchors, the Task 5.2 two-step approval chain and O-1 through O-3 closure,
both Task 5.3 ownership identities, byte-identical-copy mutation result, crosswalk accounting, and
validation. End with exactly one terminal `APPROVE` or `REJECT` line. Do not edit any existing
request, verdict, manifest, reviewed commit, or canonical specification.
