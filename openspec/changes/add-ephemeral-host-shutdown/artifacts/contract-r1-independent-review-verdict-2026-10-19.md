# Issue #2 shutdown contract R1 — independent review verdict

**Date:** 2026-10-19
**Target:** base `fa88986`, 24 entries, tree `b1611b09f2774bab0a84ae2a1492abd71a1e4459`, porcelain
SHA-256 `6d09f39c…`. HEAD, the porcelain hash and the tree all reproduced, and nothing drifted during
review. This was a diff-only review from `7d3f144f…`.

## Verdict: APPROVE, with one binding correction (F2) applied in the 2.1 transition target

F1 is resolved. The design and both deltas now define the irreversible sequence
Open → Closed → Stopping → Completed:
- `ApplicationStopping` and abortive disposal only close admission.
- Only the public `StopAsync` or the engine's hosted `StopAsync` enter Stopping, which cancels and
  joins.
- While Closed, accepted instances keep ordinary queries, waits, commands, ingress, timers, retries
  and grants.
- From Stopping, live operations are rejected per operation, each with a stated rationale.
- After Completed, nothing creates work. The fast path returns the cached result without reading
  `TimeProvider`.
- Three drain recipes are documented: the ordered Generic Host drainer (including the
  `ServicesStopConcurrently` caveat and the shared `ShutdownTimeout` budget), the explicit host
  owner, and the plain-DI submission gate.
- The RunAsync migration note and the 3.1/3.4 cases are added.
- The seven public declarations are byte-equal.
- The guard edits are only the mechanical provenance pins: record `9547ee6c…`, artifact `87f527a9…`,
  semantic approval still false.

**Authorized now:**
1. Stage exactly the 24 manifest paths, excluding this verdict. Confirm tree `b1611b09…` with parent
   `fa88986`, then commit.
2. Add a verdict-only child commit; 1.4 may be ticked there.
3. Prepare the 2.1 transition target, including the F2 correction below. The 2.2 review checks F2's
   exact wording together with the canonical sync, so no separate contract round is needed.

## F2 — P2 — Closed must not abort a provisional start that was admitted before close

**Problem.** The design (§2) and the management delta make publication and Closed share the
acceptance/abort election. A start already running its first step when `ApplicationStopping` fires
therefore loses to close. It stays unpublished, and its caller gets host-stopping once the step
returns, even though the step ran and its effects happened.

That defeats the approved drain intent. The Generic Host recipe has the drainer await "its own pending
starts", and those starts would end in host-stopping instead of `Accepted`. The only remaining option
would be a key replay, which is also refused while Closed.

**Correction for the 2.1 target:**
- Closed refuses new, queued pre-Init and replayed starts. A queued start that has not run Init may
  still fail as host-stopping, as now.
- A start that already passed its initialization admission before close proceeds normally while
  Closed and may publish `Accepted`.
- Move the acceptance/abort election to Stopping. Only an unaccepted start that is still outstanding
  when Stopping begins is fenced and fails as host-stopping, unless it already has an authoritative
  original error or caller cancellation.
- Update the 3.1 case "provisional work losing close" to cover both sides: acceptance during Closed,
  and election at Stopping.

## P3 (non-blocking)

**Count-only diagnosis.** Once Stopping begins, snapshot and state reads are rejected, and Incomplete
carries only a count, so the stuck instances can't be identified at that point.
- The rationale is acceptable: codec and dependency work.
- The guides should point operators to the pre-Stopping window and the existing stuck-step and
  lifecycle diagnostics as the way to identify them.

## Lanes

I did not re-run the lanes. This was a contract-text diff with mechanical pins only, and the reported
results (OpenSpec 23/23, Release 0/0, Infrastructure 249/249) are on the exact tree, which I
reproduced. The 2.2 review re-runs Infrastructure on the transition target.
