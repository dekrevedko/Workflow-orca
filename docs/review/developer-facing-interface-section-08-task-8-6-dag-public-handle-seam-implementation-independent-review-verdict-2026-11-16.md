# Public DAG facade (Task 8.6 seam) final implementation independent review verdict

**Date:** 2026-11-16
**Reviewer:** independent review
**Scope reviewed:** the 130-entry freeze in `X:/ocv302` (branch `draft/admit-dag-public-handle-seam`) on parent
`da1a4729ea2cdb9474eb219eb507760574f83dcb`, named by
`developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-implementation-dirty-manifest-2026-11-15.txt`.
It is the owner-authorized combined candidate for `admit-dag-public-handle-seam`:
- the RV-4 contract and the Task 0.1 process rows;
- canonical synchronization of five blocks;
- the public facade source and a PostgreSQL checkpoint-lineage fix;
- tests on InMemory, PostgreSQL and SQL Server, including real process restarts;
- documentation reconciliation and guard and fixture updates.

**Authorization requested:** final review of the combined contract, canonical, source and validation candidate.

This verdict authorizes the checkpoint of this exact target and its direct-child evidence commit. It authorizes no task
checkbox, no registry promotion to Complete, no merge into the current main branch and no reshape 8.3–8.6 activation.

**Reviewer disclosure:** in an earlier session I drafted RV-1 to RV-4 of this contract and built the throwaway prototype in
`X:/ocv303`. RV-4 was approved by a separate pre-gate review. This verdict therefore judges the implementation, the
synchronization and the evidence against that approved contract. It does not re-open the contract text, of which I am not an
independent judge.

## Summary

The seam is exactly the approved one: two internal handle constructors, three non-public types and seventeen members, with
every typed outcome owned by `OrcaCore.Dag`. The binding, round-trip, replay, unavailable-host and fault behaviour match the
contract and pass on all three storage providers, including four-process restarts. No blocking defect was found.

Five findings are recorded:
- **F1 (P2, carried forward):** the new run-input integrity layer has no test that reaches it.
- **F2 (P2, carried forward):** the target sits 18 commits behind the main branch, and its scope guard cannot survive a merge
  unchanged.
- **F3 (P3):** a DAG whose child workflow is not installed registers, then fails at start with an internal exception type.
- **F4 (P3):** the read-path integrity check is wider than the contract states.
- **F5 (P3):** two wording fixes.

## What was checked

- **Seam (entry point 1).**
  - `DagDefinitionHandle<TRunInput>` and `DagRunHandle` each have exactly one internal constructor, matching doc 17
    §17.2.6 and the deltas.
  - The typed throws all sit in `OrcaCore.Dag`: `DagRunNotFoundException`, the extended
    `DagRunDefinitionMismatchException`, `DagRunInputContractMismatchException` and `DagRunFaultedException`. So do
    foreign-node rejection by reference identity and the declared-output check.
  - The shipped `OrcaCore.Dag.Hosting.dll` is compared for equality against three types and seventeen members. The linked
    mapping harness is still compared against fifteen.
- **Projection and hosting (entry point 2).**
  - `DagFacadeRegistry` uses only public constructors and enum members. It constructs no DAG exception and none of the four
    opaque values.
  - Coordinator reflection is gone from the Hosting tests and from provider certification.
  - `Register` installs the plan for recovery.
  - A run whose stored input does not match the installed binding is retired by the scheduler, and the mapping reader
    refuses it independently (probes C1 to C3).
- **Durable side (entry point 3).**
  - A submitted run input that does not round-trip is rejected with `NotSupportedException` before any key lookup or append.
    Mapped inputs keep `Normalize`, and child outputs keep the join path.
  - Stored input is verified against its own graph key on every store read (integrity), then against the installed key and
    type (binding).
  - Start resolves the key owner on retry and after a competing create. A supplied run-id collision stays a separate
    conflict.
  - Cancellation on a faulted run returns `AlreadyRequested`, or the fault signal, from durable facts.
  - The terminal wait rereads the latest checkpoint after each notification.
- **PostgreSQL (entry point 4).**
  - Parent and root lineage are read, inserted and updated, including an update to null, and the provider test asserts both
    directions.
  - The columns are added to `001_initial.sql` in place, so only greenfield databases get them, as the request states.
