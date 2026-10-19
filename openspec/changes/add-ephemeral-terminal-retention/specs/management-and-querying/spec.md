## ADDED Requirements

### Requirement: Ephemeral terminal retention requires physical settlement
Automatic ephemeral terminal retention SHALL protect every accepted active, waiting, delayed, or cancellation-requested instance and every terminal instance with runtime-owned physical attempts, callbacks, pending commits, continuations, or cleanup still unsettled. The runtime SHALL register per-instance root/branch/item physical ownership before dispatch and remove it on definitive completion, independently of whether throttles or transient pools are configured. Terminal status alone, elapsed time, or a free logical path token SHALL NOT be settlement proof. Retirement SHALL NOT cancel work, release a physical governance slot early, change immutable terminal status, or claim application-spawned external effects stopped.

An unaccepted provisional start SHALL remain runtime-owned until either atomic facade acceptance or definitive abort. Caller cancellation of `StartOrGetAsync` observed after runtime creation and before acceptance, or an exception after `Init` before acceptance, SHALL abort that provisional unit. Before surfacing that cancellation/exception, abort handling SHALL fence the unit through its owning mutation lane to `Terminated`, or preserve an already committed immutable terminal status, using cleanup authority independent of the cancelled caller token. The unit SHALL have no published facade binding, start-key binding, or reachable application handle, SHALL remain outside the accepted terminal-retention budget, and SHALL be released eagerly once every owned physical attempt/callback/commit has settled, even with `TerminalRetention` omitted. If acceptance wins the acceptance/abort race, the start SHALL return `Accepted`; subsequent cancellation of that start token SHALL NOT abort the accepted workflow. The caller-visible abort SHALL retain the original cancellation/exception. No cancellation/exception/not-found result SHALL imply that the workflow never ran or that retrying its unbound key is safe for external effects.

#### Scenario: Timeout fences an uncooperative attempt
- **WHEN** a runtime-owned task remains incomplete after the workflow becomes `TimedOut`, `Cancelled`, or `Terminated`
- **THEN** the runtime instance, facade binding, payload and start binding remain protected beyond age/count limits until physical settlement is established

#### Scenario: Active work is parked
- **WHEN** an instance is waiting on an event, delay, retry, governance admission, or a structured join
- **THEN** automatic terminal retention leaves its runtime state and key bound regardless of current memory pressure or elapsed age

#### Scenario: All owned work eventually settles
- **WHEN** terminal root/branch/item attempts and owned callbacks/commits finish
- **THEN** the published instance becomes eligible through the same lifetime protocol without a public eviction call or a claim about untracked external work

### Requirement: Ephemeral terminal retention retires one complete ownership unit
An enabled policy SHALL retire runtime instance/state/output/failure ownership, facade instance binding, exact start binding, and obsolete instance-owned routing/timer/notification/retention records together, including every entry for the instance in the internal event router's `accepted` event-deduplication map. It SHALL preserve definition registrations and SHALL NOT replace removed records with unbounded retired-ID or key tombstones. The host SHALL stamp monotonic settlement age once after both physical settlement and facade publication, plus a unique increasing settlement ordinal. At publication/settlement, final admitted-operation pin release, and each non-overlapping sweep, it SHALL first retire eligible units at or beyond maximum age, then retire eligible units by oldest settlement ordinal until their host-wide count is at most `MaxRetainedInstances`. The triggering lifetime operation SHALL enforce the count before completion. Reads and key replay SHALL NOT refresh age/order. Age retirement SHALL run during idle hosts on the first sweep at/after expiry, subject to scheduling delay. Active/unsettled and transiently pinned units SHALL be protected in addition to that eligible-terminal budget; after all work and admitted operations settle, owned per-instance payloads and metadata SHALL plateau at the configured retained count. The policy SHALL promise neither a minimum retention duration under count pressure nor a total byte/process-memory ceiling.

#### Scenario: Count pressure spans definitions
- **WHEN** more than the configured count of eligible terminal units exists across several registered definitions
- **THEN** the oldest settlement ordinals retire first and runtime/facade/start/payload records obey the same host-wide bound

#### Scenario: Idle age expiry follows settlement
- **WHEN** monotonic age reaches `MaxAge` with no new application calls
- **THEN** the next scheduled sweep retires the unit, while wall-clock changes and repeated reads/replays never extend its settlement age

#### Scenario: Workflow completes before start publication
- **WHEN** runtime execution completes synchronously before the facade has published its accepted instance/start binding
- **THEN** retention observes one coherent published unit and cannot leave a start binding or handle pointing at already removed runtime data

#### Scenario: A provisional start fails
- **WHEN** a start creates runtime-owned records but fails or is aborted before facade acceptance
- **THEN** the owning lane fences the unaccepted unit to `Terminated` unless it is already immutably terminal, no facade/start binding or handle is published, and the unit is released outside the retained budget immediately after physical settlement even when the policy is disabled

