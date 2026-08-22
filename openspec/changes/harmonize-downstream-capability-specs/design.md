## Context

The repository has two normative trees (`docs/specs/` and `openspec/specs/`) plus active delta
changes. Earlier synchronization applied only the deltas named by one change and ran before later
approved amendments. That allowed canonical requirements to remain stale and allowed two active
changes to modify the same requirement headings without structural validation reporting a conflict.

`reshape-developer-facing-interfaces` is the authoritative product-contract change. It now owns the
complete event-routing/wait, durable-persistence/outbox, repository friend-graph, and durable
resource-governance deltas. This change retains only two unique capability deltas:
`event-driven-prototype` and `state-driven-runtime`. Its remaining work is cross-tree provenance,
numbered-requirement/acceptance coverage, documentation classification, and synchronization process.

The repository is greenfield: there is no released package or persisted-schema compatibility
contract to preserve. Historical plans and verdicts remain immutable evidence, but they do not
override current normative sources.

## Goals / Non-Goals

**Goals:**

- Give each canonical requirement heading one active change owner.
- Synchronize only independently approved deltas and prove their unchanged canonical baseline.
- Detect missing, duplicated, stale, and post-gate amendments across both normative trees.
- Preserve exact provenance for historical documents and review verdicts.
- Make freeze manifests reproducible across reviewers, including rename records.
- Add missing numbered requirements and acceptance criteria for already-approved authoring-session
  and failure-provenance semantics.

**Non-Goals:**

- Define or implement event routing, persistence/outbox, friend topology, resource governance,
  deletion remediation, observability, provider behavior, or other product runtime semantics owned
  by `reshape-developer-facing-interfaces`.
- Synchronize canonical specs before independent approval.
- Edit immutable historical reviews or archived record content.
- Add compatibility aliases, migration shims, or provisional-schema upgrade paths.
- Author product runtime source, behavior tests, samples, packages, or provider migrations.
  Documentation/OpenSpec corrections and narrowly scoped infrastructure guard tests listed in the
  task ledger remain in scope.

## Decisions

### 1. One active delta owner per capability requirement

`reshape-developer-facing-interfaces` exclusively owns `event-routing-and-waits`,
`durable-persistence-and-outbox`, `repository-foundation`, and `durable-runtime` for the current
Section 7 contract. This change contains no delta directory for those capabilities, even when an
earlier harmonize version held byte-identical text. Byte identity is not safe coordination: either
copy can later diverge and the second synchronization would silently overwrite the first.

This change owns only `event-driven-prototype` and the non-overlapping `Ephemeral mode has explicit
limitations` requirement in `state-driven-runtime`.

Alternative considered: retain equal duplicate deltas and require a fixed sync order. Rejected
because structural strict validation does not prove equality or prevent later divergence.

### 2. Canonical synchronization is approval- and provenance-driven

Synchronization occurs only after a new independent approval of the reduced change. It applies the
two approved delta files to their exact unchanged canonical requirement headings, records the
canonical diff, applies any approved `Purpose` wording manually, and strict-validates the result.

The gate also enumerates every canonical capability and every active delta heading. It rejects an
unexplained missing capability, an uncoordinated duplicate owner, or canonical content with no
approved source change. Structural validity remains necessary but is never reported as semantic or
provenance approval by itself. Canonical preambles are independently pinned per capability after LF
normalization, including each `## Purpose` block, so requirement-only synchronization cannot erase
or rewrite unowned preamble text without a reviewed fixture refreeze.

Canonical enumeration and active-delta ownership are separate inventories. A canonical capability
does not have to retain an active delta after its owning change is archived. Archival changes the
active provenance record and therefore requires a reviewed fixture refreeze, but no invariant may
make the archived state permanently invalid. Synchronized-removal provenance is a permanent catalog
that embeds the exact normalized historical canonical block and pins its hash. Currently active
synchronized removals must be a subset of that catalog, so archiving a change cannot require a C#
source-constant rewrite or erase already-reviewed history. The originating commit remains optional
corroborating metadata; when the object is available, validation reads it directly without requiring
ancestry from `HEAD`, and correctness does not depend on that object surviving integration-history
rewriting. CI executes the complete must-be-green infrastructure guard disposition while
intentional expected-red guards remain a separate lane.

A checkpoint is provenance-complete only when its exact target has immutable approval evidence.
The existence of a review request, green validation, a completed task checkbox, or a commit is not
approval. A dependent checkpoint remains blocked until its base checkpoint has either an
independent verdict or an explicit, dated owner-approval disclosure. The machine-readable guard
records `MissingApproval`, `Rejected`, and `Approved` states honestly, retains every immutable
verdict with an exact byte hash, and requires approved evidence to be preserved in a distinct
repository checkpoint before a dependent checkpoint may descend from it.

