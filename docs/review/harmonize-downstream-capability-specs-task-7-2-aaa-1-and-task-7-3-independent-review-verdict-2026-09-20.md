# Harmonization Tasks 7.2 AAA-1 and 7.3 combined independent review verdict

**Date:** 2026-09-20
**Reviewer:** independent review
**Scope reviewed:** the thirty-six-entry combined freeze on base
`89e3ed55357e849852c1a0f6fefa2124433d7e30`, named by
`harmonize-downstream-capability-specs-task-7-2-aaa-1-and-task-7-3-dirty-manifest-2026-09-18.txt`.
It combines Part A, Task 7.2 AAA-1 hardening, with Part B, Task 7.3 active-documentation
reconciliation.
**Authorization requested:** one combined checkpoint for both parts. This verdict does not authorize
it.

## Summary

Part A is correct and every requested AAA-1 control reproduces. Part B does not meet its own
contract, and the base carries a provenance defect. The target cannot be checkpointed as frozen.

- **BBB-1 (P2):** the activated Task 7.2 entry names an approval-evidence commit that does not
  exist, and no guard resolves it.
- **CCC-1 (P2):** reconciled numbered requirements contradict canonical OpenSpec and the shipped
  source on inbound `EventId` ownership and on who supplies `IWorkflowEventDispatcher`.
- **DDD-1 (P2):** the reconciliation deletes or weakens normative content outside Section 7B. That
  includes acceptance clauses for attempt deadlines, collation independence, and dynamic waits. It
  also turns a canonical `SHALL` into `MAY`, and removes the dispatch hooks that document 15 still
  derives its observer contract from.
- **EEE-1 (P2):** new acceptance criteria AC-118, AC-119, and AC-120 are credited only through AC
  traits on a guard that reads Markdown. Two AC waivers now describe criteria that no longer exist.
- **FFF-1 (P2):** requested Task 7.3 control 1 fails for three of the 22 sources. Each can be
  reverted wholesale to its pre-reconciliation text with the guard staying green.
- **GGG-1 (P3):** hygiene. The committed diff fails `git diff --check`, and a guard-pinned ledger
  line reads "7.1, and … 7.2, and … 7.3".

Every reported validation count reproduces, and both freeze anchors are exact.

## Method

All probing ran in two fresh disposable `git worktree` copies created from `89e3ed55`. One was used
for validation and the simulated checkpoint, the other for mutation and history probes. Each
received the thirty-six frozen entries byte-for-byte, and its porcelain was confirmed byte-identical
to the frozen manifest.

The reviewed worktree was never modified, `HEAD` never moved, and nothing was staged or committed in
it. Every probe commit was detached, and no branch or other ref was created; the branch list is
unchanged. Guard-source variants were rebuilt inside the probe worktree and restored byte-exact.
Both worktrees are removed and pruned.

## 1. Approved Task 7.2 chain

- **Checkpoint `421c39b1`:** parent `e046e04a`, tree `ec8f79e6f9fabc98b02360632e6e1a2c6a885fd4`. This
  is identical to my Round 64 simulated checkpoint tree, with exactly seven paths (two added, five
  modified).
- **Evidence `087817776fe5747ed2d1b153f2dd52d26386b164`:** changes exactly three paths.
  - It adds the Round 64 verdict unchanged (blob SHA-256 `02d13bd2…`, 11,033 bytes) and catalogs it
    with those normalized bytes.
  - It clears the active freeze and adds archived freeze `7.2-post-approval-hardening`: 7 lines,
    602 bytes `41fa3fe2…`, checkpoint `421c39b1`, tree `ec8f79e6`, content record 943 bytes
    `17520209…`.
  - It points the Task 7.2 entry at `421c39b1` in state `ApprovalAwaitingEvidenceCommit`, with the
    Round 64 verdict as its state evidence and fifth registered verdict.
  - This matches the sequence prescribed by the Round 64 verdict.
- **Activation `89e3ed55`:** changes exactly two values. One of them is wrong; see BBB-1.
- **Projection:** in the reviewed worktree, all sixteen archived freezes reproduce manifest, content
  record, and tree from their checkpoint blobs. All eleven entries reproduce their historical rows
  with order-exact maximal current-match pins (emulated `-Check`).

## 2. Freeze anchors and validation

- **Raw commit-real porcelain:** **2,101 bytes**, SHA-256
  `f83199de37f7f8c4537316efa0a7ef50a44834930abd8611c31410a79be6637e`, byte-identical to the frozen
  manifest. 33 modified, 3 untracked, none staged.
- **Scoped content record:** **35 rows, 4,494 bytes**, SHA-256
  `3069fd97597042232bd235192a64289ed9ac5ef19c6ead71cdb13d1e6c888be9`. The review-provenance fixture is
  its only exclusion.
