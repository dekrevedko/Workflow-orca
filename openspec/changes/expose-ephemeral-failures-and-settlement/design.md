## Context

Issue #16 follows published preview.3. Its body inspected tag 6b4ab95 before
publication; the live base is synced master 0a8e375. Product/build inputs are the
same as the released source; later master changes are workflow/status fixes.
The primary X: checkout has separate DAG work and is outside this target.

Current output waiting pins the instance and captures a detached output/failure;
its operational exception has an internal constructor and already inherits
Exception.InnerException. Non-OrcaCore thrown steps are incorrectly wrapped with
WF-LEGACY-LIFECYCLE even though WF-STEP-UNHANDLED is already in the approved code
catalogue. WorkflowFailure has five immutable public properties and no type slot.
Its custom framework codec accepts exactly five JSON properties. Lifetime
WhenSettled is an internal epoch signal: owned work can reach zero while an
accepted workflow is still Waiting, and later work can create another epoch.
Public observation therefore cannot directly expose that Task.

## Goals / Non-Goals

**Goals:** expose an elected original step exception to an in-process ephemeral
output waiter; preserve detached type information without fake causes; let an
accepted instance prove its physical work has ended independently of output;
retain retention/shutdown/native/codec invariants with the exact small API.

**Non-Goals:** original exceptions from durable/storage/event paths, root-provider
or whole-host disposal permission from a single receipt, a global settlement
lookup, a settlement start-result helper, changing output/timeout semantics,
joining untracked application work/child processes, forced process termination,
exactly-once external effects, broad native badges, or source implementation here.

## Decisions

### 1. Two public additions, one existing exception carrier

Add the nullable getter WorkflowFailure.ExceptionType and the nonvirtual base
WorkflowInstanceHandle.WaitForSettlementAsync(CancellationToken=default).
All namespaces/assembly owners remain OrcaCore. No public constructor/setter,
exception type, registration option, timeout overload or new service is added.
The owner selected the explicit property in response to the contract question;
InnerException still carries the actual object. Its function differs from a
string in detached snapshots. Encoding a type in Causes was rejected because
Causes represent real execution failures. Reconstructing an exception from a type
name was rejected because it loses identity, changes behavior and widens reflection.

WorkflowOutputUnavailableException's internal construction can pass an original
to its already capable base constructor; the public CLR shape remains unchanged.
Durable inherited settlement calls fail as NotSupportedException without I/O.
A pre-cancelled observer token is checked first, consistently with existing handles.

### 2. Elect identity with the terminal failure

Capture the original at the admitted business-step boundary before wrapping or
retry selection. Here a business step means the named-step execution method
or authored ephemeral step delegate, not DI resolution/constructors, initialization,
merge/condition/selector callbacks, codecs or lifecycle callbacks. Those boundaries
do not gain a live original-exception promise from this change. Preserve user-thrown TargetInvocationException objects; unwrap
only a proven engine-owned invocation wrapper. Returned StepResult.Failed carries
the supplied OrcaCore error object. Select the original by actual failure/attempt
identity, not equal message/type/path/occurrence values. The terminal status,
detached failure and eligible original must be published coherently under the
existing single owning lane and facade/retirement visibility protocol.

Non-OrcaCore exceptions use existing WF-STEP-UNHANDLED; known OrcaCore codes remain.
Nonblank messages are copied verbatim without appended authored paths; if a
message is blank, use the stable detached fallback 'The step failed without an
exception message.' to preserve WorkflowFailure's existing nonblank invariant.
The original object remains unchanged and is always accessible through its inner
reference when eligible. Type text is GetType().FullName with Name fallback;
it is opaque text and never resolved or deserialized as a CLR type.

An outer output-unavailable wrapper is newly constructed per observation, with
unchanged outer code/status/Failure properties. Its InnerException is the same
original object, not an ExceptionDispatchInfo, internal normalization wrapper,
recreated exception or rethrow. The engine does not reset the original stack.
A caller may later rethrow via ExceptionDispatchInfo if desired; that is caller work.

### 3. Single causes preserve; true aggregates remain detached

