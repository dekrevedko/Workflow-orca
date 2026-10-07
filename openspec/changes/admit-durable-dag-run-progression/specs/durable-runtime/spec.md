## MODIFIED Requirements

### Requirement: Durable DAG progression is runtime owned
The durable DAG runtime SHALL reconstruct node readiness from committed DAG state, evaluate each opaque `MapInput` only after all declared direct dependencies succeed, commit each fixed-codec node input once from immutable run input plus successful direct dependency outputs, start or reattach one durable child workflow instance per node occurrence, and continue after child outcomes without caller-owned ready/completed sets. `Build` SHALL reject only inspectable structural faults, including a missing or duplicate `MapInput`; it SHALL NOT claim to discover an undeclared/non-direct `OutputOf` call hidden in delegate code. Mapper access to an undeclared, non-direct, resultless, foreign-plan, or otherwise invalid output SHALL fail deterministically as `DAG_INPUT_MAPPING_INVALID` during mapping evaluation before node-input commit or child start. The node becomes failed, its dependants become dependency-blocked as applicable, and independent nodes remain valid and continue. The runtime SHALL support independent nodes whose mapper uses only immutable run input, resultless dependency-only nodes, and resultful output-bearing nodes. Every node/snapshot SHALL use zero-based authored order. `MaxConcurrentNodes` SHALL count every started nonterminal child node, including a child parked in a workflow wait, until that child becomes terminal. Child failed/timed-out/terminated SHALL map to stable `CHILD_FAILED`/`CHILD_TIMED_OUT`/`CHILD_TERMINATED`; child cancellation without a DAG-run cancellation request SHALL map to `CHILD_CANCELLED` failure. Without a run cancellation request, the run SHALL fail on any non-success child and succeed only when all nodes succeed.

The runtime SHALL create/reopen immutable graph/run input, discover/recover nonterminal runs, serialize all progression under a live fence/expected version, record mapping failure with transitive DependencyBlocked closure, join authoritative child terminal facts once, and cancel/terminalize without caller pumps. Ordinary declared-type decoding of ANY successful direct resultful output, including unused outputs, SHALL fail as DAG_INPUT_MAPPING_INVALID before mapper/input/start; protocol/storage failures SHALL retain runtime classifications. Child identity SHALL hash ONLY run GUID and ordinal by SHA-256 of UTF-8 orcacore-dag-child-v1\n<lowercase-D run GUID>\n<invariant ordinal>\n; its first 32 hex characters form Guid N (all-zero last digit becomes 1). Workflow/version/fingerprint/types/input bytes SHALL bind SEPARATELY on collision. The current fenced owner SHALL reapply its host-owned MaxConcurrentNodes per run after restart. StartAuthorized and all started nonterminal children, including parked children, SHALL retain admission until authoritative join. A full ceiling SHALL return CapacityDeferred with no input/start commit and wait for capacity/ownership notification; AdmissionStopped SHALL mean cancellation/terminal barrier only. Lower takeover limits SHALL NOT cancel/forget prior admissions. The six-call child result union SHALL add CapacityDeferred and delegate admission to this fenced coordinator, never become a second unfenced lane. Durable.Hosting SHALL define the internal ready sink; Dag.Hosting SHALL implement it/evaluate mappers, with no reverse edge, codec access or new friend. Run-change/terminal waits SHALL subscribe/recheck, never poll. Integrity faults SHALL stop admission visibly without claiming physical work stopped; only authoritative reconciliation SHALL release uncertain slots.

Dag.Hosting SHALL own the admission/mapping coordinator; Durable.Hosting SHALL own the lease broker and every-host join/cancellation coordinator, using the same provider FIFO. Dag.Hosting SHALL obtain/release/renew its turn only through the exact internal run bridge, never call a provider port or dispatch admission to a remote coordinator. Each granted admission turn SHALL apply its local host-owned ceiling; successive hosts may use different ceilings but SHALL NOT admit concurrently. A granted turn SHALL release after at most one atomic mutation and before every idle/capacity/child wait. Renewal SHALL NOT extend its absolute 15-second deadline; stale append SHALL be rejected; all eager decode, mapper evaluation and normalization SHALL occur outside any turn with no mapper time budget. Physical evaluations SHALL use distinct internal DagEvaluationParallelismPerRun=1 isolated run lanes, NEVER a host-global MaxConcurrentNodes pool. Lightweight dispatch SHALL be round-robin without waiting on other run mappers; a stuck/cancelled mapper SHALL retain only its own dedicated lane until return, with no overlapping replacement. Completed immutable results SHALL survive unrelated version conflicts/capacity deferral and commit under a fresh short turn after binding/readiness/barrier/capacity recheck. New admission requests SHALL NOT overtake earlier joins/cancellation; under healthy storage/scheduling, K prior live turns bound progress by (K+1)*15 seconds plus bounded service delays, with at most 30 seconds per dead waiter. The captured-turn child bridge SHALL NOT borrow a newer fence. Public cancellation SHALL originate through DagRunHandle on a DAG-role host; every durable host SHALL consume committed child-cancellation/join work. Same-host purposes SHALL use one arrival FIFO before provider FIFO, with OwnerBusy/LocalQueueFull explicit outcomes. OpenChildBridgeForTurn SHALL capture the sole explicit validated host-limit argument. Persisted nodes SHALL use StartAuthorized, not Running, and project it as public Running with logical admission StartedAt.