- **Simulated checkpoint:** tree `f6105d787b453a6d0226b83697eecc265bc43677`, with 36 paths (3 added, 33
  modified), identical to the handoff.

| Gate | Result |
|---|---|
| Debug and Release builds, non-incremental, warnings as errors | 0 warnings, 0 errors each |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Full guard lane, dirty target | 224 passed, 14 failed (all `ExecutableBehaviorExpectedRedGuards.Scenario_…`) |
| Infrastructure lane, Release, CI filter `Disposition=Infrastructure` | 224/224 |
| Committed simulated checkpoint, full guard lane | 224 passed, 14 expected red |
| OpenSpec `validate --all --strict` | 18 / 18 |
| Harmonization ledger | 27 complete, 7 open, 34 total |
| `git diff --check` on the dirty tracked diff / on the committed simulated checkpoint | clean / **2 errors** (GGG-1) |
| Changes under `src/**`, `openspec/specs/**`, or `docs/orleans-engine/**` | zero |

## 3. Part A: AAA-1 is closed

`ValidateAppendOnlyHistoricalRecords` now requires every `--full-history` addition of a cataloged
path to reproduce its normalized bytes. It no longer requires exactly one addition. The synthetic
regression covers CRLF/LF identity and a divergent addition. The ledger, design, and archive rule
state the same contract.

On the committed simulated checkpoint `C1`, I used detached commits only:

| Probe | History | Result |
|---|---|---|
| A1 | a cherry-pick copy of `C1` onto its parent, merged back; merge tree equals `C1`; the manifest has two additions | **green** |
| A2a | an independent side commit adds the request path with other content, merged with `-s ours` | **red**, names the side commit |
| A2b | the first addition carries other content, and a later commit changes it to the cataloged bytes | **red**, names the first commit |
| A3 | former exactly-one assertion restored, run on A1 | **red**, two first additions |
| A3k | former assertion restored, run on plain `C1` | green |
| A4a–d | identical re-add sentence removed from the ledger; AAA-1 completion removed; design "every" changed to "first"; archive rule sentence removed | each **red** |

Baseline and final committed controls were green.

## 4. BBB-1 (P2): the Task 7.2 approval-evidence commit does not exist and is never resolved

Activation `89e3ed55` sets the Task 7.2 entry's `approvalEvidenceCommit` to
`087817731b942fb7d90514b54bc7e61190871dc9` (`review-manifest-provenance.json:1791`).
- No such object exists: `git cat-file -t` fails.
- The real evidence commit, also named in the request, is
  `087817776fe5747ed2d1b153f2dd52d26386b164`.
- The two share only the seven-character prefix `0878177`.

The frozen target edits this fixture and would checkpoint the wrong identity as approval provenance.

For the ten other entries, `approvalEvidenceCommit` exists, is an ancestor of `HEAD`, and adds the
entry's state-evidence verdict. For every independent-review `Approved` entry, its parent is also
`reviewedTargetCommit`. Nothing enforces any of this:
- `ValidateReviewStateEvidence` checks nothing about `ApprovalEvidenceCommit` in `Approved` state
  (`OpenSpecCorpusGuards.cs:3998-4004`).
- Only Tasks 5.1 and 5.2 resolve it, in task-specific code.

| Probe | Mutation | Result |
|---|---|---|
| D1 | Task 7.2 `approvalEvidenceCommit` set to `null`, full Infrastructure lane | 222 passed. The only 2 failures are the package-feed guards, which fail identically without the mutation in a probe worktree that has no packed feed. |
| D2 | Task 7.1 `approvalEvidenceCommit` set to forty zeros | review-manifest and Task 7.2 guards **green** |

**Required:**
- Correct the value to `087817776fe5…`.
- For independent-review `Approved` entries, require that `approvalEvidenceCommit`:
  - resolves to a commit and is an ancestor of `HEAD`;
  - has `reviewedTargetCommit` as its parent;
  - adds `stateEvidencePath` with the registered SHA-256.
- Give owner-authorized entries the equivalent rule their evidence supports.

## 5. CCC-1 (P2): reconciled requirements contradict canonical OpenSpec and the source

**Inbound identity.** EV-001 (`05-requirements-events-waits-timers.md:10-14`) now reads "runtime-owned
`EventId`" and "optional causation/correlation/origin metadata". This is wrong on three points:
- **`EventId`:** `WorkflowInboundEvent.Create` takes a caller-supplied `EventId`, and canonical
  `workflow-contracts/spec.md:320` requires a "globally unique caller-created `EventId`".
- **`CorrelationId`:** it is required, and `Create` throws on `null`.
- **Origin:** it exists only on `WorkflowOutboundEvent`.

The reconciled watcher text in document 14 and the handoff correctly reuses a caller-stable
`EventId`. So a numbered requirement now contradicts both the canonical tree and its own companion
guides.

