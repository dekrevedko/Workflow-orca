## MODIFIED Requirements

### Requirement: Durable DAG progression is runtime owned
The durable DAG runtime SHALL reconstruct node readiness from committed DAG state, evaluate each opaque `MapInput` only after all declared direct dependencies succeed, commit each fixed-codec node input once from immutable run input plus successful direct dependency outputs, start or reattach one durable child workflow instance per node occurrence, and continue after child outcomes without caller-owned ready/completed sets. `Build` SHALL reject only inspectable structural faults, including a missing or duplicate `MapInput`; it SHALL NOT claim to discover an undeclared/non-direct `OutputOf` call hidden in delegate code. Mapper access to an undeclared, non-direct, resultless, foreign-plan, or otherwise invalid output SHALL fail deterministically as `DAG_INPUT_MAPPING_INVALID` during mapping evaluation before node-input commit or child start. The node becomes failed, its dependants become dependency-blocked as applicable, and independent nodes remain valid and continue. The runtime SHALL support independent nodes whose mapper uses only immutable run input, resultless dependency-only nodes, and resultful output-bearing nodes. Every node/snapshot SHALL use zero-based authored order. `MaxConcurrentNodes` SHALL count every started nonterminal child node, including a child parked in a workflow wait, until that child becomes terminal. Child failed/timed-out/terminated SHALL map to stable `CHILD_FAILED`/`CHILD_TIMED_OUT`/`CHILD_TERMINATED`; child cancellation without a DAG-run cancellation request SHALL map to `CHILD_CANCELLED` failure. Without a run cancellation request, the run SHALL fail on any non-success child and succeed only when all nodes succeed.

#### Scenario: Host restarts during a DAG run
- **WHEN** a DAG resumes after one node child was started before host failure
- **THEN** runtime reconstruction reattaches the deterministic child identity and does not duplicate the node occurrence

#### Scenario: Required dependency fails
- **WHEN** one node's required dependency reaches failure
- **THEN** its input mapping is not invoked, it and its dependant chain become dependency-blocked as applicable, and independent ready/running nodes continue until no node can progress

#### Scenario: Opaque mapper reads an undeclared output
- **WHEN** `MapInput` calls `OutputOf` for an undeclared or non-direct dependency
- **THEN** mapping evaluation fails with `DAG_INPUT_MAPPING_INVALID` before input commit or child start, the node fails deterministically, its dependants block, and independent nodes continue

#### Scenario: Independent node maps only run input
- **WHEN** a node declares no dependencies and its one mapper uses only immutable DAG run input
- **THEN** `Build` accepts the structurally complete node and runtime mapping may commit its input and start it normally

#### Scenario: Started child is parked
- **WHEN** a started child workflow is nonterminal but parked in a wait
- **THEN** it continues to consume one `MaxConcurrentNodes` admission until terminal and another eligible node remains unadmitted when the cap is full

#### Scenario: DAG run terminalizes after mixed outcomes
- **WHEN** no node can progress after successes, failures, cancellations, and dependency blocking
- **THEN** the terminal result lists every node once in stable node order with its final succeeded, failed, cancelled, or dependency-blocked status

#### Scenario: DAG run cancellation is requested
- **WHEN** a caller requests cancellation through `DagRunHandle`
- **THEN** new admission stops, pending/ready nodes become cancelled, running nodes become cancellation-requested and propagate to children, dependency-blocked nodes remain blocked, and the run becomes cancelled only after running children are terminal

#### Scenario: DAG hosting starts a child
- **WHEN** `OrcaCore.Dag.Hosting` progresses a ready node
- **THEN** it uses the named versioned internal child-start/join bridge exposed only by `OrcaCore.Durable.Hosting` through the single friend declaration, with no public child member

The complete DAG run lifecycle SHALL include provider-authoritative create/reopen and immutable run-input/graph persistence, fenced progression/recovery, ready notification, atomic mapping failure and transitive dependency blocking, node-input admission, child-terminal join, run cancellation, fault/poison reconciliation and notification-driven run-change/terminal observation. The durable bridge SHALL eagerly decode every successful declared resultful dependency, including unused outputs, to its approved declared type before handing the exact dependency map to Dag.Hosting's evaluator. Decode failure SHALL prevent mapper invocation and input/start commit through a named durable bridge failure. Only the current fenced run-progression owner SHALL admit nodes. On restart or ownership change it SHALL reapply its current validated host-owned `MaxConcurrentNodes` ceiling rather than treating a creation-time stored ceiling as authority. Already admitted parked children and committed start intents SHALL retain their slots until authoritative terminal reconciliation; lowering the limit SHALL stop new admission without cancelling or forgetting existing admissions. A full admission window SHALL return `CapacityDeferred`, leave the node ready with no input/start commit, discard any speculative mapped value and wait for a durable capacity/ownership notification. `AdmissionStopped` SHALL instead denote a cancellation/terminal admission barrier. Child identity SHALL be deterministic from complete run/node/workflow coordinates, exact-bound on collision, and separate from caller start-key reuse.

#### Scenario: Replacement owner lowers its host limit
- **WHEN** an owner recovers a run whose admitted children exceed its current validated ceiling
- **THEN** it keeps every prior admission counted, admits no new node until capacity permits and never reuses the obsolete creation-time ceiling as authority

#### Scenario: Ready node encounters full capacity
- **WHEN** a fenced owner evaluates admission at a full ceiling
- **THEN** CapacityDeferred is nonterminal, no input/start record commits, and reevaluation waits for a capacity/ownership change rather than cancelling the node or hot-looping claims

#### Scenario: Declared but unused dependency cannot decode
- **WHEN** a successful declared resultful dependency cannot be decoded to the approved declared type
- **THEN** the bridge commits the named failure/blocking transition without invoking the mapper, even if its delegate would not read that output

#### Scenario: Stale owner attempts admission
- **WHEN** a former owner tries to append an admission after a fenced takeover
- **THEN** the provider rejects its stale fence and no competing host can independently admit under a different local ceiling
