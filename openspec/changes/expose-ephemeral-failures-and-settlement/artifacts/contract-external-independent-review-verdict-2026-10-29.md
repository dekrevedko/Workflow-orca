# Issue #16 contract — external independent review verdict

**Date:** 2026-10-29
**Reviewer:** an independent reviewer (Claude). This is separate from the author's two Codex passes:
- `contract-independent-review-verdict-2026-10-29.md`, a self-review;
- `…-2026-10-29-review.md`, a same-chat review that does not claim to be external.

I neither replace nor edit those files.

**Target:** `codex/issue-16-ephemeral-failure-settlement`, base/HEAD `0a8e375` (GitHub master). The
manifest `contract-manifest-2026-10-29.txt` lists 26 paths.

**Freeze.** All three anchors match `freeze.json`, and nothing drifted:

| Anchor | Value | How I checked |
|---|---|---|
| Candidate tree | `cf054886e39c20d9294b3baac55e58fa19c6bbdf` | reproduced through a private index |
| Content SHA-256 | `995a02e5…` | recomputed with the stated rule: path NUL lowercase-file-SHA NUL, in manifest order |
| Porcelain `-z` SHA-256 | `1e1f69a9…` | excluding the review verdicts |

## Verdict: APPROVE the contract

There are no P1 findings. One P2 (F1) is folded into the task 2.1 transition, and the P3 notes are
optional.

The contract still needs the owner's **explicit approval** under task 1.4 before it is checkpointed.
That approval covers:
- the two-member API;
- the closed version-2 failure subformat amendment of `orcacore-json-v1`.

**After owner approval:**
1. Commit exactly the 26 manifest paths, excluding all three review verdicts. Confirm tree
   `cf054886…` with parent `0a8e375`.
2. Add a verdict-only child commit that contains this external verdict. The Codex reviews may go in
   the same child as transparency evidence.
3. Prepare the 2.1 transition with F1 applied. The 2.2 review checks F1's exact wording.

## What I verified

**Issue fit.** Part A follows the owner's direction: `InnerException` carries the original object,
and `ExceptionType` is only detached text. Part B follows the issue's
`WaitForSettlementAsync(CancellationToken)`. Each acceptance criterion maps to a requirement and a
task. Durable live exceptions and changes to output-wait semantics stay out of scope.

**Exact API.** The addition is exactly two members inside existing `OrcaCore` types:
- a nullable `WorkflowFailure.ExceptionType` getter;
- a nonvirtual base `WorkflowInstanceHandle.WaitForSettlementAsync`, which the typed handle inherits.

No other surface is added: no constructor, setter, exception type, option or overload.
`WorkflowOutputUnavailableException` uses its existing `InnerException`. On durable handles, the new
method throws `NotSupportedException` without I/O, after the pre-cancelled-token check.

**It conforms to the canonical contract rather than redefining it.**
- Today `InMemoryExecutionStateAdapter.cs:583` wraps every non-OrcaCore exception as
  `WorkflowLifecycleException` (`WF-LEGACY-LIFECYCLE`). It also unwraps *every*
  `TargetInvocationException`.
- Canonical `workflow-contracts` already requires arbitrary exceptions to normalize to the
  catalogued generic code. Durable already uses `WF-STEP-UNHANDLED`.
- The contract fixes both: it uses `WF-STEP-UNHANDLED`, and it unwraps only proven engine-owned
  invocation wrappers.

**Type text versus "protocol identity".** Canonical `developer-facing-surface` says CLR type names
"SHALL remain diagnostic text rather than protocol identity", and docs 03 and 17 say the same.
`ExceptionType` is exactly that diagnostic slot:
- the code remains the only matching key;
- the type text is opaque and never resolved;
- `WorkflowFailure` keeps reference equality.

There is no conflict (P3-1 asks for an explicit sentence).

**Live-state reconciliation.** The modified "Typed committed root state and output are queryable"
allows exactly one live exception, an ephemeral output-unavailable wrapper's `InnerException`. It
states that the exception's graph is application-owned and is not settlement proof. Snapshots,
state/output values and branch/item data never hold it, and durable wrappers never expose it.