**Dispatcher supply.** Several sources now say the engine supplies the dispatcher:
- DR-033 (`16-requirements-durable-driver.md:319-321`) says `AddOrcaCoreDurableEngine` "SHALL
  register … `IWorkflowEventDispatcher`".
- DR-032 says the engine "SHALL expose" it.
- `production-readiness.md:143-144`, AC-026 (`12-acceptance-criteria.md:135`), and PR-040 say the
  durable engine "owns" it.

The source and the approved matrix say the opposite:
- `AddOrcaCoreDurableEngine` registers no dispatcher. It resolves an application registration with
  `GetService`, and the workflow-event pump exits when none exists.
- Definition registration reports `MissingWorkflowEventDispatcher` for authored `Publish` without
  one.
- Document 17 calls it "the application dispatcher" and makes such a definition host-incompatible
  "when no dispatcher is registered".
- Canonical `workflow-contracts` "require[s] an application dispatcher for authored `Publish`".
- `00-stack-decisions.md` itself says application- or companion-owned adapters.

The Round 64 standard applies: when the trees disagree, the approved change wins. These lines are
new two-tree contradictions introduced by the reconciliation.

**Required:**
- EV-001 must match `workflow-contracts:320`.
- DR-032, DR-033, AC-026, PR-040, and production-readiness must say the application registers
  `IWorkflowEventDispatcher` and the durable engine consumes it. Package ownership of the interface
  type is a separate fact.

## 6. DDD-1 (P2): normative content outside Section 7B was deleted or weakened

The request, the artifact, and the design decision ("without widening product scope") describe a
Section 7B reconciliation. The diff also rewrites unrelated obligations, and none of this is
disclosed:

- **AC-113** (`12-acceptance-criteria.md:199`) loses several clauses:
  - `StepAttemptTimeoutException`;
  - a retry receiving a new attempt deadline and number while keeping `StepOperationId`;
  - terminal `TimedOut` with `WorkflowDeadlineExceededException` and merge suppression;
  - reuse of the persisted attempt deadline and number on host-loss replay.
- **AC-117** loses "correlation routing returns the same result through every certified provider
  regardless of its default collation". That is the provider-collation obligation behind PR-024.
- **AC-116** is repurposed and drops its EV-045 clause, so EV-045 no longer has any acceptance
  criterion. Document 12 had 1 EV-045 reference and now has 0, while EV-045 is unchanged.
- **MG-030** (`09-requirements-management-operations.md:106-112`) goes from "Host/operator
  projections SHALL support grouped operational queries" with enumerated dimensions to "MAY expose
  retained operational statistics".
  - Canonical `durable-persistence-and-outbox` keeps "Provider/host operator projections SHALL
    expose pending, retryable, claimed, permanent-failure, and poison counts…".
  - Task 7.3 asked for provider/operator ownership, not optionality.
- **DU-032 and PR-015** lose manual dispatch, background pumping, poison/failure, retry-delay, and
  observability hooks.
  - Untouched document 15 still states "DU-032 and PR-015 require observability hooks on the
    dispatch pipeline" (`15-requirements-observability-otel.md:301`).
  - It derives `IOutboxPumpDelayStrategy` from them (`:313`) and cites them again at `:406` and
    `:430`.
  - The internal `IOutboxPumpObserver` is still wired into both pumps.
  - OB-060 and OB-061 therefore now derive from requirements that no longer say it.
- **PR-040** loses its package/class ownership and PostgreSQL options paragraph. Document 17 still
  carries that contract, so this item is minor on its own.

**Required:**
- Restore every non-Section 7B obligation verbatim, or route each change through its owning
  approved change.
- Keep EV-045 mapped to an acceptance criterion.
- Keep MG-030 at `SHALL` with its dimensions.
- Keep DU-032 and PR-015 consistent with document 15.

## 7. EEE-1 (P2): documentation traits stand in for acceptance evidence

`AcceptanceCriterionCatalog_HasTraitCoverageOrExplicitWaiver` counts any `[Trait("AC", …)]` in
`tests/**`. The only AC-118, AC-119, and AC-120 traits sit on
`Task73_ActiveDocumentationMatchesTheApprovedSection7BContract`, which reads Markdown strings.

Those criteria require real product behavior:
- **AC-118:** reflection and fresh-package consumers expose exactly the four routes;
- **AC-119:** durable `Publish` commits atomically, retries with the same identity, and stays
  isolated;
- **AC-120:** only `Accepted`/`Duplicate` authorize acknowledgement.

Probe E1 removed the three traits from source only, and the catalog test turned **red**. The artifact
still calls these "executable AC-104 through AC-120 coverage statements".

Behavioral tests already exist and should carry the traits, for example:
- `Engine.Durable.Tests/Events/DurablePublishTests.cs`;
- `Hosting.Tests/WorkflowEventDispatcherTests.cs`;
- the Section 7B package and hosting guards;
- `DurableRecoveryTests`.