A sole failed root step, propagated fixed branch or item has its original live
reference. A genuine SFE-JOIN-FAILED aggregate gets no invented AggregateException;
its ordered detached causes carry each leaf's type and provenance. This avoids
pretending a newly made aggregate was the original business exception. A
WhenAllOutcomes recovery receives only detached failures and releases obsolete
original associations when they no longer contribute to a terminal winner.
Synthetic/lifecycle/non-step failures have no original reference under this API.

Only the exhausted final retry is elected. A timeout/cancel/terminate winner has
no late business InnerException; a losing attempt cannot rewrite failure data.
An unrelated OperationCanceledException counts only if existing runtime policy
elects it as a Failed business step; governed cancellation remains governed
cancellation. Local output/settlement-wait cancellation never becomes workflow data.

### 4. Small live reference ownership, not serialization

Live exceptions live only in the ephemeral instance/attempt ownership domain.
Core/provider/fiber records may carry detached type strings but never Exception,
ExceptionDispatchInfo, Data graphs or delegate state. Snapshot/branch/item failure
models and state/output values remain fixed-codec-detached. The narrow live
InnerException exception to the detached-inspection rule is explicit: user
exceptions can themselves reference application objects, so their graph is not a
disposal guarantee or a detached state model. The engine adds no internal/provider
objects to it and performs no eager ToString/Data traversal or arbitrary reflection.

An admitted output waiter captures the original together with the elected detached
failure while its existing pin is held. Retirement clears engine references with
other payloads. A previously delivered outer/inner remains caller-owned. Weak-
reference tests must drop caller references and avoid definitions capturing the
exception themselves. Retention-off can retain failures for host lifetime, as it
already retains payloads; no new hidden unbounded global cache is permitted.

### 5. Closed failure-subformat amendment

Null ExceptionType writes the exact existing five-property object, with no added
null/version field. New readers read that legacy object as ExceptionType=null.
Non-null type writes the seven-property object in order: version (integer 2),
code, message, authoredLocation, occurrence, exceptionType, causes. It must have
exactly those unique names. The type string must be nonblank; version/type must
appear together. Duplicate, missing, extra, null/blank/wrong-type fields, unknown
versions and invalid existing occurrence forms reject through the existing
malformed-codec boundary. Causes recursively accept either closed form.

The existing root/branch/item occurrence discriminator remains version 1. The
overall codec ID remains orcacore-json-v1; this proposal explicitly amends its
certified framework failure subformat. This is not a claim of old-binary reading
new typed failures: preview.3 and older exact-five readers reject the new form.
Rollout must use a new package version with a matched selected package closure.
New readers accept old payloads; null-type and unrelated values retain their exact
bytes/fingerprints. Durable runtime failure production remains null-type/legacy
and byte-compatible; a detached typed failure supplied as application data can use
the shared new reader without carrying an original object. No provider schema,
serializer hook, reflection switch or additional converter catalogue is introduced.

### 6. A monotonic receipt, not a payload or host pin

An accepted instance gets one small identity-bound receipt. It transitions once
to success only when terminal and all physical attempts, cancellation handlers,
queued callbacks, pending commits and final ownership continuations have finished.
A temporary owned==0 while Waiting is not settlement. Internal epoch tasks require
terminal/recheck/final-handoff coordination; they cannot be exposed directly.
Once the receipt succeeds, later stale producer trampolines must reject before
creating owned work, dependency use or commits. Failure in unrelated retirement
cleanup/diagnostics does not undo already proven physical quiescence.

Settlement observers subscribe before authoritative recheck, never poll, and
hold no physical owner, payload pin or host join obligation. Local cancellation
removes only the local observer and does not cancel the receipt/work or affect
other waiters. Query pins and retirement cleanup are outside its success condition;
it uses physical settlement, not the stronger 'eligible for retirement' predicate.
An observer must not make StopAsync wait on itself or block count/age retirement.

A handle retains only the small receipt, not removed payloads, original exceptions,
services or a new global ID tombstone. A held original handle can return successful
settlement after retirement; GetInstanceAsync by the retired ID and ordinary
stale operations still fail with the original not-found ID. A new run under an
expired start key has a different ID/receipt and is never addressed by the old one.

### 7. Lifecycle and precedence