#### Scenario: Start cancellation occurs during the first step
- **WHEN** the start caller cancels its token after `Init` created the provisional runtime instance but before facade acceptance, while the first physical step is running
- **THEN** abort handling uses its own cleanup authority to terminally fence the unpublished unit, preserves the caller's cancellation result, protects the remaining physical work until it returns, and eagerly releases the unit after settlement instead of leaving an unreachable `Running` instance

#### Scenario: An exception escapes after Init
- **WHEN** an exception escapes a start after runtime creation but before acceptance
- **THEN** the original exception reaches the caller after the owning-lane terminal fence, existing terminal status is preserved if present, no key/handle is published, and all provisional runtime/router/timer payload records are released after physical settlement

#### Scenario: Accepted publication wins cancellation
- **WHEN** facade acceptance linearizes before the start-token cancellation can abort its provisional unit
- **THEN** the caller receives `Accepted`, its instance/key remain published, and the later start-token cancellation cannot substitute for the instance handle's explicit workflow-cancellation operation

### Requirement: Retired ephemeral handles have deterministic outcomes
Handles SHALL carry identity without extending retained payload lifetime. Operation admission and retirement SHALL share a linearization boundary. A snapshot/state/output query admitted before retirement SHALL pin a coherent unit until it captures a detached result. A pre-existing `WaitForOutputAsync` SHALL subscribe and authoritatively recheck under that protocol, retain an operation pin until detached successful output or terminal failure is captured, and settle without polling or a missed wakeup even if count/age pressure races notification. Completion/error/local caller cancellation SHALL release that pin exactly once; caller cancellation SHALL NOT cancel the workflow. Obtaining a handle alone SHALL NOT grant a pin to later calls. After retirement, `GetInstanceAsync` and every runtime operation on an existing handle SHALL throw existing `WorkflowInstanceNotFoundException`, code `WF-INSTANCE-NOT-FOUND`, carrying that handle's original ID; no replacement instance SHALL be targeted. Previously detached values and completed wait results SHALL remain caller-owned and valid. For a retained instance, existing definition/type mismatch behavior SHALL remain unchanged.

#### Scenario: A stale handle is reused after its key starts a new instance
- **WHEN** a unit has retired and a later start reuses its former key
- **THEN** all calls through the old handle fail with not-found for the old ID while the new handle addresses only the new runtime-minted ID

#### Scenario: Output notification races retirement
- **WHEN** count/age pressure selects a newly terminal instance with already admitted output waiters
- **THEN** each waiter captures its detached output or typed terminal failure before pin release and no waiter hangs or becomes spuriously not-found

#### Scenario: Caller cancels one output wait
- **WHEN** local cancellation wins for one admitted waiter
- **THEN** only its subscription/pin is released and the workflow and other waiters remain unchanged

#### Scenario: Read and retirement interleave
- **WHEN** a query races the retirement visibility boundary
- **THEN** it either completes from a pinned detached value or returns typed not-found, with no partially removed state or live payload reference

#### Scenario: Count pressure retires a result between start return and observation
- **WHEN** `StartOrGetAsync` has returned `Accepted` for a completed unit and enough other units settle to retire it by count before the caller's direct or start-result-helper `WaitForOutputAsync` is admitted
- **THEN** the wait throws `WorkflowInstanceNotFoundException` for the original ID even though that job ran and may have produced output or side effects; replay of its now-unbound key creates a fresh ID with `WasExisting = false` and may repeat side effects, so not-found after acceptance SHALL NOT be interpreted as proof the run never started or as permission for an effect-safe retry

### Requirement: Ephemeral start idempotency expires with atomic retirement
While an ephemeral unit is retained, protected, or pinned, its exact start key SHALL retain the current definition identity/version/fingerprint and fixed-codec input fingerprint binding. Compatible replay SHALL return the same instance with `WasExisting = true`; incompatible replay SHALL return the existing typed conflict and perform no start. Actual retirement SHALL remove the unit and key in the same public visibility transition. Elapsed maximum age alone SHALL NOT unbind a key before retirement. After retirement, the unbound key SHALL accept another valid registered definition/input as a new start with a fresh runtime-minted ID and `WasExisting = false`. Concurrent replay, new start, completion and retirement SHALL serialize at that transition so no bound key points to absent runtime data and no current key admits two new instances. Host-local deduplication after retirement and physical external-effect exactly-once behavior SHALL NOT be promised.

#### Scenario: Duplicate arrives within the retained set
- **WHEN** a retained/protected unit's exact key and input are replayed
- **THEN** the original instance is returned and its retention age/order is unchanged; changed definition/input yields the existing closed conflict

#### Scenario: Former key is reused after retirement
- **WHEN** a valid start uses a key whose old unit was atomically retired
- **THEN** the runtime creates one fresh instance with `WasExisting = false` and old handles remain not-found

#### Scenario: Replay races retirement
- **WHEN** a duplicate start and retirement contend for the same unit
- **THEN** replay either observes the complete retained binding or starts after complete removal; it never reopens missing runtime data or forgets an unsettled binding
