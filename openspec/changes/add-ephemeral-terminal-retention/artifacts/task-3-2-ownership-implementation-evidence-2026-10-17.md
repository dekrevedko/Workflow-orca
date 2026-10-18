# Issue #1 tasks 3.1/3.2 — ownership implementation evidence for the 3.3 gate

## Authority and preserved baseline

The candidate remains uncommitted on `issue/1-ephemeral-retention`, parent
`ef88957af4c41bf18def689ba81c71e1a1d18696`. The task 3.1 reviewer approved the
test baseline at tree `aed8ba3fea6a66e59f0a1cc92e90291385ac5333` and explicitly
forbade checkpointing its red state. That verdict authorizes implementing 3.2 on
those bytes and bringing 3.1 and 3.2 together to an independently reviewed, green
3.3 checkpoint. This packet requests that review; passing tests do not activate source approval.

The fourteen approved files are preserved, with their original paths and raw
bytes, in `task-3-1-approved-baseline-2026-10-17.zip`: 177,017 bytes, SHA-256
`4308f221951c69d6afb1304495da68691677226cd74ef82252000141f3b45fc5`.
The adjacent inventory records every byte count and hash, the approved tree and
base, and the 2,158-byte fourteen-row content record SHA-256
`7dcc22e7488e12f647ec948475da8825058d21665789844d65165944da2a2aa6`.
The carried 3.1 verdict is 11,951 bytes, SHA-256
`d10c172dc8bae4b7ae2a3d63d0d1d39f812d0fb1b4639b4490d69a9e62d452a7`.
A new Infrastructure guard verifies this archive, every inventory row, both
anchors and the raw verdict. Reproduction does not depend on Git retaining an
unreferenced tree indefinitely. Earlier dated artifacts remain unchanged.

The implementation is downstream of binding checkpoint
`5f17b7e104b54aa4849795095a161216359ca6c7`, tree
`baf557450590d86178e041194002a32cedc2b04c`, and its sole direct child verdict
commit `ef88957af4c41bf18def689ba81c71e1a1d18696`. The existing git-backed binding
validator still checks that chain. Tasks 3.1 through 5.4 remain open and both
registry records remain `CanonicalApprovedSourcePending`, with null source approval.

## Implementation decisions

`EphemeralInstanceLifetime` owns a provisional-publication reference, real
physical attempts, capacity-release completion, retry/resource continuations,
timer dispatch, lane work, commit publication, event routing and facade
publication. Ownership is acquired before dispatch. Logical timeout and
terminal status do not imply physical settlement. Throttle/pool disposal is
tracked separately through the existing retained-capacity lease. Unthrottled
attempts are protected by the same lifetime tracking.

Acceptance and abort elect under the instance gate. The facade holds publication
ownership while it elects acceptance and installs its instance/start entries
under its existing gate. A cancellation after acceptance cannot undo that election.
Every error escaping after Init retains the initialized instance reference and
fences any active provisional state to Terminated on the owning execution lane,
using CancellationToken.None. Previously terminal state remains immutable. The
original caller error/token is preserved, including when commit notification or
cleanup throws. Provisional units publish no returned facade handle or start key.

The abort path eagerly releases a physically settled unpublished unit in either
configuration mode. It does not wait for noncooperative work before returning
the original error: unsettled ownership prevents removal and releases the unit
when its last owned work returns. Cancellation of terminal retry/resource waits
does not cancel or prematurely release outstanding user physical attempts.

The lifetime gate only mutates bookkeeping or selects cleanup actions. User
work, observers, cancellation callbacks, cleanup actions, metrics and storage
removal run outside that gate. The internal completion observer is a deterministic
test barrier while ownership is still held; its default is null. `WhenSettled`
completes after the last owned work has returned and, for an aborted unpublished
unit, all registered cleanup has been attempted. It is not a public API, global
retired-ID cache or synchronous physical-completion requirement.