Alternative considered: rely on `openspec validate --all --strict`. Rejected because it validates
each change independently and permits contradictory or duplicated active deltas.

### 3. Late amendments re-enter every affected gate

An amendment approved after its original implementation-section gate closed must explicitly map to
canonical OpenSpec, numbered requirements, acceptance criteria, implementation tasks, executable
evidence, refreeze, and independent approval. A completed historical gate does not waive later
reconciliation.

The workflow records the affected capability/requirement IDs and the owning change. It does not
infer coverage from a broad task description or from the existence of a passing suite.

The machine-readable post-gate record carries eight mandatory stages: amendment approval,
canonical OpenSpec, numbered requirements, acceptance criteria, implementation tasks, executable
evidence, refreeze, and independent approval. Task references record their expected open/complete
state and resolve through exactly one active or dated archived change record. The record embeds the
exact requirement identities and validates them against that resolved change, so archival does not
erase the amendment contract or force a permanently active delta. Implementation task ranges carry
an exact count, and approval evidence is normalized before task-bound verdict validation. The known
Section 7B case records all 18 `developer-facing-surface` requirements. Task 5.1 has completed its
seven canonical operations, while the numbered requirement/acceptance corrections remain under
task 7.3. Closing this process task therefore cannot be mistaken for completing those downstream
edits.

### 4. Numbered requirements and acceptance criteria remain bidirectional

The authoring-session lifecycle and `WorkflowFailure` occurrence provenance already exist in the
approved reshape contract. Harmonization adds stable numbered requirements and acceptance criteria
that mirror those semantics without exposing implementation internals. Each new requirement maps to
an executable guard, and each acceptance criterion maps back to one normative requirement.

`docs/specs/17-public-authoring-contract.cs` changes only if the review concludes that these
semantics alter its compile-shaped public surface. Internal lifecycle state alone is insufficient
reason to add a public declaration.

### 5. Active documentation and immutable history have different rules

Active guides, architecture, implementation, and readiness documents describe only the current
contract. Deferred capabilities remain discoverable through the future-capability registry but are
not shown as usable APIs. Removed concepts retain no active alias or how-to path. Newly approved
Section 7B buffering, fanout, start-or-deliver, and durable publish are not misclassified as deferred.

Dated reviews and archived plans remain byte-immutable. Classification, supersession, and current
routing live in active indexes or new dated records. Moves of historical files preserve Git rename
history where possible; otherwise an immutable provenance record names the exact predecessor and
commit rather than rewriting the historical file.

### 6. Freeze hashes use one explicit byte pipeline

A review freeze records only paths that can enter the checkpoint commit. Its commit-real entry set
is the union of `git diff --name-only --no-renames HEAD` and
`git ls-files --others --exclude-standard`. The manifest starts from
`git status --porcelain=v1 --untracked-files=all --no-renames` and retains, in Git-emitted order,
only records whose paths belong to that entry set. This excludes index- or attribute-only status
entries whose worktree bytes equal `HEAD`; such entries cannot appear in the resulting commit and
therefore cannot be claimed by its frozen manifest.

The raw anchor is SHA-256 over that exact ordered commit-real manifest using the review packet's
stated line-ending convention. The normalized comparison anchor uses exactly:

```text
commit-real Git-order manifest
| read one status/path record per LF line
| remove one trailing CR from each record
| sort records by ordinal byte order
| join with LF and one final LF
| SHA-256
```

The freeze uses `--no-renames`, so a move is represented by the same delete/add path pair the commit
projection can verify. Reviewers record both the commit-real entry count and normalized-line count.
PowerShell culture sorting, anchoring unfiltered status output, path-only sorting, and omission of
the final LF are different algorithms and SHALL NOT be compared to this anchor.

A checked-in human-readable dirty manifest that claims to reproduce the raw anchor SHALL retain
the filtered Git-emitted order. A path-sorted, status-grouped, or unfiltered status manifest is
non-authoritative evidence even when its entry count and byte length happen to match another
projection; its digest SHALL NOT be presented as the commit-real raw anchor.

