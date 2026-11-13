# Issue #16 scenario and evidence inventory

These are planned source/consumer controls, not implemented runtime tests. Every
row must be individually mapped to assertions; no weakened expected outcome is
accepted. Both retention-off and bounded-count modes apply to every owning-runtime
row. Time synchronization uses explicit barriers/fake clocks rather than sleeps.

| ID | Trigger | Required proof |
|---|---|---|
| FS-01 | Thrown root IOException / InvalidDataException | Exact InnerException identity/type/message/stack; WF-STEP-UNHANDLED; real authored/occurrence provenance |
| FS-02 | OrcaCore throw and explicit StepResult.Failed | Original supplied object, stable existing code, captured type; no normalization wrapper exposed |
| FS-03 | Engine invocation wrapper vs user-thrown TargetInvocationException | Unwrap only an owned engine wrapper; preserve a user-thrown wrapper itself; DI/merge/codec boundaries gain no live original promise |
| FS-04 | Retry final attempt, equal errors and repeated sites | Only the actual final failure object; equal text/path never aliases an earlier attempt |
| FS-05 | Root/branch/item timeout, cancel, terminate and late exception | Logical output finishes earlier; late error cannot overwrite elected failure/original slot |
| FS-06 | Single branch/item propagation and true aggregate | Single original retained; SFE-JOIN-FAILED has no invented live inner; real causes keep authored/item order and type |
| FS-07 | WhenAllOutcomes successful recovery | Only detached failure data crosses merge; obsolete originals released |
| FS-08 | Null-type legacy and typed v2 codec | Exact old five-field bytes; exact ordered seven-field v2, mixed recursive graphs; no exception serialization/type resolution |
| FS-09 | Malformed wire and message edge controls | Unknown/duplicate/missing/wrong-type/version/type/provenance reject; nonblank messages verbatim and blank fallback exact |
| FS-10 | Terminal/physical separation under retention off and bounded count | Output timeout/termination does not settle a blocked root/branch/item body or callback |
| FS-11 | Nonterminal idle zero and terminal handoff | Event/delay/retry/lease/commit continuations cannot give premature or reversible success |
| FS-12 | Multiple observers and local cancellation | One observer cancels without workflow/other-receipt cancellation, payload pin or host owner |
| FS-13 | Subscribe/recheck and final notification race | No polling, no missed wakeup and exactly one observer completion |
| FS-14 | Count/age retirement and expired-key replacement | Old receipt succeeds without payload/tombstone retention; normal old operations remain original-ID not-found |
| FS-15 | Original reference retirement/release | Engine releases originals; caller-held wrapper remains valid; weak-reference test avoids a definition/caller capture |
| FS-16 | Closed/Stopping/Completed and correct owner disposal | Draining allowed; passive receipt never blocks host join, invokes dependencies or creates work; completed fast path passive |
| FS-17 | Provisional cancellation/exception after Init, in both policy modes | No invented handle or per-start receipt; abort remains terminal-fenced and host-owned until physical return |
| FS-18 | Resultless/resultful, pre-cancel and durable | Inherited one method; token first; NotSupported with zero provider/DI calls; existing durable output/failure unchanged |
| FS-19 | Fresh public-package application resource owner | Keep resources through output/local cancellation; release after receipt; retain without handle unless step/external/host owner proves settlement |
| FS-20 | Six native profiles with unchanged old journeys plus new witnesses | Actual jobs and aggregate run, package/source identities agree, legacy/v2/type/original/settlement controls succeed, no private rooting or reflection switch |

Cancellation-vs-completion races accept one valid local observer result, never a
changed workflow result. Failure type text and original reference are tested
separately. A legacy reader rejecting v2 is an expected compatibility boundary,
not a product regression to hide. External unjoined child processes and arbitrary
client queries are outside per-instance physical proof; the resource recipe must
state and demonstrate that boundary.
