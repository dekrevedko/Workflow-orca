# Harmonization task 5.2 independent review request

**Date:** 2026-08-20  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** create the coherent task 5.2 checkpoint commit

This is an implementation-owner request, not an approval. Task 5.2 is implemented and marked
complete in the working target, but the target remains uncommitted until an independent reviewer
approves the exact frozen bytes. Task 5.3 and every later harmonization task remain blocked during
this review. Reshape task 8.0 remains blocked by the unfinished harmonization exit gate regardless
of this review's verdict.

The exact `HEAD`, tree, raw-porcelain SHA-256, normalized-porcelain SHA-256, capability-directory
SHA-256, and content-record SHA-256 are supplied with the review handoff after this request and its
self-inclusive manifest are written. They are not embedded here, avoiding a self-reference cycle.

## Review target

The target is `HEAD` plus every entry in
`harmonize-downstream-capability-specs-task-5-2-dirty-manifest-2026-08-20.txt`.
Reproduce `git status --porcelain=v1 --untracked-files=all` before reading conclusions and again
after validation. Any status, path, byte-length, or content-hash difference invalidates the review.

The raw porcelain anchor hashes Git's emitted porcelain lines in their original order, joined with
LF and one final LF as UTF-8 without BOM. The normalized anchor reads
`git status --porcelain=v1 -z --untracked-files=all`, splits NUL records, removes one trailing CR,
sorts records by ordinal byte order, then joins them with LF and one final LF before hashing UTF-8
without BOM. Rename records are expanded by the NUL split and must not be collapsed.

The capability-directory anchor enumerates repository-relative
`openspec/changes/*/specs/*/` directory paths, uses `/` separators and one trailing `/`, sorts by
ordinal byte order, and joins with LF plus one final LF before SHA-256. It records both directory
count and the count lacking `spec.md`.

The content record sorts rendered records ordinally. Each record is
`<2-char status>\t<forward-slash path>\t<byte length>\t<lowercase file SHA-256>`, joined with LF
and one final LF and encoded as UTF-8 without BOM. This proves byte identity, not only path identity.

The checked-in dirty manifest deliberately preserves Git's emitted raw-porcelain order. A
path-sorted or status-grouped copy is set-only evidence and cannot claim the raw anchor.

No `src/**`, product public surface, package manifest, sample, provider schema, or runtime behavior
is changed by this target.

## Claims to re-derive

### Task 5.2 canonical synchronization

- Reshape tasks 7.16, 7.17, 7.17a, 7.17b, 7.17c, 7.17d, 7.18, 7.19, 7.20, 7.21,
  and 7.22 are complete. The immutable task 7.22 exit verdict is `APPROVE`, and checkpoint
  `923ab0633cf565debee790e235d679b17f4e450d` records that gate.
- Exactly eight formerly pending operations are consumed: `management-and-querying` (1),
  `quality-and-verification` (5), and `repository-foundation` (2).
- Every affected canonical requirement block is byte-equivalent to its authoritative reshape delta
  after the repository's requirement-block normalization. The three canonical preambles and all
  unrelated requirement blocks retain their prior bytes and order.
- The provenance record remains 176 rows: 173 synchronized canonical operations, zero pending
  canonical operations, and three declared new-capability requirements outside the canonical set.
- The record is 46,211 bytes with SHA-256
  `e1420f367a491e62e896f041f288b3235eeaf7647d721069cb71dda509b4021b`, has zero duplicate
  `(capability, requirement)` owners, and is semantic-approval eligible.
- The permanent historical-removal catalog remains exactly 11 entries with SHA-256
  `81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd`.
- Structural OpenSpec validation remains distinct from semantic approval. The fixture and dated
  artifact now report eligibility because both canonical synchronization tasks are complete, not
  merely because strict structural validation passes.

### Prior-review observation remediation

1. CI checkout detection is independent of the `actions/checkout` major version. A named or unnamed
   checkout step using any version is recognized and must carry `fetch-depth: 0`.
2. The task 4.3 artifact accurately states that the scheduler-sensitive wall-clock scan recursively
   covers non-generated C# sources under both behavior-scenario and provider-certification trees.
3. Design Decision 6 and task accounting distinguish a Git-emitted raw-anchor manifest from a
   path-sorted or status-grouped set-only manifest. This packet follows the raw-order rule.
4. The permanent historical-removal catalog, strict-subset self-test, generated-output exclusions,
   and completed-count pin approved with task 5.1 remain unchanged and green.

### Gate and scope accounting

- `harmonize-downstream-capability-specs` is 16/33 complete; task 5.2 is the last completed task and
  task 5.3 is next.
- Task 5.1's seven-capability, 42-operation synchronization remains unchanged.
- The task 4.2 and 4.3 artifacts and machine-readable fixtures agree with the canonical tree, task
  states, approval evidence, and zero-pending-operation state.
- Earlier dated independent-review requests, manifests, and verdicts remain immutable.
- No `src/**` entry exists in the frozen manifest.

## Owner-run validation

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
| Full guard composition | 211 passed / 14 failed / 225 total |
| Fresh-package negative probes | 132/132 isolated compiler diagnostics |
| Exact fresh-package API baselines | 14/14 passed |
| Package consumers | 8 green; only `dag-hosting` expected red |
| Strict OpenSpec | 18/18 |
| Harmonization accounting | 16/33 complete |
| Provenance record | 176 rows; 173 synchronized / 0 pending / 3 outside canonical |
| Whitespace | `git diff --check` exit 0; line-ending advisories only |

The full guard project is intentionally not all green: its 14 failures are separately classified
Section 8 executable scenarios. Approval requires the 211-test infrastructure lane to be green and
the expected-red lane to contain exactly those 14 named failures.

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

openspec validate --all --strict
git diff --check
```

Also independently:

- compare all eight synchronized canonical blocks to the authoritative reshape deltas;
- verify unchanged preambles, requirement order, and unrelated canonical blocks;
- rederive the 176-row provenance record, its 173/0/3 state accounting, and both fixture hashes;
- mutation-test checkout detection with `actions/checkout@v5` and no `fetch-depth`;
- confirm no `src/**` entry exists in the manifest; and
- reproduce all three Decision 6 anchors and the content anchor before and after validation.

Run provider/container suites sequentially if repeating the owner packet; shared infrastructure can
contend when run concurrently.

## Verdict instructions

Write one new dated immutable verdict under `docs/review/`. Record all three Decision 6 anchors plus
the content anchor before and after validation, every command/result, the independently rederived
canonical/provenance counts, each observation's disposition, and exactly `APPROVE` or `REJECT`.
Do not edit the reviewed target.

`APPROVE` authorizes only zero-drift verification and the coherent task 5.2 checkpoint commit. It
does not authorize task 5.3, the harmonization exit gate, archival, or reshape task 8.0.