MaxConcurrentNodes SHALL remain solely the host-owned PER-RUN ceiling on admitted nonterminal child/start intents. Evaluation SHALL select only the lowest Ready authored ordinal within F=max(0,current local ceiling-admitted children) and SHALL start none at F=0. There SHALL be zero look-ahead and at most one evaluation OR retained receipt plus one coalesced admission request per run/host. Successful admission of n SHALL recheck under the fence F>0, lower Ready count<F and lower Ready count=0; otherwise CapacityDeferred SHALL commit no input/start. Mapping failure SHALL need no child slot. A newly Ready lower ordinal SHALL invalidate one higher speculative receipt to preserve single-host progress, but elapsed time, unrelated version conflict and full capacity SHALL NOT cause reevaluation. Cross-host duplicate evaluation SHALL be explicit and bounded one/run/boot host, not exactly-once mapper invocation. Non-success child joins SHALL transitively block every reachable Pending/Ready descendant in the same append; success alone readies eligible dependants. Unaddressed ready hints SHALL acknowledge validated/coalesced local wake acceptance before evaluation, hold no claim across mapping and recover acknowledged/lost/poisoned hints through continuous wrapping nonterminal-run discovery with at most five seconds between full passes. DG-P22–24 SHALL certify authored ties on one/two hosts, cross-run slow/stuck isolation and bounded receipts, and ready-hint/host-death recovery on real production storage.

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

#### Scenario: Remote join releases admission during slow out-of-turn mapping
- **WHEN** a different durable host queues an authoritative child join while slow mapping runs outside ownership
- **THEN** the owner releases or expires within its hard deadline and FIFO grants the join without being overtaken by new admissions

#### Scenario: Another DAG host requests cancellation
- **WHEN** another DAG-role host uses its public DagRunHandle while a mapper evaluates outside any turn
- **THEN** the fair cancellation turn commits the admission barrier and all late stale mapping results are rejected

#### Scenario: Full ceiling is capacity-deferred rather than stopped
- **WHEN** an eligible node encounters a full host-owned window including parked and StartAuthorized children
- **THEN** no mapped input/start commits and the turn releases before a capacity notification wait, returning CapacityDeferred not AdmissionStopped

#### Scenario: Unused declared output cannot skip decode validation
- **WHEN** an unused successful direct resultful dependency cannot decode as its declared type
- **THEN** DAG_INPUT_MAPPING_INVALID is recorded before mapper/input/start and descendants block while independent branches continue

#### Scenario: Successful null input is present bytes
- **WHEN** a legal nullable mapper returns null
- **THEN** the bridge persists input presence plus the four UTF-8 null bytes and committed-byte fingerprint


#### Scenario: Slow valid mapper still commits
- **WHEN** valid mapping takes longer than fifteen seconds and unrelated joins change the run version
- **THEN** one in-flight evaluation completes, its immutable result survives the unrelated changes and capacity deferral, and one short fenced commit admits it without rerunning the mapper

#### Scenario: Slow mapper cannot reorder a one-slot ready tie
- **WHEN** lower and higher authored nodes are Ready with one free child slot and the lower mapper is slow on competing hosts
- **THEN** only the lower ordinal may commit and the higher waits without wall-clock admission priority

#### Scenario: Cancelled stuck mapper does not exhaust another run
- **WHEN** one run retains a nonterminating evaluation after cancellation and another healthy run is Ready on the same host
- **THEN** the healthy run dispatches through its independent run lane while the stuck mapper retains only its own thread

#### Scenario: Acknowledged ready wake survives host death during evaluation
- **WHEN** a host acknowledges a validated ready wake and dies before mapped-input commit
- **THEN** another DAG host discovers the authoritative Ready node even without a surviving hint and evaluates it without polling a terminal waiter