- **Tests (entry point 5).**
  - No existing assertion was weakened. Changed existing tests do one of three things: move from reflection to the public
    facade, assert the new replay semantics, or reflect `Resolve` returning null for an unavailable binding.
  - The fault and reconciliation race uses a provider-port barrier, not timing.
  - Competing creates use a barrier after the key lookup.
  - The restart certification runs four processes with distinct PIDs per provider.
- **Process, guards and fixtures (entry point 6).**
  - The five Proposed rows carry the RV-4 block hashes.
  - The owner-authorized candidate marker is source-pinned together with the owner-direction record. Historical guards see
    prior bytes only through `git show <parent>`, with pinned before and after hashes.
  - The settlement validator accepts this change as a third owner for one heading only.
  - Catalogue fixtures are additive, apart from the active-freeze pointer and counts.
  - Doc 17, docs 08/09/10/12, CLAUDE.md, two implementation documents and the Task 8.0 map row are updated, and doc 17
    lists `DAG-RUN-FAULTED` and `DAG-RUN-INPUT-CONTRACT-MISMATCH`.
  - Reshape 8.3–8.6 stay open, and reshape 7.20 changes only its counts.
  - Provenance is reported as 223 rows / 59,536 B / `6c27f88e…`, with no unresolved operation and semantic approval
    eligible. That eligibility holds only together with this approval, because the canonical sync precedes it by owner
    direction.

## Validation

As the owner asked, freeze anchors were not re-derived. The author worktree's status equals the 130-row manifest and nothing
is staged. I copied the 130 files into a disposable detached worktree `X:/ocv312` on the same parent and ran everything
there. The copy was byte-identical to `X:/ocv302` before and after the probes.

| Lane / probe | Result |
|---|---|
| Release solution build, warnings as errors | 0 warnings, 0 errors |
| Hosting / Durable engine / Ephemeral / Acceptance | 75/75 · 113/113 · 405/405 · 37/37 |
| PostgreSQL / SQL Server, real containers | 121/121 · 92/92 (the three SQL Server facade tests confirmed by name) |
| Infrastructure guards | 317/319 without a packed feed; the two package-feed guards then pass 2/2 against it, so 319/319; exactly 14 ExpectedRed |
| Package fixtures (`run-package-fixtures.ps1 -CompletedSection 8 -CompletedTask 8.6`) | 9/9 consumers, including `dag-hosting`, from a freshly packed feed; packed-package source provenance passes |
| A: different-key start reported as an ordinary conflict | **red** (1 Hosting facade test) |
| B: submitted run input not round-trip checked | **red** (2) |
| C1 / C2: scheduler skip removed / mapping-reader refusal removed | green / green (each defends alone) |
| C3: both removed | **red** (stored-input redeploy test) |
| D: pre-fault status forced to `Running` | **red** |
| E: owner-integrity check removed from store reads | **green** 4/4 (see F1) |
| F: start with the child workflow not installed | `Registered`, then internal `DurableDagDefinitionUnavailableException` (see F3) |

Every mutated file was restored and hash-checked, and the facade baseline was green after each batch.

## Findings

- **F1 (P2, evidence precision; carried forward): no test reaches the new run-input integrity layer.**
  - All four corruption variants in `CorruptStoredInput_RemainsAnIntegrityError_OnReopenReplayAndMapping`, and the
    corrupt-key case in the shared `FacadeBindingsAsync` certification, rewrite the checkpoint through `JsonNode`.
  - That produces non-canonical frame bytes. Instrumentation shows the rejections come from existing layers:
    - `key` and `json`: "DAG checkpoint coordinates or noncanonical bytes", from `DurableDagWire.Read`;
    - `format` and `fingerprint`: from `DagRunValidation.ValueBytes`.
  - The new `VerifiedRunInput` checks are never reached: the recorded value key against its own graph, and malformed JSON
    with a valid fingerprint. Probe E stays green.
  - Without the new check, a canonical checkpoint whose value key disagrees with its graph key, and whose bytes equal the
    submission, replays as `Existing`. That is exactly the RV-4 scenario "Start-key owner examined during replay is
    corrupt". The product code is correct today, but a regression would go unnoticed.
  - The test name also claims a mapping leg, but its plan has no nodes.
  - In the next target, build the corrupted frame canonically, through the frame serializer or the certification's
    independent wire builders. Then cover value-key mismatch and malformed JSON with a valid fingerprint on reopen, replay
    and the scheduler/mapping path, on InMemory and both providers. Rename or narrow the Hosting test.
  - Task 3.2 (promotion to Complete) must not run before this is fixed.
