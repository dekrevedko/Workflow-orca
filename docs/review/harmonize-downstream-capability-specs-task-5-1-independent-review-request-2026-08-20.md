# Harmonization task 5.1 independent review request

**Date:** 2026-08-20  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** create the coherent task 5.1 checkpoint commit

This is an implementation-owner request, not an approval. Task 5.1 is implemented and marked
complete in the working target, but the target remains uncommitted until an independent reviewer
approves the exact frozen bytes. Task 5.2 and every later harmonization task remain blocked during
this review. Reshape task 8.0 remains blocked by the unfinished harmonization exit gate regardless
of this review's verdict.

The exact `HEAD`, tree, raw-porcelain SHA-256, normalized-porcelain SHA-256, capability-directory
SHA-256, and content-record SHA-256 are supplied with the review handoff after this file and the
self-inclusive manifest are written. They are not embedded here so the content-record hash can
cover this request without a self-reference cycle.

## Review target

The target is `HEAD` plus every entry in
`harmonize-downstream-capability-specs-task-5-1-dirty-manifest-2026-08-20.txt`.
Reproduce `git status --porcelain=v1 --untracked-files=all` before reading conclusions and again
after validation. Any status, path, byte-length, or content-hash difference invalidates the review.

The raw porcelain anchor is SHA-256 over the porcelain lines in Git's emitted order, joined with LF
and a final LF, encoded as UTF-8 without BOM. The normalized anchor uses the exact design Decision 6
pipeline: read `git status --porcelain=v1 -z --untracked-files=all`, split NUL records, remove one
trailing CR from each record, sort records by ordinal byte order, then join with LF and one final LF
before hashing UTF-8 without BOM. Rename records are expanded by the NUL split and must not be
collapsed.

The capability-directory anchor enumerates repository-relative
`openspec/changes/*/specs/*/` directory paths, uses `/` separators and one trailing `/`, sorts by
ordinal byte order, and joins with LF plus one final LF before SHA-256. It records both the directory
count and the count lacking `spec.md`.

The additional content record sorts rendered records ordinally; each record is
`<2-char status>\t<forward-slash path>\t<byte length>\t<lowercase file SHA-256>`, again joined with
LF and a final LF and encoded as UTF-8 without BOM. Its anchor proves byte identity of every target
file rather than manifest identity alone.

This target contains two bounded parts:

1. remediation of eight observations from the independently reviewed task 4.3/task 5.1 chain; and
2. harmonization task 5.1's synchronization of 42 already-approved Section 7B canonical
   requirement operations across seven capabilities.

No `src/**`, product public surface, package manifest, sample, provider schema, or runtime behavior
is changed by this target.

## Claims to re-derive

### Review-observation remediation

1. Active synchronized removals and the permanent historical-removal catalog use distinct wrapper
   types. Reversing their arguments is a compile-time error rather than a semantically valid call.
2. Every CI checkout retains full Git history with `fetch-depth: 0`, so available historical source
   commits can be independently corroborated.
3. Permanent-catalog cardinality is pinned and checked before its digest. Removing an entry produces
   a precise shrink diagnostic rather than only a generic hash mismatch.
4. The scheduler-sensitive wall-clock scan recursively covers every behavior-scenario and
   provider-certification source. The former provider confirmation/tombstone gate now waits on
   workflow-owned signals, and no matching `WaitAsync(TimeSpan...)` gate remains in those trees.
5. The subset helper itself is exercised with a strict synthetic active-versus-archived catalog, so
   reversing its body fails even while the live active set equals the permanent catalog.
6. Checkout discovery treats `uses: actions/checkout@v4` anywhere in a YAML step body as authoritative,
   including a named checkout step, and requires `fetch-depth: 0` on every such step.
7. Recursive wall-clock scanning excludes generated `bin` and `obj` trees, so build state cannot
   create false infrastructure reds.
8. A completed canonical-reconciliation count must appear after the task's `**Completed:**` marker;
   task 5.1's earlier `(7)` scope declaration cannot satisfy the resulting `(0)` assertion.

Each observation has executable regression coverage in the infrastructure lane. Confirm that the
remediation does not alter product behavior or weaken the permanent removal history.

### Task 5.1 canonical synchronization

- The immutable reshape task 7.23 verdict is `APPROVE`, and the owning reshape deltas remain the
  authority for the synchronized Section 7B contract.
- Exactly 42 formerly pending operations are consumed:
  `developer-facing-surface` (7), `durable-persistence-and-outbox` (5), `durable-runtime` (8),
  `event-routing-and-waits` (8), `state-driven-runtime` (2), `workflow-authoring` (3), and
  `workflow-contracts` (9).