Runtime removal, facade/start cleanup, router accepted-event deduplication and
timer cancellation form one protected cleanup unit. Cleanup registration is
deduplicated by owner identity; actions and captured references are cleared on
release. Save and commit paths acquire ownership, so stale owners cannot reinsert
a released instance or publish new work. Completion also refuses to overwrite
any terminal status. Definitions remain registered. No count/age retention,
sweeper, query pin or output-wait pin is implemented in this slice.

The old EvictInstance/EvictTerminalInstances paths now use the same retirement
election, preserving the active-instance error behavior and refusing unsettled
terminal units. They remain internal test retirement probes. If task 4.2 isolates
these helpers, all ownership/removal tests must move to the policy retirement
path at that gate with equal or stronger assertions.

The event router moved unchanged except for the reviewed ownership additions
from the large facade file into its own file, keeping each below the existing
1,000-line guard. Its shared FacadeValueFactory changes from file-local to internal
so both files use the same implementation; no new friend or public member is added.

An actual provisional transition records the existing lifecycle counter with
the fixed `orca.lifecycle.origin=provisional-abort` tag. It does not call the
application termination-command path or emit its count/duration. Already terminal
aborts add no second transition. Metric labels introduce no instance or start-key ID.
New listener tests verify both error modes in both configurations. Full retirement
telemetry remains at the policy gate.

## Every change to the approved PhysicalOwnershipTests.cs

The adjacent `task-3-2-physical-tests-review-diff-2026-10-17.patch` is the complete
file diff against the archived approved file. Only these changes are made:

1. Seven method-level dispositions change from ExpectedRed to Implemented. This
   promotes the same 52 cases; all 22 controls remain controls.
2. CancelledStartAfterInit and ExceptionAfterInit each await WhenSettled after
   their original terminal-fence assertion and before their original removal
   assertion. The await is conditional on the expected terminal status so a
   missing fence still reports its original behavior assertion instead of
   hiding behind a never-completing settlement wait. Every original assertion
   still executes inside its original scope, including on a wrong status.
3. The 36 timeout cases await WhenSettled after the physical-body barrier and
   before the original terminal/removal assertions. A user task returning does
   not guarantee its capacity-release and runtime continuations have settled.
4. A class-level AC-535 trait connects the now-passing provisional-abort evidence
   to the canonical acceptance criterion. It changes no test body or case data.

There are no other test-file edits: no assertions, case rows, expected values,
timeouts or setup code are removed or weakened. The review preparation script
reconstructs the current file from the approved copy using precisely those edits.

## New callback and regression coverage

ContinuationOwnershipTests adds eighteen cases. Twelve cases cover real pending
retry-backoff, resource-grant and due-timer completion callbacks, both internal
settings profiles, and single/bulk retirement. Each proves that a terminal unit
cannot retire during completion, then retires after settlement; facade and router
records disappear, stale Complete/Save cannot resurrect it, advancing time does
not dispatch a late retry/grant, and another resource holder remains physically
protected. Two cases prove accepted instances reject the provisional abort path.
Four verify internal-abort telemetry without an application termination command.
All paths use actual engine/storage/router behavior and explicit barriers/fake time.

The original 74 approved cases and eighteen added cases pass, alongside all 88
pre-existing owning-project tests. CI's owning-project command is unfiltered.

## Scoped guard and accounting changes

The pending DAG fingerprint amendment no longer compares arbitrary ephemeral
source to two fixed textual edits. Its proposed-stage rule now freezes DAG and
DAG.Hosting source, its delta specs, and its named public contract fixtures.
Existing fingerprint, seven-API-addition, git approval and canonical-text checks
remain. Canonical append exceptions still allow only the exact issue #1 blocks.
The active/archive fail-closed resolver is retained. Negative DAG-source/delta
probes must fail while this separately authorized ephemeral implementation passes.
This is an explicit guard-scope change for independent review, not DAG source approval.