| Situation | Settlement method on existing handle |
|---|---|
| pre-cancelled caller token | local OperationCanceledException before mode/proof checks |
| durable, valid token | NotSupportedException without provider or DI access |
| ephemeral Running/Waiting | pending until terminal final physical quiescence |
| Closed | observation allowed; accepted work drains normally |
| Stopping | passive observation allowed, no new owned obligation |
| already terminal but physical work remains | pending despite output availability/unavailability |
| original instance retired | success from its receipt, no lookup or resurrection |
| Completed and correct owner disposal | success from completed receipt; no DI/codec/user/engine work |

Cancellation racing final success may resolve either observer outcome once under
normal token/task race rules; it never changes the underlying proof. Completed
fast-path observation registers no timer or cancellation callback. Ordinary output
wait/query/command/ingress behavior after Stopping remains unchanged. Per-instance
settlement does not permit disposing a shared/root DI provider: use host-wide
StopAsync Completed for that. Caller continuations are caller work, not engine work.

### 8. Honest resource recipe, including no-handle starts

For an Accepted start, capture the handle and keep resources through output success,
failure or local wait cancellation, then await settlement with an owner-controlled
noncancelled cleanup token before releasing resources used by that instance.
If cleanup observation is cancelled or never completes, retain those resources.
An arbitrary UI request token is not a resource-disposal permission.
Settlement is observed from outside the instance's still-owned body/callback.
Awaiting one's own settlement from that body prevents its return and cannot satisfy
the receipt condition. Step-owned cleanup releases its own resources directly;
it does not await its own receipt. Arbitrary application wait-cycle detection is
outside this method, just as a self-referential output wait is not a scheduler join.

A failed/cancelled unaccepted start has no handle and cannot use this method.
The application must use step-owned cleanup, its own physical-work owner, or a
successful host-wide join; it cannot infer physical return from the start error.
A noncancelled start token alone does not prevent a host Stopping abort. Steps must
await their external process/I/O/read-copy tasks before returning; settlement does
not discover fire-and-forget FFmpeg processes. The consumer experiment must prove
that distinction without requiring an FFmpeg installation.

### 9. Exact proposed ownership without modifying old approvals

Nine MODIFIED operations share headings with current active predecessors. The
contract includes a separate nine-row proposed-owner registration, with exact
predecessor/canonical/successor block hashes and null approval fields. The existing
sole-owner guard accepts only those source-pinned pairs, validates both owners,
keeps canonical/source tasks open and still classifies every successor as pending.
No blanket duplicate waiver, global registry/schema rewrite or old approval edit
is permitted. Three ADDED operations retain ordinary sole ownership. Canonical
transition task 2.1 must update this staged process record under real approval.

## Risks / Trade-offs

- Live Exception graphs can retain application resources -> retain only elected
  originals, clear them on retirement, never serialize them, and keep the opt-in
  retention limit/resource recipe explicit.
- A common base method is callable on durable handles -> reject early without I/O;
  no promise of durable physical settlement is inferred.
- An optional type getter changes failure wire when present -> exact v2 negatives,
  exact old null-type bytes, cross-mode codec tests, matched-version rollout and
  no new-to-old compatibility claim.
- A raw lifetime epoch can signal while nonterminal -> monotonic terminal+physical
  receipt and races with ownership handoff tested under barriers in both policy modes.
- A pre-handle abort cannot expose a per-handle proof -> preserve host ownership and
  explicitly document safe external/step-owned cleanup rather than invent another API.

## Migration Plan

1. Review/approve this uncommitted contract and checkpoint it with a verdict-only child.
2. Prepare and independently review canonical/numbered/guide/declaration transition,
   including precise v2 failure compatibility and all conflicting shutdown/not-found rules.
3. On that approved checkpoint, build one coherent tests/source/baselines/native/consumer
   target. Run ReSharper and full green lanes; keep it uncommitted for review.
4. After approval checkpoint, publish the issue branch/PR only under its named gate,
   merge with a merge commit after CI, and choose any future release separately.

## Open Questions

No API design choice is deferred to implementation. Owner approval of this contract,
including the explicit failure-subformat amendment, remains required. No runtime,
canonical, source-support or publication approval is inferred from green validation.