- Every affected canonical requirement block is byte-equivalent to its authoritative reshape delta
  after the repository's requirement-block normalization. Canonical preambles and unrelated
  requirement blocks remain unchanged.
- The authored-`Yield` canonical requirement is removed with its exact historical block preserved
  in the permanent catalog. The catalog now contains exactly 11 entries.
- The provenance record remains 176 rows: 165 synchronized operations, 8 pending operations owned
  by task 5.2, and 3 declared new-capability requirements outside the canonical set.
- The remaining task 5.2 operations are exactly `management-and-querying` (1),
  `quality-and-verification` (5), and `repository-foundation` (2). This review must not pre-close,
  synchronize, or approve those operations.
- Structural OpenSpec validation remains distinct from semantic approval. The provenance fixture
  correctly reports semantic approval ineligible while those eight operations remain.
- No duplicate `(capability, requirement)` owner, undeclared delta directory, missing `spec.md`, or
  unexplained canonical capability is introduced.

### Gate and scope accounting

- `harmonize-downstream-capability-specs` is 15/33 complete; task 5.1 is the last completed task and
  task 5.2 is next.
- `reshape-developer-facing-interfaces` Section 7 task 7.22 has an immutable `APPROVE` verdict and
  checkpoint. This review neither reopens that target nor authorizes reshape task 8.0.
- The task 4.2 and 4.3 records and machine-readable fixtures agree with the current canonical tree,
  task states, approval evidence, and remaining-operation table.
- Earlier dated review records remain unchanged.

## Owner-run validation

The complete code-bearing target and follow-up observation remediation were validated before the
final freeze. This request and its manifest are documentation-only review inputs; neither changes an
executable input.

| Lane | Result |
|---|---:|
| Release solution build | 0 warnings / 0 errors |
| Debug solution build | 0 warnings / 0 errors |
| Core / Ephemeral / Durable | 350 / 79 / 98 passed |
| Acceptance / Hosting | 37 / 24 passed |
| Provider certification | 96 passed |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 passed |
| Infrastructure guards | 211/211 |
| Expected-red guards | exactly 14/14 intentional Section 8 failures |
| Strict OpenSpec | 18/18 |
| Harmonization accounting | 15/33 complete |
| Provenance record | 176 rows; 46,250 bytes; SHA-256 `078e916462cd47c3635f9c85d0ed5fc52cc62e055d45599e15a41da873be6438` |
| Permanent removal catalog | 11 entries; SHA-256 `81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd` |
| Whitespace | `git diff --check` exit 0; line-ending advisories only |

The full guard project is intentionally not all green: its 14 failures are the separately classified
Section 8 executable scenarios. Approval must require the 211-test infrastructure lane to be green
and the expected-red lane to contain exactly those 14 named failures; it must not describe the full
225-test project as green.

## Minimum independent checks

Run from the repository root:

```powershell
git status --porcelain=v1 --untracked-files=all
git rev-parse HEAD
git rev-parse 'HEAD^{tree}'

dotnet build OrcaCore.slnx -c Release --no-incremental -m:1 -warnaserror
dotnet build OrcaCore.slnx -c Debug --no-restore --no-incremental -m:1 -warnaserror

dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=Infrastructure"
dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj -c Release --no-build --no-restore --filter "Disposition=ExpectedRed"
dotnet test tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj -c Release --no-build --no-restore

openspec.cmd validate --all --strict
git diff --check
```

Also independently:

- compare all 42 synchronized canonical blocks to the authoritative reshape deltas;
- verify unchanged preambles and unrelated canonical blocks;
- rederive the 176-row provenance record, the 165/8/3 state accounting, and both fixture hashes;
- inspect the four observation regressions and prove their negative cases discriminate;
- confirm no `src/**` entry exists in the manifest; and
- reproduce all three Decision 6 freeze anchors and the additional content anchor before and after
  validation.

Run provider/container suites sequentially if repeating the complete owner packet; their shared
infrastructure can contend when run concurrently.

## Verdict instructions

Write one new dated immutable verdict under `docs/review/`. Record all three Decision 6 freeze
anchors plus the content anchor before and after validation, every command/result, the independently
rederived canonical/provenance counts, each observation's disposition, and exactly `APPROVE` or
`REJECT`. Do not edit the reviewed target.

`APPROVE` authorizes only zero-drift verification and the coherent task 5.1 checkpoint commit. It
does not authorize task 5.2 implementation, the harmonization exit gate, or reshape task 8.0.
