# Developer-Facing Interface Section 7 Rejection-Remediation Exit Independent Verdict

Date: 2026-07-31

Verdict: **APPROVE**

## Scope and frozen target

I independently reviewed the live rejection-remediation request, its frozen manifest, the prior
immutable rejection, the test-recovery record, the inactive-project audit, the reviewed source and
tests, and the active OpenSpec/task state. I did not edit any reviewed path and did not begin Section
8.

Before reading the review claims, and again after completing validation, I reproduced the frozen
target from `git status --porcelain=v1 -uall`. Both checks produced the exact same 705 ordered lines
as the self-inclusive manifest: 404 modified, 52 deleted, and 249 untracked paths. The recorded
hashes independently match:

- raw manifest SHA-256: `E42D9C8C686C64ED9372DD837B89D0EC412184C29F810726F3B6FF4260842C99`
- ordinal-sorted, LF-normalized status SHA-256: `3856C150A07D3C5FD07167671E5552C8E2D66B0EE3B8DEA9D3E241DE74760BE0`

The reviewed provenance also matches: HEAD
`d76192f089dd07f68e310c21fe4e5a38dd93cf7f`, tree
`2264e670493ecc76359d42ee5273028eb287a566`, with baseline `8c2dd712` an ancestor. The separate
recovery worktree is clean at that same HEAD and tree.

## Independent derivation

The three P1 rejection blockers are closed.

1. Durable start idempotency now commits the definition identity/version, structural definition
   fingerprint, and fixed-codec input fingerprint with the start mapping. Both provider roles read
   the complete persisted binding. A replacement registry therefore accepts an identical replay
   and returns a typed conflict, without an incompatible handle, for changed input or a changed
   structural definition at the same identity/version. Provider certification also proves that a
   duplicate key rolls back the losing start append atomically.
2. Inbox identity is now `(InstanceId, EventId)`, and the normalized envelope fingerprint is
   persisted with that target-specific record. The facade consults persisted inbox state before
   terminal/no-active-wait classification. Replacement-runtime regressions prove identical replay,
   changed-envelope conflict, terminal replay precedence, and independence of the same event ID on
   different targets. In-memory certification and PostgreSQL certification exercise the composite
   key and persisted fingerprint.
3. Durable cancellation now commits `CancellationRequested`, exposes it while a step is in flight,
   requests cooperative cancellation through the active step scope, classifies repeated requests
   as `AlreadyRequested`, and finalizes persisted requests on a replacement host. Cancellation and
   termination dispositions are derived inside the serialized command attempt and retried after
   provider conflicts, so concurrent termination has exactly one winner and one
   `AlreadyTerminal`. The corresponding durable and ephemeral race regressions pass.

The behavior-contract remediation is also substantive. Driver discovery is restricted to the two
approved hosts and declared public static methods. Certification matches the exact declaring
assembly, declaring type, member, generic arity, parameter list, and return type; the guard invokes
the observed expression itself, requires every minted observation to be asserted, verifies
deterministic-seam consumption from a product frame above the guard boundary, and includes mutation
controls for uninvoked/stored expressions, wrong members/overloads, and throwing arguments. The
95 frozen scenarios remain partitioned into 81 completed Section 4-7 scenarios and 14 later-section
expected reds; no completed scenario was substituted or relaxed.

Task accounting independently parses as 106 complete / 30 pending / 136 total with zero duplicate
IDs for `reshape-developer-facing-interfaces`, and 16 / 0 / 16 with zero duplicates for
`add-runtime-concurrency-limits`. Task `8.0` and every Section 8 task remain unchecked.

## Test-recovery and inactive-project judgment

The retired 707 legacy white-box cases are **not** counted as passing tests. The recovery record is
acceptable because it accounts for the current 98 excluded files and 549 declarations as replaced,
deferred to Section 8+, or legacy/non-v1, with no unexplained blocker bucket. The replacement
evidence is attached to active public-surface, product, provider-certification, and application
journey tests rather than to the retired count.

I also verified the project boundaries directly. PostgreSQL certification and integration are
active solution projects. The integration project has five current public-application journeys and
keeps its provisional sources physically present under explicit compile exclusions. RabbitMQ,
Redis, SQL Server, and ZeroMQ test sources are likewise retained and explicitly classified rather
than presented as active passes; SQL Server remains a later open task. This is an auditable recovery
and deferral decision, not an inflation of the Section 7 pass count.

## Validation reproduced

I ran the review request's commands against the frozen target:

- `dotnet build OrcaCore.slnx --configuration Release --no-incremental --nologo --verbosity minimal`:
  succeeded with 0 warnings and 0 errors.
- Active product/provider/integration suites: Core 413/413, Ephemeral 64/64, Durable 72/72,
  Hosting 1/1, Acceptance 42/42, in-memory provider certification 80/80, PostgreSQL 79/79, and
  Integration 5/5. Total: 756 passed, 0 failed, 0 skipped.
- Section 7 executable drivers: 37/37 passed.
- `Disposition=Infrastructure`: 162/162 passed.
- `Disposition=ExpectedRed`: exactly 14 failed, 0 passed, 0 skipped. These are intentional Section
  8/9 failures and are not counted as passing tests.
- Green compile fixtures: succeeded, including exact/product-positive use, 26 source and 26 packed
  forbidden-member diagnostics, and incomplete-package rejection. Compile ExpectedRed count: 0.
- Green package fixtures: 6/6. Package ExpectedRed: exactly the named `dag-hosting` and
  `kubernetes-companion` consumers failed.
- Strict OpenSpec: both active changes valid; `--all --strict` reported 17 passed and 0 failed.
- `dotnet list OrcaCore.slnx package --vulnerable --include-transitive`: no vulnerable packages in
  all 24 active solution projects.
- `git diff --check`: exit 0; line-ending notices only.

## Disposition

No actionable finding remains at the Section 7 exit gate. Section 7 is independently approved.

Per the approved checkpoint rule, the implementation owner's next mandatory action is one coherent
Section 7 checkpoint commit. Section 8 may begin only after that checkpoint exists, in a later
implementation turn. This review created no commit.