- **F2 (P2, integration; carried forward): the target cannot be merged into the current main branch unchanged.**
  - The main branch (`ba2b057d`) is 18 commits past the parent. 17 of the 130 paths also changed there, including:
    - `openspec/specs/management-and-querying/spec.md` (a different block);
    - CLAUDE.md, docs 09 and 12, and the reshape ledger;
    - `OpenSpecCorpusGuards.cs`, `DagPumpSliceGuards.cs` and other transition guards;
    - the provenance, history, package-source and crosswalk fixtures.
  - `DagFacadeReviewCandidate.SourcePaths` requires `git diff --name-only da1a4729 -- src` to equal its fifteen files. On
    the main branch that diff also contains nine `src/OrcaCore.Engine.Ephemeral/` files from PRs #23 and #25. The main
    branch already handles the same pattern in `Task23CandidatePaths` with a separate exact exclusion.
  - Checkpoint this target on `da1a4729` as reviewed; do not rebase the frozen target. Integrate through a reviewed merge
    that:
    - gives the candidate scope the same exact exclusion;
    - re-pins the whole-file canonical record and the overlapping fixtures;
    - reruns the canonical synchronization gate.
- **F3 (P3, public outcome): an uninstalled or mismatched child workflow surfaces late and untyped.**
  - With the child workflow not added to the durable engine, `Register` returns `Registered`. The first `StartOrGetAsync`
    then throws `OrcaCore.Durable.Hosting.Dag.DurableDagDefinitionUnavailableException`, which is not a public type.
  - A child whose installed fingerprint differs surfaces as `InvalidDataException("The installed DAG start binding is
    inconsistent.")`, which reports a configuration error as data corruption.
  - Neither outcome is in doc 17 or the contract.
  - Consider validating child bindings at registration with a typed result, like the workflow `HostIncompatible`, or
    document a public exception.
- **F4 (P3, behaviour wider than stated): the read-path integrity check blocks every API for a corrupt run.**
  - Because `DurableDagRunStore.ReadAsync` now verifies the stored run input, the following throw for a run whose stored
    input is internally inconsistent: cancellation, terminal waits, snapshots, notifications, scheduler visits and
    `ReconcilePoisonAsync`.
  - That run can then only be repaired outside the product. The contract names only registration, start and reopen.
  - Fail-closed is defensible. Either say so in doc 17 and the design, or limit the check to reopen, replay and mapping.
- **F5 (P3, wording):**
  - CLAUDE.md now says the extension "adds no delegates". Both constructors take delegate parameters; "exposes no mapper
    delegates" is accurate.
  - `DagRunCertification` keeps a comment that "only this harness uses reflection" after the reflection was removed.
- **Recorded:**
  - The owner-authorized combined delivery replaces the separate 0.2 and 1.2 gates. I relied on the recorded owner-direction
    artifact.
  - In the C probes, the scheduler skip and the mapping-reader refusal each defend alone. The test detects only the loss of
    both, which is acceptable defence in depth.
  - In the faulted-wait test, a fault committed after subscription is caught by the post-subscribe recheck, not by a
    notification delivered to a loop that is already waiting. The level-triggered contract is met either way.

## Hygiene and checkpoint sequence

`X:/ocv302` is at `da1a4729` with the 130 frozen entries and nothing staged. When this verdict was written it showed exactly
those entries plus this new, untracked verdict. My disposable review copy `X:/ocv312` is removed. `X:/ocv303` (my
prototype) and the main checkout (`ba2b057d`, branch `fix/dag-task-3-1-certification`) were not touched.

1. Confirm zero drift against the 130-entry manifest.
2. Stage exactly those 130 paths from the manifest (not `git add -A`) on parent `da1a4729`, expecting the author's projected
   tree `8ef5842bb8c7952f30f0f4184e97baf45ba986c0`, and commit.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the history pointer.

There is no activation in this chain. One next target can combine:
- the F2 reviewed merge of the main branch;
- the F1 tests;
- recording Tasks 0.2, 1.2 and 2.7 against the real checkpoint and evidence commits;
- the Task 3.2 registry promotion.

F3 to F5 may ride along.

## Determination

The contract's public surface, ownership split, four-part binding, run-input round-trip rule, unavailable-host behaviour and
level-triggered fault outcome are implemented as approved and verified on all three providers. The open items are evidence
and integration work for the next target and do not affect the product behaviour reviewed here.

**Verdict:** **APPROVE**