**Election and aggregates.**
- The original exception is associated by failure/attempt identity, not by equal
  type/message/path values.
- Only the exhausted final retry counts. Late timeout or cancel losers cannot rewrite the failure.
- A true `SFE-JOIN-FAILED` gets no invented `AggregateException`; its causes carry their own types.
- A `WhenAllOutcomes` recovery releases obsolete associations.
- Retirement releases the engine's references, and a weak-reference test is required.

**Wire amendment.**
- A null type keeps the exact five-property bytes. A non-null type uses one exact seven-property
  version-2 order.
- Strict negatives cover duplicate, unknown, missing, blank and wrong-version input.
- The occurrence discriminator stays version 1, and the codec ID is unchanged.
- The text is honest that old readers reject the new form, so a matched-version rollout is needed.

**The settlement receipt is sound.**
- It completes only on terminal status plus final physical handoffs. A transient zero-work state
  while Waiting does not count, which is why the internal `WhenSettled` epoch is correctly not
  exposed.
- It is monotonic. Observers subscribe and then recheck, hold no lease or pin, and their cancellation
  stays local.
- Retirement counts as already-settled success, with no payload or tombstone retention.
- A provisional start that never got a handle gets no receipt, and its no-handle resource recipe is
  explicit.

**Fit with issue #2.**
- The modified Stopping and Completed requirements carve out passive settlement observation on an
  existing handle, without reopening any other live operation.
- After Completed, an observation does no DI, codec, user or engine work.
- Per-instance settlement is explicitly not permission to dispose the root provider.

**Process.**
- The OpenSpec guard admits only the exact SHA-pinned nine-pair registration. It keeps approvals null
  and semantic approval false, and requires 2.1 and 3.1–3.6 to stay open. That is not a blanket
  duplicate-owner waiver.
- The provenance pins are mechanical.
- The tasks are streamlined: contract, then one transition, then one source/consumer/native target.

**Lanes.** I did not re-run them; this is a contract-only target. The recorded OpenSpec 26/26, Release
0/0, Infrastructure 309/309 and ReSharper results are on this exact tree.

## F1 — P2 — The fiber delta's "provider records MAY carry type text" contradicts durable byte stability

**Problem.** The `structured-fiber-execution` delta says "Core/fiber/provider records MAY carry
detached type text". However:
- `workflow-contracts` requires durable-produced failures to use null, with existing durable
  payloads and bytes unchanged;
- the design says "Durable runtime failure production remains null-type/legacy and byte-compatible".

Persisting type text in durable fiber, checkpoint, outbox or provider records would change stored
durable bytes, and the shared Core reducer makes that easy to do by accident. The quality requirement
names only "durable unsupported/unchanged behavior", with no explicit byte witness.

**Required in the 2.1 transition** (before the blocks become canonical):
- Narrow the sentence. Ephemeral fiber records MAY carry detached type text. Durable fiber,
  checkpoint, outbox, projection and provider records SHALL NOT, and durable-persisted failure bytes
  SHALL remain byte-identical.
- Add a durable witness to the quality requirement. A thrown non-OrcaCore exception in a durable step
  must produce persisted failure bytes identical to base `0a8e375`, through InMemory and provider
  certification.

## P3 (optional)

- **P3-1 — Identity sentence.** Add one sentence to the provenance requirement: "`ExceptionType` is
  diagnostic text only; it is never protocol identity or a matching key, and `Code` remains the
  identity." This ties it verbatim to the unmodified canonical clause.
- **P3-2 — Rethrow guidance.** Several observers receive the same original exception object, and
  `throw inner;` resets that shared object's stack trace. The guide should recommend
  `ExceptionDispatchInfo.Capture(inner).Throw()`, or wrapping, as `Task` rethrows do.
- **P3-3 — Native watch item.** Assert in the six native cells that `ExceptionType` text equals the
  JIT value. Type names under trimming and AOT should be proven, not assumed.
- **P3-4 — I concur with the Codex review's P3.** Task 2.1 should state that the declaration
  companion changes at the canonical transition, while executable baselines change with source.