The declaration crosswalk adds this slice's three new theories and one evidence
guard: 347 physical files / 1,439 declarations, active 199 / 751. Historical
declarations, retired dispositions/digest and earlier artifacts remain unchanged.
Recovery and task-accounting pins and the maintained reshape task 7.20 note match
those totals; no checkbox changes. The package-source fixture updates only the
ephemeral source digest, from the approved red baseline's `a5bb0b7745a4c762b56bc3a825e66b7fcd6872382f24ae4480cc07a22a335855`
to `c727e9e70c658b634854ff3e22c063c838f9cd09257161c03e6f6e8a3fc37566`.
Eleven other package entries and every executable API/compile/package-surface
baseline remain unchanged.

The wider CI unit lane exposed missing acceptance-catalog accounting inherited
from the canonical transition. RepositoryGuardTests now explicitly defers AC-533,
534 and 536–542 to their actual remaining source/consumer gates, with partial
3.2 coverage stated where relevant. AC-535 is linked to the real passing tests.
These are reasoned coverage deferrals, not implementation claims or test skips.

## Validation, freeze and probes

The self-inclusive dirty manifest is the entire combined target. External receipts
under `.artifacts/publishing-validation/issue-1-ownership-implementation/` record
native and NUL porcelain, raw sorted status/path/byte-count/SHA-256 rows, a copied
index tree, and an exact staged rehearsal in E:/ocv200. Ordinary index stays empty.
The full diff for review is `git diff aed8ba3fea6a66e59f0a1cc92e90291385ac5333 <candidateTree>`;
the external approved-to-candidate.diff preserves it even if the old tree is later
collected. The ZIP independently reconstructs all fourteen old blobs. Freeze
hashes and the final tree are external to avoid a self-referential evidence hash.

Validation commands and actual outcomes are recorded in the external validation
JSON and adjacent case-results TSV. Required final lanes: strict OpenSpec, complete Release
build with warnings as errors, exact twelve-package feed, all Infrastructure
guards, and all six CI unit projects with coverage. The short copy must be staged
at the exact candidate tree and must not drift before/after these checks. Logs,
TRXs and coverage XML remain local evidence; generated binaries/packages are removed.

The initial long-path Infrastructure attempt was not green: its absent local
feed caused two failures and compile-fixture cleanup hit the known Windows
long-path issue. It is not acceptance evidence. The final short staged run uses
fresh current-source packages and must pass the complete lane.

Mutation results are adjacent. Physical retirement, abort fencing, stale saves,
terminal immutability, router cleanup, internal-abort classification, DAG scope
and baseline/verdict anchors are exercised one at a time in the disposable copy,
then restored and rebuilt. The early-capacity-release control is reported
separately: it fails at its governance-wait timeout barrier when the mutant never
parks the second start, not at a behavior assertion. No such timeout occurs in
the unmodified candidate. The mutation report distinguishes this outcome.

## Carried P3 ownership

| Item | Disposition |
|---|---|
| Post-return removal races | Addressed by the three reviewed settlement waits; no synchronous user-task settlement assumption |
| Callback coverage | Addressed with retry, resource and due-timer completion barriers |
| DAG fixed-edit exemption | Replaced by the explicitly reviewed DAG-owned scope described above |
| Old eviction probes | Same settlement-aware retirement election here; mandatory migration if isolated at 4.2 |
| Ledger row 209 label | Historical ledger stays immutable; next separately reviewed ledger reconciliation |
| Doc 17 section 17.4 guard | Task 4.2, together with the implementing API and baselines |
| Master integration conflicts | Separate frozen/reviewed integration using a merge commit, never squash; no integration here |
| Temporary NormativeContractGuards ownership-row mapping | Task 4.2 |
| DAG active-path archive coupling | Already resolved by 3.1; shared fail-closed resolver retained |
| Guides' implementation-pending release text | Explicit reconciliation at 4.5/5.3 before shipping claims |
| Developer guide draft wording | Task 4.5 |

Source task 4.1, public policy, canonical edits, registry activation, task ticks,
commits, push and GitHub issue changes remain outside this review target.