Two waivers in `RepositoryGuardTests.cs` are also stale:
- **AC-108 (`:22`):** still waived as "the legacy no-definition-fanout criterion". AC-108 is now the
  current fanout-membership criterion, with no trait.
- **AC-116 (`:26`):** still cites "typed facade routing outcomes". AC-116 is now the acceptance-result
  union.

**Required:** attach AC-118 through AC-120, AC-108, and AC-116 to behavioral tests, or give each an
honest waiver. Remove AC traits from the documentation guard.

## 8. FFF-1 (P2): requested Task 7.3 control 1 does not hold for three sources

The request states that reintroducing "deferred fanout/Publish … in any of the 22 sources" must
fail and name the source. The guard rejects ten exact, case-sensitive phrases. The exact phrases,
tested in six different sources (B1a–f), are each red.

The contract is not enforced, though:

| Probe | Mutation | Result |
|---|---|---|
| B1g | `CLAUDE.md` reverted wholesale to base ("workflow-authored `Publish`/`Cancel`, and definition-targeted event fanout" deferred) | **green** |
| B1h | `docs/normative-source-map.md` reverted wholesale to base (the same items in the deferred registry) | **green** |
| B1i | `docs/specs/01-concept-and-goals.md` reverted wholesale to base | **green** |
| B1j | "Definition-targeted event fanout is deferred." appended to document 10 | **green** |
| B1k | "Workflow-authored `Publish` and `Cancel` are deferred." (the base document 03 wording) appended | **green** |
| B1l | "Events use one of exactly two routes." appended to production-readiness | **green** |
| B1m | the removed document 13 future-registry row for `Publish` appended to document 13 | **green** |
| B3d | PR-040's SQL Server entry and sentence removed, while other sources keep the joined marker | **green** |

A string emulation of all 22 single-source reverts agrees. Exactly those three sources escape, and
they are the ones whose stale claim was a deferred-registry entry.

The other requested controls behave as claimed:
- B3a–c: headings, AC bullets, and an all-holder marker removal are **red**.
- B2a and B4c: reordering or coherent editing of the artifact without a digest change is **red**.
- B2b and B2c: reordering or dropping a row with the digest updated and the guard rebuilt is **red**
  on the path list.
- B4a and B4b: reopening Task 7.3 or removing its evidence is **red**.
- Task 7.4 remains open, and no Orleans path is in the target.

**Required:** bind the reconciled statement in each source that previously carried a
deferred-registry claim. For example, require the exact current `Publish`/fanout sentence in
`CLAUDE.md`, the source map, and document 01, and reject both items in any deferred-registry row.
Case-insensitive matching would catch B1j. Per-source hosting markers would catch B3d.

## 9. GGG-1 (P3): hygiene

- **Trailing whitespace:** the new artifact ends lines 3 and 4 with two spaces. It is untracked, so
  the reported `git diff --check` is clean. On the committed simulated checkpoint,
  `git diff --check HEAD^ HEAD` reports both lines.
- **Ledger wording:** Task 7.20 and its pin (`TaskAccountingGuards.cs:68`) read "harmonization task
  7.1, and harmonization task 7.2, and harmonization task 7.3".
- **Long lines:** archive rule 5 now has a 128-character line (`docs/archive/README.md:24`). The Task
  7.3 completion lines and phased-plan step 6 are unwrapped.
- **Blank lines:** document 10 has a doubled blank line before PR-018.

## 10. Reviewer hygiene and next steps

`HEAD` remained at `89e3ed55357e849852c1a0f6fefa2124433d7e30` throughout. Nothing was staged, and I
created no commit or ref in the reviewed repository. When this verdict was written, the repository
still showed exactly the thirty-six frozen entries. Both disposable worktrees are removed and
pruned.

This verdict's filename matches the Task 7.2 discovery glob. A superseding freeze must:
- register it as a `REJECT` in the Task 7.2 entry's `verdictEvidence` (the entry stays `Approved`
  on the Round 64 evidence);
- add a `rejectedFreezes` record for this request, manifest, and verdict;
- catalog the verdict in `appendOnlyRecords` with its LF-normalized bytes.

Part A may be refrozen unchanged. Correct BBB-1 in the same fixture edit.

## Determination

Part A is a sound, fully mutation-proven closure of AAA-1.

Part B reconciles the targeted Section 7B vocabulary, but it has four problems:
- it introduces new contradictions with canonical OpenSpec and the shipped source;
- it silently weakens unrelated normative acceptance and management obligations;
- it credits three behavioral acceptance criteria to a documentation string check;
- its guard does not enforce the reviewer control the request names.

The base also records a nonexistent approval-evidence commit that no guard can detect. These are
provenance and normative-content defects, not style.

**Verdict:** **REJECT**