Every harmonization dirty manifest is registered in a machine-readable fixture as either
`RawGitOrder` or `SetOnlyPathSorted`. New `RawGitOrder` captures use the commit-real filter above.
The registry pins the manifest bytes and digest, the frozen raw digest, and the counterfactual
path-sorted digest. A mutation regression both removes a synthetic status-only phantom and
path-sorts a known raw manifest, requiring the corresponding guard to reject either regression.
Historical set-only evidence remains
immutable and is disclosed as such; for a committed target, the guard independently validates the
exact changed-path set and commit tree against the recorded base instead of relabeling the manifest.
Every historical content record embeds its exact status, path, byte-count, and SHA-256 rows and
recomputes the aggregate from those rows. When the reviewed target is later committed, the historical
record SHALL equal the raw commit-blob projection whenever those bytes are identical; a deliberately
checkout-filtered projection is separate environment evidence and SHALL NOT be used to rewrite or
invalidate the published dirty anchor. An uncommitted rejected target remains reproducible from its
recorded rows even after the live worktree moves. Rows explicitly marked as still independently
verifiable from the current worktree are also checked against their current bytes, so a coherent
rehash cannot silently rewrite surviving historical evidence. Canonical OpenSpec markdown, the Section 7
declaration crosswalk, and the public-authoring companion use repository LF attributes so future
checkouts do not reintroduce line-ending-only provenance drift.

Git status cannot represent empty untracked directories. Every freeze therefore records a third
anchor over repository-relative capability-directory paths matching
`openspec/changes/*/specs/*/`. Paths use `/`, retain one trailing `/`, are sorted by ordinal byte
order, and are joined with LF plus one final LF before SHA-256. The packet records the directory
count and hash and fails if any enumerated capability directory lacks `spec.md`. This inventory is
recomputed before and after validation alongside the two status anchors.

For the planning target immediately before this design was created, the authoritative normalized
projection contained 451 records and hashed to
`12332f853ca307a4bfac07b3f706854db7aafa7d338f34967ef6ab50fda587f3`.
This value is provenance evidence for that pre-design target, not the hash of a later freeze.

### 7. Empty and impossible guards receive explicit dispositions

Every empty capability directory is resolved as either a missing delta or stray directory before
freeze. The known `add-runtime-concurrency-limits/specs/state-driven-runtime/` instance was verified
as stray: that completed change declares only `runtime-resource-governance`, OpenSpec reports only
that delta, and its `state-driven-runtime` task reference describes canonical composition rather
than a second delta. The empty directory was therefore removed during planning remediation.
Namespace-pinned forbidden-symbol entries that cannot address a real historical or current owner
are corrected or deleted and receive a regression against the exact qualified owner. Neither an
empty directory nor an impossible negative guard may silently satisfy completeness.

## Risks / Trade-offs

- **[Removing duplicate deltas appears to discard approved text]** → Verify the authoritative
  reshape delta is a requirement-by-requirement superset before deletion and run a cross-change
  duplicate-heading scan after every revision.
- **[Canonical synchronization can race active reshape edits]** → Require both changes to reach
  their approval gates, freeze exact inputs, and synchronize in recorded ownership order.
- **[Vocabulary checks confuse deferred, removed, and newly approved behavior]** → Maintain three
  explicit classifications and test positive as well as negative usages in the active tree.
- **[History-preserving moves are platform- or similarity-sensitive]** → Verify `git log --follow`;
  when exact rename detection is impossible, record predecessor path/commit without altering the
  archived bytes.
- **[Two manifest hashes become incomparable]** → Store the exact pipeline, entry counts, expanded
  line counts, line endings, sort order, and final-newline rule with every freeze.
- **[Process work expands indefinitely]** → Limit this change to the tasks in its reduced proposal;
  product and Section 7B implementation stays in reshape.

## Migration Plan

1. Approve the reduced proposal, two unique deltas, this design, and tasks independently.
2. Resolve the empty delta directory and cross-change provenance checks.
3. Synchronize only the two approved harmonize deltas to canonical OpenSpec.
4. Add the mapped numbered requirements and acceptance criteria.
5. Reconcile active documentation, future-registry links, negative guards, archive provenance, and
   active vocabulary checks without editing historical content.
6. Validate all active changes, freeze with the explicit manifest pipelines, and obtain immutable
   exit approval.
7. After zero drift, create the mandatory harmonization checkpoint commit. Section 8 remains blocked
   until reshape also completes its own approved checkpoint.

Rollback is planning-only: revert this change's canonical/documentation synchronization and retain
the prior immutable verdicts. No product code, released package, or persisted data migration is
involved.

## Open Questions

- Does `docs/specs/17-public-authoring-contract.cs` require an amendment for any externally visible
  consequence of authoring-session lifecycle, or should the behavior remain guard-only?
- Is the empty runtime-concurrency delta directory a dropped artifact or a stray directory?
- Can the Phase-0 kickoff prompt move be represented as a Git rename at the final target, or must a
  separate immutable predecessor record carry its provenance?
