# Independent review D: OrcaCore v1 planning contract and Phase 0 guard readiness

**Review date:** 2026-07-19

**Review scope:** all 65 files named by the reviewer prompt

**Independence:** peer headline verdict/finding summaries were visible in the conversation, but this review was completed without opening the three other 2026-07-19 review artifacts or either supplied review attachment. Every finding below was re-derived and line-verified against the bounded input packet; peer artifacts remain reserved for the later consolidation pass.
**Output note:** the prompt's designated path and the `-b` and `-c` siblings were already occupied by parallel reviews, so this frozen independent result uses the collision-safe `-d` sibling. It is intended for later consolidation, not as an overwrite of a peer artifact.

The primary snapshot reviewed was:

- `docs/specs/17-selected-mode-capability-matrix.md` — SHA-256 `6A53E81FE39D6D5C3C73D78D8245CA4DB415479FC08E975A6E740141D053395E`;
- `docs/specs/17-public-authoring-contract.cs` — SHA-256 `B9917704409A333AD1B7BE48501C238B17CDCE7BB647B52F5AF940E1AB5A5844`;
- reshape `tasks.md` — SHA-256 `91FFBFC0E64C4BF8F1F7BFBC4D5B75C7E7F349BB81EAB011A2B3A05076286061`.

## 1. Verdicts

| Gate | Verdict | Reason |
|---|---|---|
| Planning contract | **REJECT** | The direction is strong, but the approved exact surface contains two silently unsafe C# paths, durable lease state transitions conflict with retry, DAG build validation is impossible through the declared opaque mapper, and several required operational/package contracts have no exact seam. These are not editorial fixes. |
| Guard-retarget readiness | **NOT READY** | A guard-only agent would have to freeze unsafe signatures or invent mapping, failure, package, resize-conflict, recovery-query, ephemeral-event ownership, and deterministic governance-test contracts. Tasks 3.1-3.4, 3.7-3.8, 3.10-3.11d, and therefore 3.12 are blocked or only partially draftable. |

No P0 was found. Eleven P1 findings block approval. The smallest safe next step is a planning-only reconciliation; product source and Phase 0 guards should remain untouched until it is reviewed.

## 2. Severity-ordered findings

### P1-1 — A leased nested `Parallel` join returns an unrestricted builder and re-enables nested acquisition

**Evidence.** The contract says nested authoring retains an enclosing lease restriction (`docs/specs/17-selected-mode-capability-matrix.md:532-539`), leased builders omit `AcquireResources` (`docs/specs/17-selected-mode-capability-matrix.md:843-861`), and the exact companion is normative (`openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md:219-224`). `DurableLeaseNestedBuilder.Parallel` correctly returns `DurableLeaseNestedParallelJoinBuilder` (`docs/specs/17-public-authoring-contract.cs:640-675`), but both join methods return unrestricted `DurableNestedBuilder<TInput,TState>` (`docs/specs/17-public-authoring-contract.cs:1044-1056`). That returned type exposes both `AcquireResources` overloads (`docs/specs/17-public-authoring-contract.cs:342-387`). The other leased join families correctly return their leased parents (`docs/specs/17-public-authoring-contract.cs:1014-1026,1074-1089,1107-1119`).

**Impact.** This chain compiles and silently admits a descendant acquisition while an ancestor lease is live:

```csharp
leased.If(_ => true, nested =>
    nested.Parallel<int>(_ => { })
          .WhenAll((state, results) => state.Value)
          .AcquireResources(secondRequest, _ => { }));
```

That violates the inclusive ancestry invariant and can deadlock or double-reserve external capacity. Under the prompt's rule, a silently accepted lease-capacity misuse is at least P1.

**Smallest remediation.** Change both return types at companion lines 1048 and 1053 to `DurableLeaseNestedBuilder<TInput,TState>`. Add a negative compile fixture for the complete chained path to tasks 3.4 and 3.11a.

### P1-2 — The one-parameter ephemeral async lambda compiles as `async void`

**Evidence.** Every ephemeral context exposes `Then(Action<StepContext<TState>>)` and only a **two-parameter** async alternative `Then(Func<StepContext<TState>,CancellationToken,ValueTask>)`: root at `docs/specs/17-public-authoring-contract.cs:153-161`, nested at `:298-306`, branch at `:394-402`, and item at `:447-455`. The contract promises synchronous and asynchronous lambda business steps that execute as one step (`openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md:152-157`), while the matrix requires the runtime to own completion, deadlines, cancellation, and fenced state (`docs/specs/17-selected-mode-capability-matrix.md:553-593`).

**Impact.** `Then(async ctx => await WorkAsync(ctx))` has only the one-argument `Action` target and therefore compiles as `async void`. The delegate returns at its first incomplete await; the engine may commit/advance and release its transient slot while work is still running, and a later exception cannot become a step failure. This affects state, external effects, deadlines, and capacity.

**Smallest remediation.** Remove the void-return business-step delegate. Use `Func<StepContext<TState>,ValueTask>` for the one-parameter form and retain the cancellation-aware two-parameter form; synchronous bodies return `ValueTask.CompletedTask`. Add a source/reflection guard proving no public void-return delegate can accept an async step body.

### P1-3 — `MapInput(Func<...>)` cannot provide the promised build-time `OutputOf` validation

**Evidence.** DAG input mapping is an opaque `Func<DagNodeInputContext<TRunInput>,TNodeInput>` (`docs/specs/17-selected-mode-capability-matrix.md:943-960`), and `OutputOf` is a method on the runtime context (`:974-980`). Nevertheless `Build` must reject undeclared or non-direct `OutputOf` access (`:1135-1148`), and the Phase 0 scenario requires rejection before any run starts (`openspec/changes/reshape-developer-facing-interfaces/specs/quality-and-verification/spec.md:133-138`). Mapper code is intentionally outside structural fingerprint inspection (`docs/specs/17-selected-mode-capability-matrix.md:419-426,1135-1141`).

**Impact.** A mapper can branch on the real run input or captured state and call an undeclared same-typed reference only on one branch. No build-time probe or delegate inspection can soundly discover all calls. A guard cannot implement the required build diagnostic without inventing a structural mapping API; a runtime-only check would contradict the contract and delay failure until a run.

**Smallest remediation.** Either replace the delegate with an inspectable mapping expression/DSL, or explicitly change the guarantee to a deterministic mapper-invocation failure before child start. Revise task 3.10, tasks 8.2/8.3, and the quality scenario together. Wrong output type and resultless `OutputOf` remain compile-impossible through the generic reference types; the defect is same-typed undeclared/non-direct access.

### P1-4 — Retryable timeout/process loss conflicts with immediate lease quarantine

**Evidence.** A step timeout is retryable with the same `StepOperationId`, a higher attempt number, and a fresh attempt deadline (`docs/specs/17-selected-mode-capability-matrix.md:553-567`; `openspec/changes/reshape-developer-facing-interfaces/specs/durable-runtime/spec.md:134-158`). Late bodies may remain physically active while the retry starts (`durable-runtime/spec.md:149-151`). Yet any step timeout or process loss while protected work may exist is required to move the exact lease obligation to quarantine (`docs/specs/17-selected-mode-capability-matrix.md:863-869`; `durable-runtime/spec.md:160-167`; reshape `tasks.md:35`). A live held token is `NotConfirmable`, while a quarantined token is eligible for trusted release (`docs/specs/17-selected-mode-capability-matrix.md:895-903`).

**Impact.** On the first retryable timeout, the same lexical scope simultaneously needs its lease for the retry and exposes it as detached quarantine. The contract does not say whether the retry still owns the token, whether a reconciler may release it, or what happens when a successful retry reaches scope exit while the fenced prior attempt still runs. Ordinary task-3.9 retry/deadline guards remain possible, but lease-specific task 3.11b cannot encode both requirements.

**Smallest remediation.** Keep the obligation `Held`, `Marked`, or `Ambiguous` while its lexical owner remains retryable/recoverable. Transfer it to quarantine only when the scope is abandoned/terminal or tries to exit while protected work is not proven stopped. Specify retryable timeout, host-loss recovery, successful retry with a late prior body, and final exhaustion transitions explicitly.

### P1-5 — Quarantined lease obligations have no exact operator discovery path

**Evidence.** `IDurableResourceLeaseRecovery` accepts a token the caller already knows (`docs/specs/17-selected-mode-capability-matrix.md:827-833`). Pool management exposes only aggregate totals and the oldest review deadline (`:1306-1328`). The management delta says exact obligation detail exists in “advanced diagnostics” but declares no interface or DTO (`openspec/changes/reshape-developer-facing-interfaces/specs/management-and-querying/spec.md:88-108`). The scheduler requires operators to see pool, units, reconciliation age, protection identity, and confirmation outcome (`docs/specs/14-driving-scenario-eks-job-scheduler.md:169-182`). `IDurableResourceGovernanceStore` exposes encoded provider records, not an operator-facing trusted query (`docs/specs/17-selected-mode-capability-matrix.md:1359-1369,1460-1468`).

**Impact.** External labels can recover a token when a Job exists, but they do not cover crash-before-label/create, an absent resource, or an orphaned quarantine record. Capacity can remain reserved forever with no supported way to discover what must be reconciled. The required Kubernetes operator journey is incomplete, and task 3.11c has no public discovery-to-confirmation fixture.

**Smallest remediation.** Define one exact opt-in advanced obligation/reconciliation query projection, including token, state, owner occurrence, pools/units, review state, confirmation state, and authorization/redaction. Alternatively state that integration-owned storage is the sole discovery contract and remove the contrary operator/advanced-diagnostics requirements; that choice must be explicit.

### P1-6 — Phase 0 packed consumers have no normative package manifest

**Evidence.** Task 3.2 requires packed consumers for every application/hosting/provider/DAG/meta/companion role (`openspec/changes/reshape-developer-facing-interfaces/tasks.md:25`). The matrix gives a logical graph (`docs/specs/17-selected-mode-capability-matrix.md:1473-1496`) and exact core hosting methods (`:1498-1535`) but no PackageId/reference-set table. It calls `OrcaCore` both the application-contract dependency root (`:1478`) and a small meta-package (`:1494`) without defining whether those are one artifact. Production providers get only an “equivalently named” extension (`:1545-1547`). Package-ID assignment is deferred to implementation Phase 4 (`docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md:270-290`; reshape `tasks.md:89`). Supporting docs also differ between `AddOrcaCorePostgres(...)` style (`docs/implementation/01-solution-architecture.md:68-69`) and `AddOrcaCorePostgreSql(...)` (`docs/production-readiness.md:124-127`).

There is also no declared assembly owner for the exact core extension class. The matrix places all three methods on one CLR type, `OrcaCore.Hosting.OrcaCoreHostingServiceCollectionExtensions` (`matrix:1501-1515`), but the proposed projects put ephemeral hosting in `OrcaCore.Engine.Ephemeral`, durable hosting in `OrcaCore.Durable.Hosting`, and define no common hosting project (`docs/implementation/01-solution-architecture.md:6-25,43-51`). One class cannot be partial across assemblies. Owning it in either role forces an unwanted role/package closure; adding a common facade/hosting assembly is possible but would itself be an undeclared package-graph decision.

**Impact.** A guard author must invent PackageReference IDs, which package owns `Workflow`, which one owns the three hosting extensions, the meta-package contents, a local version/feed, and one production-provider registration signature. A packed-consumer baseline would accidentally become design authority.

**Smallest remediation.** Add a normative package manifest before task 3.2: PackageId, assembly/project, namespace/role, direct dependencies, meta-package contents, local version/feed convention, and one exact production-provider registration plus options. Assign each extension class to one assembly: split the ephemeral and durable methods across distinct concrete extension types, or explicitly approve a common hosting package and its dependency cost. Move PackageId assignment ahead of Phase 0.

### P1-7 — Task 3.11d requires facts and deterministic crash gates that are not declared

**Evidence.** Task 3.11d demands four-stage handoff crash recovery, ownership-equal facts, isolated restoration, contended conservation/direct transfer, and no ghost/double grant (`openspec/changes/reshape-developer-facing-interfaces/tasks.md:37`; `docs/specs/09-requirements-management-operations.md:344-375`; `docs/specs/12-acceptance-criteria.md:410-414`). The exact surface provides aggregate pool snapshots and opaque persisted records/store (`docs/specs/17-selected-mode-capability-matrix.md:1306-1369`). Runtime command/fact/checkpoint types are deferred to task 7.3 (`openspec/changes/reshape-developer-facing-interfaces/tasks.md:91`), and no deterministic fault barrier names the four handoff boundaries.

**Impact.** Aggregate totals cannot prove equality of instance/generation/fiber/scope/ticket/provider-generation ownership. Timing or process-kill tests cannot deterministically target each transition. A Phase 0 behavior guard must either use provisional internals or invent the future protocol and test harness.

**Smallest remediation.** Define a provider-certification/runtime test harness with immutable occurrence facts and named deterministic barriers at all four handoff transitions. Otherwise narrow 3.11d to the currently declared store/record architecture checks and move runtime conservation/crash behavior to the phase that defines the protocol.

### P1-8 — `ResizeAsync` requires an operation-ID conflict but cannot represent it

**Evidence.** `IDurableResourcePoolManagement.ResizeAsync` returns only `DurableResourcePoolSnapshot` (`docs/specs/17-selected-mode-capability-matrix.md:1314-1328`). Reusing a `ResourcePoolOperationId` for a different mutation “is a conflict” (`:1445-1448`; `docs/specs/09-requirements-management-operations.md:337-342`). No resize result or exception is declared. `ResourceGovernanceAppendResult.Conflict` is the provider expected-version append result, not the public operation-id conflict (`docs/specs/17-selected-mode-capability-matrix.md:1352-1357`).

**Impact.** A guard must invent throw-versus-result behavior, the exception/result type, and which existing/attempted facts are returned. Returning the old snapshot would silently accept changed intent; throwing an arbitrary exception is not an exact public contract.

**Smallest remediation.** Specify either a closed `Applied`/`Conflict` resize result or one exact typed conflict exception. Cover same-ID/same-mutation replay, same-ID/different-mutation no-op conflict, and the carried pool/capacity/operation facts in task 3.11d.

### P1-9 — The public failure contract references an undeclared base type and unspecified stable mapping

**Evidence.** `StepResult.Failed` requires `OrcaCoreException` (`docs/specs/17-selected-mode-capability-matrix.md:475-483`), and eleven declared exception types derive from it (`:291,485-528,809-815,999-1013,1646-1660`). Neither document 17 nor its exact companion declares `OrcaCoreException`; repository-wide search of the reviewed planning packet finds no declaration. The matrix nevertheless promises deterministic stable non-empty `WorkflowFailure.Code` mapping (`:602-610,710-715`). The companion header says referenced value/result/error types are defined by document 17 (`docs/specs/17-public-authoring-contract.cs:1-7`).

**Impact.** An application cannot know whether it may derive a domain failure, which constructor is accessible, or how an explicit failure becomes a stable code. Task 3.1 cannot create an exact public baseline, and a realistic `WhenAllOutcomes` rejection step must throw a provisional exception or invent a subtype contract.

**Smallest remediation.** Add the exact `OrcaCoreException` declaration and constructor/derivation policy plus the stable exception/`StepResult.Failed` to `WorkflowFailure` code rule. A closed author-created failure value is also viable, but it must be approved explicitly.

### P1-10 — Ephemeral event delivery has no declared hosting owner

**Evidence.** `IWorkflowEventClient` is a mode-neutral public interface with two routes and four overloads (`docs/specs/17-selected-mode-capability-matrix.md:1871-1892`). Task 3.8 requires complete ephemeral and durable journeys covering both routes and payload forms (`openspec/changes/reshape-developer-facing-interfaces/tasks.md:31`). The hosting contract explicitly says `AddOrcaCoreDurableEventIngress` exposes `IWorkflowEventClient` and that the durable engine includes ingress (`matrix:1538-1547`; `openspec/changes/reshape-developer-facing-interfaces/specs/developer-facing-surface/spec.md:70-71`). It never says that `AddOrcaCoreEphemeralEngine` registers the client, which component owns in-process wait lookup/dedup, or how an ephemeral-only consumer obtains it.

**Impact.** The event signatures are exact, but a clean ephemeral package consumer cannot complete the required task-3.8 journey without inventing service-registration semantics or constructing an undeclared implementation. Resolving `IWorkflowEventClient` after ephemeral registration could either work or fail under the current text; a guard would turn that guess into policy.

**Smallest remediation.** State explicitly that `AddOrcaCoreEphemeralEngine` registers `IWorkflowEventClient`, identify its in-process delivery/dedup owner and lifetime, and add that service-resolution guarantee to tasks 3.7/3.8. If ephemeral external delivery is not intended, narrow task 3.8 and explain how ordinary ephemeral `Wait` is resumed instead.

### P1-11 — Four high-consequence integration misuses are knowingly accepted without an enforceable boundary

**Evidence.** The prompt requires every silently accepted misuse affecting identity, external effects, lease capacity, mapping, or dependency direction to be at least P1 (`developer-facing-interface-v1-simplification-reviewer-prompt-2026-07-18.md:238-239`). The proposed surface exposes arbitrary ephemeral delegates (`docs/specs/17-public-authoring-contract.cs:153-161`), a plain diagnostic `int AttemptNumber` beside `StepOperationId` (`docs/specs/17-selected-mode-capability-matrix.md:236-246,349-354`), and a trusted lease-confirmation call that accepts only caller-supplied token/confirmation IDs (`matrix:827-833,895-903`). Nothing can detect a closure driving external effects from mutable captured state, an adapter using `AttemptNumber` as an idempotency key, an adapter replaying a cached prior `StepOperationId` for another occurrence, or a trusted reconciler attesting stop based only on elapsed time/delete acknowledgement/terminal status.

**Impact.** All four calls compile and core cannot distinguish correct use from the misuse. The first three can duplicate or misassociate external work; the fourth can release capacity while protected work still exists. Calling the integration “trusted” explains the intended boundary but does not make a time-only attestation runtime-detectable. Under the prompt's explicit severity rule these cannot be downgraded to documentation-only friction.

**Smallest remediation.** Make the residual trust model normative and executable: certify external-effect adapters against operation-ID stability/distinctness, expose attempt count as diagnostics rather than an ordinary step-body value if feasible, add an analyzer/registration rule for effect-driving ephemeral closures or clearly forbid such use, and define authorization/audit/certification requirements for trusted stop confirmation. If runtime enforcement is deliberately impossible, record each as an accepted P1 residual risk rather than claiming misuse resistance.

### P2-1 — Pre-wait instance event delivery has opposite lower- and higher-authority contracts

**Evidence.** The normative matrix says a live instance without a matching wait returns non-consuming `NoActiveWait`; only an accepted target writes dedup state (`docs/specs/17-selected-mode-capability-matrix.md:1914-1924`). EV-012 agrees (`docs/specs/05-requirements-events-waits-timers.md:49-56`). EV-030 in the same canonical document instead requires instance-targeted events arriving before a wait to be buffered and later consumed without redelivery (`:87-100`), and AC-104 repeats it (`docs/specs/12-acceptance-criteria.md:136-137`). The scheduler journey relies on mailbox handling when a Job completes before wait registration (`docs/specs/14-driving-scenario-eks-job-scheduler.md:105-111,203-208`). The five public statuses have no distinct buffered outcome (`docs/specs/17-selected-mode-capability-matrix.md:1858-1869`).

**Impact.** The explicit precedence and task 3.8's “non-consuming results” wording make the matrix behavior guardable: return `NoActiveWait`, write no dedup record, and redeliver. This therefore does not require guard invention by itself. It remains a material planning/developer inconsistency because an implementer or companion author following EV-030/AC-104/the scheduler journey would stop redelivery and rely on storage that the approved facade says did not occur.

**Smallest remediation.** Given the higher-authority matrix, remove the mailbox guarantee, replace AC-104, and require same-`EventId` redelivery after the intended wait is observed. If buffering is desired instead, add an explicit buffered acceptance contract and update statuses, dedup rules, matrix, delta, tasks, and scheduler journey together.

### P2-2 — Root-only `ContinueAsNew` leaves finite rollover usability unspecified

**Evidence.** The only method is unconditional on `DurableWorkflowBuilder` (`docs/specs/17-public-authoring-contract.cs:220-291`, specifically `:282-284`). Root `If`/`While` bodies receive `DurableNestedBuilder` (`:237-245`), which has no `ContinueAsNew` (`:342-388`). The contract requires exactly one root `End` and every reachable successful root path to converge on it (`openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md:69-89`).

**Impact.** Root-only placement is an accepted decision, and the exact unconditional signature is guardable. However, a normal finite generational workflow cannot express “roll over if history is large, otherwise reach typed `End`.” Once the unconditional node is reached, an `End` after it is never reached in that generation; putting `End` first closes authoring. No authoritative requirement explicitly promises finite conditional rollover, so this is a usability/completeness ambiguity rather than a Phase 0 signature blocker.

**Smallest remediation.** State that v1 rollover is intentionally unconditional/perpetual and explain how the required `End` is validated, or—only if finite rollover is required—approve a separate root-only conditional terminal design and its path validation in a later amendment.

### P2-3 — The stable diagnostic contract lacks a complete catalog and location grammar

**Evidence.** The compiler promises stable code, severity, primary/related locations, deterministic order, and all graph checks (`docs/specs/17-selected-mode-capability-matrix.md:1192-1215`), but only four lease codes are assigned (`:1224-1238`). `AuthoredLocation.Value` is an opaque internal string with no path grammar (`:163-167`). Task 3.4 asks for exact `TryBuild` diagnostics and `Build` parity (`openspec/changes/reshape-developer-facing-interfaces/tasks.md:27`).

**Impact.** A guard author must invent codes and locations for missing/multiple End, incomplete paths, duplicate branches, return/merge/type/codec/limit, and loop-progress failures, or avoid testing the stated stability guarantee.

**Smallest remediation.** Define the code/severity/location grammar for every promised graph check, or narrow the Phase 0 guarantee to parity, order, and diagnostic category while explicitly deferring code-level compatibility.

### P2-4 — `DefinitionId.Parse` does not explicitly reject `Guid.Empty`

**Evidence.** `DefinitionId` exposes `New`/`Parse`/`TryParse` (`docs/specs/17-selected-mode-capability-matrix.md:68-75`), but the validation prose covers string factories and positive versions, not empty GUIDs (`:307-327`). The workflow-contract delta permits rejection at construction **or** the nearest boundary (`openspec/changes/reshape-developer-facing-interfaces/specs/workflow-contracts/spec.md:97-102`), leaving `Parse` observably ambiguous.

**Impact.** Consumers cannot know whether `Parse(Guid.Empty.ToString())` succeeds and fails only during authoring/registration, and guards cannot fix one observable boundary without choosing policy.

**Smallest remediation.** State that `New` never returns empty, `Parse(Guid.Empty.ToString())` throws, and `TryParse` returns false/null; apply the same rule to other GUID-backed runtime IDs if intended.

### P2-5 — Summary/supporting artifacts contradict higher-authority choices

**Evidence.** These do not independently require signature invention because precedence resolves them, but they must be fixed before planning approval:

- leasing is summarized as root plus branch/item only (`docs/specs/17-selected-mode-capability-matrix.md:43`; reshape `design.md:76-83`), while the exact table/companion and canonical MG-062 permit root conditional/loop nested scopes (`matrix:779-801,843-847`; companion `:342-387`; `docs/specs/09-requirements-management-operations.md:215-217`);
- the active phased plan calls opaque selector/request changes fingerprint conflicts (`docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md:82-84`), while the normative rule says they require a version bump and are outside the structural fingerprint (`matrix:419-426,1898-1900`);
- EV-013/AC-115 require bulk retrieval (`docs/specs/05-requirements-events-waits-timers.md:58-60`; `docs/specs/12-acceptance-criteria.md:170-172`), while canonical management and task 3.7 explicitly defer/reject it (`docs/specs/09-requirements-management-operations.md:9-14,30-33`; reshape `tasks.md:30`);
- the required hosting integration review has no superseded banner and still asks for `AddOrcaCore` and `AddOrcaCoreHostedServices()` (`docs/review/integration-tests/01-hosting-and-hosted-services.md:62-71`), contrary to the exact role registrations (`matrix:1498-1549`).

**Impact.** A consumer or later implementation agent following a lower-authority active-looking file can implement the opposite availability, fingerprint, bulk, or hosting behavior even though a guard author can resolve it through precedence.

**Smallest remediation.** Correct the current phased plan and canonical requirement text, clarify the lease summary, and add a conspicuous superseded-routing banner to the hosting review. Do not rewrite the dated amendment, which is context only.

### P2-6 — “Configuration-binder-friendly” is not true for the declared nested values

**Evidence.** The options contain lists of `StepExecutionThrottle`, `TransientPoolDefinition`, and `DurableResourcePoolDefinition` plus a factory-only `ResourceGovernancePartitionId` (`docs/specs/17-selected-mode-capability-matrix.md:1243-1304`). Those element types have private constructors and read-only properties (`:1267-1297`), yet the prose says the option objects are configuration-binder-friendly (`:1383-1388`).

**Impact.** Standard configuration binding cannot materialize these nested entries without undeclared converters or intermediate DTOs, so ordinary hosted applications can fail before the promised immediate option validation even runs.

**Smallest remediation.** Remove the binder-friendly claim, or define the exact binding DTO/converter/builder contract and cover it with startup guards.

### P2-7 — Governance stream validation is assigned to the wrong seam

**Evidence.** `ResourceGovernanceRecord.FromPersisted` receives a single sequence number (`docs/specs/17-selected-mode-capability-matrix.md:1330-1346`) but is said to validate a “positive consecutive sequence” (`:1466-1468`). Consecutiveness needs the stream version and neighboring records. `ResourceGovernanceStream` is a public positional record over caller-owned `IReadOnlyList` (`:1348-1350`) with no copying/continuity factory.

**Impact.** A per-record factory cannot prove continuity, while the stream record can retain a mutable caller-owned list. Task 3.3 therefore lacks the seam needed to guard complete persisted-stream validation.

**Smallest remediation.** Keep per-record positivity/format/checksum/copy checks in `FromPersisted`; add a stream factory/validator that copies non-null records and validates sequence continuity against the stream version.

### P2-8 — Canonical durable-pool management omits `ListAsync`

**Evidence.** The matrix and management delta require `ListAsync`, `GetAsync`, and `ResizeAsync` (`docs/specs/17-selected-mode-capability-matrix.md:1314-1328`; `openspec/changes/reshape-developer-facing-interfaces/specs/management-and-querying/spec.md:95-100`), while canonical MG-065 says “only typed GetAsync and ResizeAsync” (`docs/specs/09-requirements-management-operations.md:337-342`).

**Impact.** The exact public baseline and canonical management requirement describe different operator surfaces; an implementer following MG-065 can omit a matrix-required method.

**Smallest remediation.** Add `ListAsync` to MG-065 or remove it consistently from the matrix and management delta.

### P3-1 — DAG “visualization” is promised without an edge projection

**Evidence.** The capability summary promises DAG planning and visualization (`docs/specs/17-selected-mode-capability-matrix.md:46`), but `WorkflowDagPlan` exposes only a node list (`:982-989`); dependencies appear only in runtime snapshots after registration (`:1037-1058`).

**Impact.** A design-time visualizer cannot reconstruct authored edges from the public plan even though visualization is advertised.

**Smallest remediation.** Remove “visualization” from the v1 claim or expose an immutable authored node/edge projection that does not leak executable mapping delegates.

## 3. Explicit non-findings

- **The matrix metavariables are not extra public types.** Lines 54-63 of document 17 call them compact semantic indexes into the companion, and the workflow-authoring delta makes every concrete companion declaration normative (`workflow-authoring/spec.md:219-224`). The real join defect is the two concrete leased-nested return types in P1-1, not the existence of twelve concrete join families.
- **Factory-only caller values are now substantially coherent.** The private-constructor plus sole `Create(string)` family and runtime-created parser family are separated consistently at `docs/specs/17-selected-mode-capability-matrix.md:65-216,307-327`. P2-4 is a narrow GUID-empty clarification, not a rejection of the approach.
- **Mode selection and deferred-surface choices are sound.** Durable lambdas, `WhenFirst`, Saga, public external jobs/children, nested dynamic fan-out, `WaitLong`, and author `Yield` are intentionally absent. The current source still contains provisional members, but the prompt identifies that as pending implementation rather than planning evidence.
- **Structured joins and bounded `ForEach` are well closed.** Ordered success/failure-only outcomes, no sibling auto-cancel, valid empty input, pre-admission item bounds, committed durable snapshots, and ancestor merge suppression form a coherent contract (`matrix:595-745`).
- **The path-token model is unusually precise.** Parent release before fan-out, parked-path release/reacquisition, separate admitted-item/DAG-node counts, and physical-slot retention for token-ignoring attempts avoid the common ceiling-one deadlock (`matrix:1390-1425`).
- **Core does not itself treat lease time as ownership proof.** No author TTL, renewal, holder ID, force release, terminal-status release, or elapsed-time reclaim is exposed; the exact token confirmation matrix is a strong safety choice (`matrix:836-903,1427-1449`). P1-11 is the narrower residual risk that a privileged caller can lie about the causal proof behind its attestation.
- **The one-way integration boundary is correct.** `OrcaCore.Dag.Hosting` is the sole friend bridge, child management remains internal, and no Kubernetes/AWS/EKS/job DTO or SDK reference was found under root `src/`.
- **Strict OpenSpec validation is green but is not semantic approval.** Both changes validate; the findings above are cross-artifact/C# expressibility defects outside schema validation.
- **The dated amendment was used only as context.** Per the reviewer prompt (`:52-54`), its superseded recommendations are not current authority and no remediation in this review asks that historical artifact to be rewritten.

## 4. Dimension scores

Scores use a five-point scale and judge the current planning packet, not the known provisional implementation.

| Dimension | Score | Assessment |
|---|---:|---|
| Consistency | 2/5 | Precedence resolves several stale texts, but exact companion, event, retry/quarantine, and package contracts still conflict. |
| Comprehensiveness | 3/5 | The packet covers nearly every needed domain, yet omits failure, recovery discovery, resize conflict, package manifest, ephemeral event ownership, and deterministic governance-test seams. |
| Developer orientation | 3/5 | Staged builders, typed handles, strong names, and role-specific hosting are good; async-void, unclear perpetual rollover, package ambiguity, and binder claims are material friction. |
| Misuse resistance | 2/5 | Most removed features are compile-impossible, but unsafe fluent/adapter/trust paths remain silently accepted and DAG mapping enforcement is not expressible. |
| Durable safety | 2/5 | Identity, deadlines, path tokens, and quarantine intent are strong; retry/quarantine ownership and operator recovery are not closed. |
| Package isolation | 4/5 | Dependency direction and Kubernetes isolation are strong; exact package ownership/IDs and packed consumer references remain undefined. |

## 5. Realistic consumer programs

These are syntactically complete consumer-journey attempts at the OrcaCore boundary, not claims that the known-stale source compiles the proposed API today. Program 5 defines its companion-owned DTO/port layer but intentionally omits Kubernetes SDK implementations. A line marked **BLOCKED** is the point at which the planning contract prevents a runnable journey without an unapproved signature; no invented OrcaCore call is hidden behind a placeholder.

### 5.1 Ephemeral typed input/output, async lambda, transient pool, outcomes, `If`, replacement state, and deadlines

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Hosting;

public sealed record QuoteInput(string CustomerId, decimal Amount);
public sealed record QuoteState(
    string CustomerId,
    decimal Amount,
    decimal Risk,
    int FailedChecks,
    bool ManualReview);
public sealed record CheckState(string CustomerId, decimal Score = 0);
public sealed record CheckResult(string Name, decimal Score);
public sealed record QuoteOutput(decimal Risk, bool ManualReview);

public sealed class RecordManualReviewStep : IStep<QuoteState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<QuoteState> context,
        CancellationToken cancellationToken)
    {
        context.ReplaceState(context.State with { ManualReview = true });
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

public static class QuoteJourney
{
public static async Task RunAsync()
{
var ioPool = TransientPoolName.Create("quote-io");
var definition = Workflow
    .Ephemeral<QuoteState>(DefinitionId.Parse("3b3f4902-d892-46d0-aece-fc9555fac304"),
                           new DefinitionVersion(1))
    .Init<QuoteInput>(input => new QuoteState(input.CustomerId, input.Amount, 0, 0, false))
    .CompleteWithin(TimeSpan.FromSeconds(30))
    // Two arguments deliberately select Func<...,CancellationToken,ValueTask>.
    .Then(async (context, cancellationToken) =>
    {
        await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
        decimal risk = context.State.Amount / 100m;
        context.ReplaceState(context.State with { Risk = risk });
    })
    .WithStepTimeout(TimeSpan.FromSeconds(5))
    .WithTransientPool(ioPool)
    .Parallel<CheckResult>(branches => branches
        .Branch<CheckState>(
            AuthoredBranchId.Create("fraud"),
            state => new CheckState(state.Value.CustomerId),
            branch => branch
                .Then((ctx, _) =>
                {
                    ctx.ReplaceState(ctx.State with { Score = 0.25m });
                    return ValueTask.CompletedTask;
                })
                .Return(state => new CheckResult("fraud", state.Value.Score)))
        .Branch<CheckState>(
            AuthoredBranchId.Create("credit"),
            state => new CheckState(state.Value.CustomerId),
            branch => branch
                .Then((ctx, _) =>
                {
                    ctx.ReplaceState(ctx.State with { Score = 0.50m });
                    return ValueTask.CompletedTask;
                })
                .Return(state => new CheckResult("credit", state.Value.Score))))
    .WhenAllOutcomes((parent, outcomes) => parent.Value with
    {
        FailedChecks = outcomes.Count(x => x is BranchOutcome<CheckResult>.Failed)
    })
    .If(
        state => state.Value.FailedChecks > 0,
        then => then.Then<RecordManualReviewStep>(),
        otherwise => otherwise!.Then((ctx, _) =>
        {
            ctx.ReplaceState(ctx.State with { ManualReview = false });
            return ValueTask.CompletedTask;
        }))
    .End(
        state => new QuoteOutput(state.Value.Risk, state.Value.ManualReview),
        WorkflowOutcomeName.Create("quote-produced"))
    .Build();

HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder();
hostBuilder.Services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
{
    StructuredExecution = new StructuredExecutionHostOptions
    {
        MaxConcurrentExecutionPathsPerInstance = 4,
        StepThrottles = Array.Empty<StepExecutionThrottle>()
    },
    TransientPools = new[] { TransientPoolDefinition.Create(ioPool, 2) }
});

using IHost host = hostBuilder.Build();
await host.StartAsync();
var registry = host.Services.GetRequiredService<IWorkflowDefinitionRegistry>();
var registered = registry.Register(definition);
var handle = registered switch
{
    WorkflowRegistrationResult<EphemeralDefinitionHandle<QuoteInput, QuoteOutput>>.Registered ok
        => ok.Handle,
    _ => throw new InvalidOperationException("definition conflict")
};
var start = (WorkflowStartResult<WorkflowInstanceHandle<QuoteOutput>>.Accepted)
    await handle.StartOrGetAsync(
    new QuoteInput("customer-42", 100m),
    StartIdempotencyKey.Create("quote/customer-42/100"));
QuoteOutput output = await WaitForOutputAsync(start.Handle);
await host.StopAsync();
}

private static async Task<QuoteOutput> WaitForOutputAsync(
    WorkflowInstanceHandle<QuoteOutput> instance)
{
    for (int i = 0; i < 200; i++)
    {
        WorkflowOutputResult<QuoteOutput> result = await instance.GetOutputAsync();
        if (result is WorkflowOutputResult<QuoteOutput>.Available available)
            return available.Output;
        if (result is WorkflowOutputResult<QuoteOutput>.Unavailable unavailable)
            throw new InvalidOperationException($"workflow ended {unavailable.Status}");
        await Task.Delay(TimeSpan.FromMilliseconds(10));
    }

    throw new TimeoutException("ephemeral workflow did not produce output");
}
}
```

**Friction.** The two-parameter async spelling is safe, but the more idiomatic `Then(async context => ...)` silently becomes `async void` (P1-2). The options cannot actually be bound from ordinary configuration as claimed (P2-6).

### 5.2 Durable bounded `ForEach`, failure summaries, host replacement, and typed output

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Hosting;

public sealed record BatchInput(IReadOnlyList<WorkItem> Items);
public sealed record BatchState(IReadOnlyList<WorkItem> Items, int Succeeded, int Failed);
public sealed record WorkItem(string Id, bool ShouldFail);
public sealed record ItemState(WorkItem Item, string? Result = null);
public sealed record ItemResult(string Id, string Value);
public sealed record BatchOutput(int Succeeded, int Failed);

public sealed class ProcessItemStep : IStep<ItemState>
{
    public async ValueTask<StepResult> ExecuteAsync(
        StepContext<ItemState> context,
        CancellationToken cancellationToken)
    {
        if (context.State.Item.ShouldFail)
            throw new InvalidOperationException("application item failure");

        await Task.Delay(TimeSpan.FromMilliseconds(1), cancellationToken);
        string value = $"processed:{context.State.Item.Id}:{context.Execution.OperationId.Value}";
        context.ReplaceState(context.State with { Result = value });
        return new StepResult.Completed();
    }
}

public sealed class RejectBatchStep : IStep<BatchState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<BatchState> context,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("too many item failures");
}

public static class DurableBatchJourney
{
public static async Task RunAsync()
{
var definition = Workflow
    .Durable<BatchState>(DefinitionId.Parse("3984acb0-b661-4594-84f0-a2c0ded60a22"),
                         new DefinitionVersion(1))
    .Init<BatchInput>(input => new BatchState(input.Items, 0, 0))
    .CompleteWithin(TimeSpan.FromHours(2))
    .ForEach<WorkItem, ItemState, ItemResult>(
        state => state.Value.Items,
        ForEachOptions.Create(maxItems: 1_000, maxConcurrency: 16),
        item => new ItemState(item.Item),
        body => body
            .Then<ProcessItemStep>()
            .WithRetry(maxAttempts: 3, fixedDelay: TimeSpan.FromSeconds(2))
            .WithStepTimeout(TimeSpan.FromMinutes(2))
            .Return(state => new ItemResult(state.Value.Item.Id, state.Value.Result!)))
    .WhenAllOutcomes((parent, outcomes) => parent.Value with
    {
        Succeeded = outcomes.Count(x => x is ForEachItemOutcome<ItemResult>.Succeeded),
        Failed = outcomes.Count(x => x is ForEachItemOutcome<ItemResult>.Failed)
    })
    .If(state => state.Value.Failed > 10, reject => reject.Then<RejectBatchStep>())
    .End(state => new BatchOutput(state.Value.Succeeded, state.Value.Failed))
    .Build();

var durableOptions = new DurableEngineHostOptions
{
    StructuredExecution = new StructuredExecutionHostOptions
    {
        MaxConcurrentExecutionPathsPerInstance = 32,
        StepThrottles = new[] { StepExecutionThrottle.For<ProcessItemStep>(16) }
    },
    ResourcePools = new DurableResourcePoolOptions
    {
        PartitionId = ResourceGovernancePartitionId.Create("batch-host"),
        Pools = new[]
        {
            DurableResourcePoolDefinition.Create(
                ResourcePoolName.Create("batch-capacity"),
                capacity: 16,
                reviewAfter: TimeSpan.FromMinutes(10))
        }
    }
};

var input = new BatchInput(new[]
{
    new WorkItem("a", ShouldFail: false),
    new WorkItem("b", ShouldFail: true)
});

// BLOCKED: the planning contract does not give the PackageId, options type, or exact
// registration method for a production durable provider. A guard/program cannot legally spell:
// services.AddTheApprovedProductionProvider(theApprovedOptions);

// With that one approved provider line, the otherwise complete restart setup is:
IHost BuildDurableHost()
{
    HostApplicationBuilder builder = Host.CreateApplicationBuilder();
    // BLOCKED provider registration belongs here.
    builder.Services.AddOrcaCoreDurableEngine(durableOptions);
    return builder.Build();
}

using IHost host1 = BuildDurableHost();
await host1.StartAsync();
var registry1 = host1.Services.GetRequiredService<IWorkflowDefinitionRegistry>();
var definitionHandle1 = ((WorkflowRegistrationResult<DurableDefinitionHandle<BatchInput, BatchOutput>>.Registered)
    registry1.Register(definition)).Handle;
var start = (WorkflowStartResult<WorkflowInstanceHandle<BatchOutput>>.Accepted)
    await definitionHandle1.StartOrGetAsync(input, StartIdempotencyKey.Create("batch/2026-07-19"));

await host1.StopAsync();                 // process/host replacement
using IHost host2 = BuildDurableHost();
await host2.StartAsync();
var registry2 = host2.Services.GetRequiredService<IWorkflowDefinitionRegistry>();
var definitionHandle2 = ((WorkflowRegistrationResult<DurableDefinitionHandle<BatchInput, BatchOutput>>.Registered)
    registry2.Register(definition)).Handle;
var reopened = await definitionHandle2.GetInstanceAsync(start.Handle.InstanceId);
BatchOutput output = await WaitForOutputAsync(reopened);
await host2.StopAsync();
}

private static async Task<BatchOutput> WaitForOutputAsync(
    WorkflowInstanceHandle<BatchOutput> instance)
{
    for (int i = 0; i < 200; i++)
    {
        WorkflowOutputResult<BatchOutput> result = await instance.GetOutputAsync();
        if (result is WorkflowOutputResult<BatchOutput>.Available available)
            return available.Output;
        if (result is WorkflowOutputResult<BatchOutput>.Unavailable unavailable)
            throw new InvalidOperationException($"workflow ended {unavailable.Status}");
        await Task.Delay(TimeSpan.FromMilliseconds(10));
    }

    throw new TimeoutException("durable workflow did not produce output");
}
}
```

**Friction.** Workflow authoring and typed reopen are coherent. A truly complete provider-backed restart program cannot be written without inventing the production package/registration contract (P1-6). Explicit application failures also lack an approved `OrcaCoreException` construction/derivation path (P1-9), so the example uses exception normalization.

### 5.3 Two lexical lease lifetimes: short DB use, then scheduler capacity across `Wait`

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using OrcaCore;

public sealed record JobRequest(string JobKey, string Payload);
public sealed record JobState(
    string JobKey,
    string Payload,
    bool DatabaseInputLoaded,
    string? ExternalOperationId,
    string? ProtectionToken,
    bool TerminalVerified)
{
    public static JobState From(JobRequest input) =>
        new(input.JobKey, input.Payload, false, null, null, false);

    public JobOutput ToOutput() => new(JobKey, TerminalVerified);
}

public sealed record JobOutput(string JobKey, bool TerminalVerified);

public sealed class LoadDatabaseInputsStep : IStep<JobState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<JobState> context,
        CancellationToken cancellationToken)
    {
        context.ReplaceState(context.State with { DatabaseInputLoaded = true });
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

public sealed class CreateOrObserveExternalWorkStep : IStep<JobState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<JobState> context,
        CancellationToken cancellationToken)
    {
        context.ReplaceState(context.State with
        {
            ExternalOperationId = context.Execution.OperationId.Value,
            ProtectionToken = context.ResourceLease!.ProtectionToken.Value
        });
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

public sealed class VerifyTerminalExternalWorkStep : IStep<JobState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<JobState> context,
        CancellationToken cancellationToken)
    {
        if (context.ResumedEvent is null)
            throw new InvalidOperationException("terminal event required");
        context.ReplaceState(context.State with { TerminalVerified = true });
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

public static class DurableLeaseJourney
{
public static void Author()
{
var dbRequest = ResourceLeaseRequest.Create(
    ResourceLeaseRequirement.Require(ResourcePoolName.Create("database")));
var schedulerRequest = ResourceLeaseRequest.Create(
    ResourceLeaseRequirement.Require(ResourcePoolName.Create("scheduler-capacity")));

var jobTerminal = EventName.Create("job-terminal");
var resourceDefinition = Workflow
    .Durable<JobState>(DefinitionId.Parse("f76cb2fe-08bd-416e-bba4-309368b592f4"),
                       new DefinitionVersion(1))
    .Init<JobRequest>(input => JobState.From(input))
    .CompleteWithin(TimeSpan.FromHours(6))
    .AcquireResources(dbRequest, lease => lease
        .Then<LoadDatabaseInputsStep>()
        .WithStepTimeout(TimeSpan.FromSeconds(20)))
    // The DB lease is released before this point and is not held during the long wait.
    .AcquireResources(schedulerRequest, lease => lease
        .Then<CreateOrObserveExternalWorkStep>()
        .WithRetry(3, TimeSpan.FromSeconds(2))
        .WithStepTimeout(TimeSpan.FromSeconds(30))
        .Wait(jobTerminal, state => CorrelationId.Create(state.Value.JobKey))
        .Then<VerifyTerminalExternalWorkStep>())
    // Scheduler capacity releases only after the terminal event is verified and the scope exits.
    .End(state => state.Value.ToOutput())
    .Build();
}
}
```

`CreateOrObserveExternalWorkStep` uses both `context.Execution.OperationId` and
`context.ResourceLease!.ProtectionToken`. The first scope demonstrates short logical DB capacity;
the second intentionally holds logical scheduler capacity while cold-waiting.

**Friction.** A timeout/process-loss retry in the second scope has no unambiguous held-versus-quarantine transition (P1-4). If it does quarantine, operators cannot discover an orphaned token through an approved query (P1-5). A nested `Parallel` inside a nested leased body can escape the restriction (P1-1).

### 5.4 Typed DAG with heterogeneous children, direct mapping, failure blocking, and independent progress

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Dag;
using OrcaCore.Dag.Hosting;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;

public sealed record PipelineInput(string Source, string Destination, string AuditKey);
public sealed record FetchInput(string Source);
public sealed record FetchOutput(string ObjectKey);
public sealed record TransformInput(string ObjectKey);
public sealed record TransformOutput(string ArtifactKey);
public sealed record PublishInput(string ArtifactKey, string Destination);
public sealed record AuditInput(string AuditKey);
public sealed record AuditOutput(string Receipt);

public sealed class FailTransformStep : IStep<TransformInput>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<TransformInput> context,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("intentional transform failure");
}

public static class DagJourney
{
public static async Task RunAsync()
{
var fetchDefinition = Workflow
    .Durable<FetchInput>(DefinitionId.Parse("1f2e3b3b-2d44-42ee-adbf-c4fbb616ee75"), new DefinitionVersion(1))
    .Init<FetchInput>(input => input)
    .End(state => new FetchOutput($"raw/{state.Value.Source}"))
    .Build();
var transformDefinition = Workflow
    .Durable<TransformInput>(DefinitionId.Parse("08500819-96cf-4c75-937e-d6d371ec4bc5"), new DefinitionVersion(1))
    .Init<TransformInput>(input => input)
    .Then<FailTransformStep>()
    .End(state => new TransformOutput($"artifact/{state.Value.ObjectKey}"))
    .Build();
var publishDefinition = Workflow
    .Durable<PublishInput>(DefinitionId.Parse("54e4aa3e-ae08-4bcd-98c2-08e5ba064b9e"), new DefinitionVersion(1))
    .Init<PublishInput>(input => input)
    .End()
    .Build();
var auditDefinition = Workflow
    .Durable<AuditInput>(DefinitionId.Parse("3847f44e-b353-4d76-9495-e7a66f77fcc8"), new DefinitionVersion(1))
    .Init<AuditInput>(input => input)
    .End(state => new AuditOutput($"receipt/{state.Value.AuditKey}"))
    .Build();

WorkflowDagPlan<PipelineInput> plan = BuildPipeline(
    fetchDefinition.Reference,
    transformDefinition.Reference,
    publishDefinition.Reference,
    auditDefinition.Reference);

var durableOptions = new DurableEngineHostOptions
{
    StructuredExecution = new StructuredExecutionHostOptions
    {
        MaxConcurrentExecutionPathsPerInstance = 8,
        StepThrottles = Array.Empty<StepExecutionThrottle>()
    },
    ResourcePools = new DurableResourcePoolOptions
    {
        PartitionId = ResourceGovernancePartitionId.Create("dag-host"),
        Pools = new[]
        {
            DurableResourcePoolDefinition.Create(
                ResourcePoolName.Create("dag-capacity"), 2, TimeSpan.FromMinutes(10))
        }
    }
};

HostApplicationBuilder hostBuilder = Host.CreateApplicationBuilder();
hostBuilder.Services.AddOrcaCoreInMemoryDurableProvider();
hostBuilder.Services.AddOrcaCoreDurableEngine(durableOptions);
hostBuilder.Services.AddOrcaCoreDag(new DagHostOptions { MaxConcurrentNodes = 2 });
using IHost host = hostBuilder.Build();
await host.StartAsync();

var workflowRegistry = host.Services.GetRequiredService<IWorkflowDefinitionRegistry>();
workflowRegistry.Register(fetchDefinition);
workflowRegistry.Register(transformDefinition);
workflowRegistry.Register(publishDefinition);
workflowRegistry.Register(auditDefinition);

var dagRegistry = host.Services.GetRequiredService<IDagDefinitionRegistry>();
var dagDefinition = ((DagRegistrationResult<PipelineInput>.Registered)
    dagRegistry.Register(plan)).Handle;
var run = (DagStartResult.Accepted)await dagDefinition.StartOrGetAsync(
    new PipelineInput("source", "destination", "audit-42"),
    StartIdempotencyKey.Create("pipeline/audit-42"));

DagRunSnapshot snapshot = await WaitForTerminalAsync(run.Handle);
Require(snapshot.Status == DagRunStatus.Failed, "run must fail after progress drains");
Require(Node(snapshot, "transform").Status == DagNodeStatus.Failed,
    "transform must fail");
Require(Node(snapshot, "publish").Status == DagNodeStatus.DependencyBlocked,
    "publish must be dependency-blocked");
Require(Node(snapshot, "audit").Status == DagNodeStatus.Succeeded,
    "independent audit must still progress");

await host.StopAsync();
}

private static WorkflowDagPlan<PipelineInput> BuildPipeline(
    DurableWorkflowRef<FetchInput, FetchOutput> fetchRef,
    DurableWorkflowRef<TransformInput, TransformOutput> transformRef,
    DurableWorkflowRef<PublishInput> publishRef,
    DurableWorkflowRef<AuditInput, AuditOutput> auditRef)
{
    var dag = Dag.Define<PipelineInput>(
        DefinitionId.Parse("34730391-d9fb-4549-88bf-c522401d6c90"),
        new DefinitionVersion(1));

    DagNodeRef<FetchOutput> fetch = dag
        .Node(DagNodeId.Create("fetch"), fetchRef)
        .MapInput(ctx => new FetchInput(ctx.RunInput.Source));

    DagNodeRef<TransformOutput> transform = dag
        .Node(DagNodeId.Create("transform"), transformRef)
        .DependsOn(fetch)
        .MapInput(ctx => new TransformInput(ctx.OutputOf(fetch).ObjectKey));

    dag.Node(DagNodeId.Create("publish"), publishRef)
        .DependsOn(transform)
        .MapInput(ctx => new PublishInput(
            ctx.OutputOf(transform).ArtifactKey,
            ctx.RunInput.Destination));

    dag.Node(DagNodeId.Create("audit"), auditRef)
        .MapInput(ctx => new AuditInput(ctx.RunInput.AuditKey));

    return dag.Build();
}

private static async Task<DagRunSnapshot> WaitForTerminalAsync(DagRunHandle run)
{
    for (int i = 0; i < 200; i++)
    {
        DagRunSnapshot snapshot = await run.GetSnapshotAsync();
        if (snapshot.Status is DagRunStatus.Succeeded
            or DagRunStatus.Failed
            or DagRunStatus.Cancelled)
            return snapshot;

        await Task.Delay(TimeSpan.FromMilliseconds(10));
    }

    throw new TimeoutException("DAG run did not become terminal");
}

private static DagNodeSnapshot Node(DagRunSnapshot snapshot, string id) =>
    snapshot.Nodes.Single(node => node.NodeId.Equals(DagNodeId.Create(id)));

private static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
}
```

If `transform` fails, `publish` becomes `DependencyBlocked`; independent `audit` remains eligible and may succeed. The run eventually fails once nothing else can progress.

**Friction.** The direct mappings read well, the in-memory role is sufficient for this non-restart journey, and wrong output types do not compile. The declared opaque `Func` cannot make the promised undeclared/non-direct `OutputOf` error a build diagnostic (P1-3).

### 5.5 Outward-only Kubernetes Job companion

All Kubernetes types below live in the companion project; OrcaCore sees only application DTOs and generic contracts.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore;
using OrcaCore.Hosting;

// Companion project only. These are application DTOs, not Kubernetes SDK types.
public sealed record KubernetesJobRequest(
    string ApplicationJobKey,
    string Cluster,
    string Namespace,
    string Image);

public sealed record KubernetesJobOutput(string ApplicationJobKey, string TerminalPhase);

public sealed record KubernetesJobState(
    string ApplicationJobKey,
    string Cluster,
    string Namespace,
    string Image,
    string? Name,
    string? ObservedUid,
    string? StepOperationId,
    string? ProtectionToken,
    string? TerminalPhase)
{
    public static KubernetesJobState From(KubernetesJobRequest request) =>
        new(request.ApplicationJobKey, request.Cluster, request.Namespace, request.Image,
            null, null, null, null, null);
}

public sealed record KubernetesJobObservation(string Name, string Uid);

public sealed record JobTerminalReport(
    string StableReportId,
    string ApplicationJobKey,
    DefinitionId DefinitionId,
    string Phase,
    DateTimeOffset ObservedAtUtc);

public sealed record QuarantinedJobRecord(
    InstanceId InstanceId,
    string Cluster,
    string Namespace,
    string Name,
    string ObservedUid,
    string StepOperationId,
    string ProtectionToken,
    string StableConfirmationId);

public interface IKubernetesJobGateway
{
    Task<KubernetesJobObservation> CreateOrObserveAsync(
        string cluster,
        string @namespace,
        string image,
        string operationIdLabel,
        string protectionTokenLabel,
        CancellationToken cancellationToken);

    Task StopAndObserveTerminalOrAbsentAsync(
        string cluster,
        string @namespace,
        string name,
        string observedUid,
        CancellationToken cancellationToken);
}

public interface IJobReportRetryQueue
{
    Task EnqueueUnchangedAsync(
        WorkflowEvent<JobTerminalReport> envelope,
        CancellationToken cancellationToken);
}

public interface IQuarantinedJobRecordStore
{
    Task UpsertObservedAsync(QuarantinedJobRecord record, CancellationToken cancellationToken);
    Task<QuarantinedJobRecord?> GetAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken);
}

public sealed class CreateOrObserveJobStep : IStep<KubernetesJobState>
{
    private readonly IKubernetesJobGateway gateway;
    private readonly IQuarantinedJobRecordStore records;

    public CreateOrObserveJobStep(
        IKubernetesJobGateway gateway,
        IQuarantinedJobRecordStore records)
    {
        this.gateway = gateway;
        this.records = records;
    }

    public async ValueTask<StepResult> ExecuteAsync(
        StepContext<KubernetesJobState> context,
        CancellationToken cancellationToken)
    {
        StepOperationId operationId = context.Execution.OperationId;
        LeaseProtectionToken protectionToken =
            context.ResourceLease?.ProtectionToken
            ?? throw new InvalidOperationException("scheduler lease required");

        KubernetesJobObservation observed = await gateway.CreateOrObserveAsync(
            context.State.Cluster,
            context.State.Namespace,
            context.State.Image,
            operationId.Value,
            protectionToken.Value,
            cancellationToken);

        var record = new QuarantinedJobRecord(
            context.Execution.WorkflowInstanceId,
            context.State.Cluster,
            context.State.Namespace,
            observed.Name,
            observed.Uid,
            operationId.Value,
            protectionToken.Value,
            $"stop/{context.Execution.WorkflowInstanceId.Value}/{protectionToken.Value}");
        await records.UpsertObservedAsync(record, cancellationToken);

        context.ReplaceState(context.State with
        {
            Name = observed.Name,
            ObservedUid = observed.Uid,
            StepOperationId = operationId.Value,
            ProtectionToken = protectionToken.Value
        });
        return new StepResult.Completed();
    }
}

public sealed class ApplyTerminalReportStep : IStep<KubernetesJobState>
{
    public ValueTask<StepResult> ExecuteAsync(
        StepContext<KubernetesJobState> context,
        CancellationToken cancellationToken)
    {
        JobTerminalReport report = context.ResumedEvent?.GetPayload<JobTerminalReport>()
            ?? throw new InvalidOperationException("terminal report required");
        context.ReplaceState(context.State with { TerminalPhase = report.Phase });
        return ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}

public sealed class KubernetesTerminalWatcher
{
    private readonly IWorkflowEventClient events;
    private readonly IJobReportRetryQueue retryQueue;

    public KubernetesTerminalWatcher(
        IWorkflowEventClient events,
        IJobReportRetryQueue retryQueue)
    {
        this.events = events;
        this.retryQueue = retryQueue;
    }

    public async Task ReportAsync(JobTerminalReport report, CancellationToken cancellationToken)
    {
        var envelope = WorkflowEvent<JobTerminalReport>.Create(
            EventId.Create(report.StableReportId),
            EventName.Create("job-terminal"),
            CorrelationId.Create(report.ApplicationJobKey),
            report,
            report.ObservedAtUtc);

        EventDeliveryResult result = await events.DeliverByCorrelationAsync(
            report.DefinitionId,
            envelope,
            cancellationToken);

        // Under the matrix contract the watcher must retain and redeliver the same envelope
        // after NoActiveWait. Canonical EV-030 currently says the opposite (P2-1).
        if (result.Status is EventDeliveryStatus.NoActiveWait)
            await retryQueue.EnqueueUnchangedAsync(envelope, cancellationToken);
    }
}

public sealed class KubernetesStopReconciler
{
    private readonly IKubernetesJobGateway gateway;
    private readonly IDurableResourceLeaseRecovery recovery;

    public KubernetesStopReconciler(
        IKubernetesJobGateway gateway,
        IDurableResourceLeaseRecovery recovery)
    {
        this.gateway = gateway;
        this.recovery = recovery;
    }

    public async Task<ProtectedWorkStopConfirmationStatus> ReconcileAsync(
        QuarantinedJobRecord record,
        CancellationToken cancellationToken)
    {
        // Companion uses the observed UID/precondition, never object name alone.
        await gateway.StopAndObserveTerminalOrAbsentAsync(
            record.Cluster,
            record.Namespace,
            record.Name,
            record.ObservedUid,
            cancellationToken);

        return await recovery.ConfirmProtectedWorkStoppedAsync(
            LeaseProtectionToken.Parse(record.ProtectionToken),
            StopConfirmationId.Create(record.StableConfirmationId),
            cancellationToken);
    }
}

public sealed class CancellationDeadlineObserver
{
    private readonly IQuarantinedJobRecordStore records;
    private readonly KubernetesStopReconciler reconciler;

    public CancellationDeadlineObserver(
        IQuarantinedJobRecordStore records,
        KubernetesStopReconciler reconciler)
    {
        this.records = records;
        this.reconciler = reconciler;
    }

    public async Task<ProtectedWorkStopConfirmationStatus?> ObserveAsync(
        WorkflowInstanceHandle instance,
        CancellationToken cancellationToken)
    {
        WorkflowInstanceSnapshot snapshot = await instance.GetSnapshotAsync(cancellationToken);
        if (snapshot.Status is not (WorkflowInstanceStatus.CancellationRequested
            or WorkflowInstanceStatus.TimedOut
            or WorkflowInstanceStatus.Cancelled
            or WorkflowInstanceStatus.Terminated
            or WorkflowInstanceStatus.Failed))
            return null;

        QuarantinedJobRecord? record = await records.GetAsync(
            snapshot.InstanceId,
            cancellationToken);
        if (record is null)
            throw new InvalidOperationException("no discoverable stop-reconciliation record");

        return await reconciler.ReconcileAsync(record, cancellationToken);
    }
}

public static class KubernetesJobJourney
{
    public static DurableWorkflowDefinition<KubernetesJobRequest, KubernetesJobOutput> Author()
    {
        EventName terminal = EventName.Create("job-terminal");
        ResourceLeaseRequest capacity = ResourceLeaseRequest.Create(
            ResourceLeaseRequirement.Require(ResourcePoolName.Create("scheduler-capacity")));

        return Workflow
            .Durable<KubernetesJobState>(
                DefinitionId.Parse("90e0f5e7-357c-48cb-90a9-20f55efc52e1"),
                new DefinitionVersion(1))
            .Init<KubernetesJobRequest>(KubernetesJobState.From)
            .CompleteWithin(TimeSpan.FromHours(8))
            .AcquireResources(capacity, lease => lease
                .Then<CreateOrObserveJobStep>()
                .WithRetry(3, TimeSpan.FromSeconds(2))
                .WithStepTimeout(TimeSpan.FromMinutes(1))
                .Wait(terminal, state => CorrelationId.Create(state.Value.ApplicationJobKey))
                .Then<ApplyTerminalReportStep>())
            .End(state => new KubernetesJobOutput(
                state.Value.ApplicationJobKey,
                state.Value.TerminalPhase!))
            .Build();
    }

    public static void ConfigureCallbackHost(IServiceCollection services)
    {
        // Callback-only watcher host: event delivery/continuation, no definition execution.
        services.AddOrcaCoreDurableEventIngress();
    }
}
```

**Friction.** Operation identity, UID-safe stop, stable `EventId`, cancellation/deadline observation, and generic stop confirmation compose without leaking a Kubernetes type into OrcaCore. The companion-owned record bridges cancellation/deadline observation to reconciliation, but a crash before that record/label exists still has no approved OrcaCore discovery path (P1-5). The runtime cannot verify why a trusted reconciler attested stop (P1-11), and pre-wait delivery must be reconciled (P2-1).

## 6. Adversarial misuse classification

“Compile-impossible” describes the proposed final public surface, not the known provisional source. A “runtime defense” may occur at registration/startup or during serialized execution as noted. In accordance with prompt lines 238-239, every silently accepted misuse that can affect identity, external effects, lease capacity, mapping, or dependency direction is treated as at least P1.

| # | Attempted misuse | Required/current classification | Review result |
|---:|---|---|---|
| 1 | Call `Build` before `End` | Compile-impossible | Root builders expose no `Build`; only completion builders do. End placement still has build defense for stale graphs. |
| 2 | Use an output type inconsistent with `DurableWorkflowRef<TInput,TOutput>` | Compile-impossible | Generic reference and DAG node signatures preserve output type. |
| 3 | Empty/default/invalid definition ID or non-positive version | Factory/constructor or nearest public-call rejection | Version is exact. Null is rejected. `Guid.Empty` rejection point is ambiguous (P2-4). |
| 4 | Reuse identity/version after structural graph change | Registration/runtime typed conflict | Structural fingerprint conflict is exact. Opaque delegate/config/request code is not detectable and requires a version bump. |
| 5 | Author a durable lambda step | Compile-impossible | Durable builders expose only `Then<TStep>()`. |
| 6 | Capture mutable external state in an ephemeral lambda | Silently accepted | Benign immutable capture is valid, but an effect-driving mutable closure is indistinguishable and therefore P1-11. The separate `async void` overload trap is P1-2. |
| 7 | Use nested `While`, nested `ForEach`, `WhenFirst`, Saga, `RunExternalJob`, `RunChild`/`RunChildren` | Compile-impossible plus stale-plan compiler defense where relevant | Deferred/absent by positive allowlist. |
| 8 | Use `WaitLong` or author `Yield` | Compile-impossible | Removed rather than deferred. Runtime fairness remains internal. |
| 9 | Attach retry/timeout after `End` | Compile-impossible | Completion builder exposes only `Build`/`TryBuild`. |
| 10 | Attach retry/timeout to `Wait`, a join, or an empty branch | Fluent-call rejection | The builder shape permits some calls syntactically, but lines 553-555 require immediate rejection unless the previous node is a business step. |
| 11 | `WhenAll` with one failed branch/item | Runtime scope failure after all finish | No sibling auto-cancel; merge does not run. Multiple failures aggregate as `SFE-JOIN-FAILED`. |
| 12 | `WhenAllOutcomes` merge followed by an `If` that rejects the summary | Runtime application failure or typed rejected output | A named step may throw/fail after the `If`; explicit `StepResult.Failed` authoring is underspecified by P1-9. |
| 13 | Unbounded durable `ForEach` | Compile-impossible | `ForEachOptions` is mandatory. |
| 14 | `ForEach` count above `MaxItems` or unsupported codec shape | Runtime defense before partial copy/admission | Required at matrix lines 734-743. |
| 15 | Empty durable `ForEach` | Valid; one empty ordered merge | Explicit non-misuse. |
| 16 | Positive host execution-path ceiling lower/higher than item/node cap | Valid configuration | Effective item/node concurrency is the lower applicable limit; startup rejects only non-positive values. |
| 17 | Parked item treated as free admitted capacity | Runtime accounting defense | It releases a path token but remains an admitted nonterminal item. |
| 18 | Host path ceiling of one during fan-out | Valid; parent releases before child admission and reacquires for merge | Explicit deadlock-free model. |
| 19 | Reuse `EventId` with changed normalized content | Runtime `EventConflict` | Same target/same bytes is `Duplicate`; changed content never resumes. |
| 20 | Register two active waits for one `(DefinitionId,EventName,CorrelationId)` | Runtime/fluent deterministic rejection before parking | `AmbiguousWaitRegistrationException`; durable index is reconstructable. |
| 21 | Attempt definition-targeted event fan-out | Compile-impossible | Only instance and unique-correlation routes exist. |
| 22 | Use `AttemptNumber` as an external idempotency key | Silently accepted | It is a plain diagnostic `int`; the adapter can misuse it and duplicate/misassociate external work (P1-11). |
| 23 | Author/reuse one operation ID for another loop/item/branch/generation | Runtime authoring is impossible; cached-ID adapter misuse is silently accepted | Runtime-created IDs are distinct, but application code can cache and send an old ID for another occurrence (P1-11). |
| 24 | Point/fiber lifetime lease, author TTL, renewal, holder ID, or force release | Compile-impossible | No such overload/member exists. |
| 25a | Call `ResourceLeaseRequest.Create()` with no first requirement | Compile-impossible | The factory requires `first`. |
| 25b | Pass null/duplicate requirements or non-positive units | Factory-call rejection | Requirement/request factories validate and copy their inputs. |
| 26 | Swap `TransientPoolName` and `ResourcePoolName` | Compile-impossible | Strong role-specific values. |
| 27 | Acquire under a live lease ancestor | Intended compile-impossible plus compiler/runtime defense | **Currently silently accepted through P1-1's leased nested join escape.** |
| 28 | Sequential root-loop lease scopes or independent sibling scopes | Valid | Exact prior release/independent fiber ownership required. |
| 29 | `ContinueAsNew` inside a leased/nested body | Compile-impossible; stale-plan build/runtime defense | `SFE-AUTH-LEASE-003`/`SFE-RUN-001`. Finite conditional rollover remains an explicit usability question (P2-2), not a current nested-member allowance. |
| 30 | Trusted reconciler confirms stop based only on time, delete ack, terminal status, or liveness guess | Silently accepted privileged attestation | Core receives only token plus confirmation ID and cannot inspect the caller's evidence. This is the accepted trust boundary but remains a P1 lease-capacity residual under the prompt (P1-11); discovery is separately missing (P1-5). |
| 31 | Confirm with mismatched token, stale ID, or ID bound to another token | Typed runtime status | `TokenNotFound`, `AlreadyConfirmed`, or `ConfirmationConflict` as applicable. |
| 32 | DAG `OutputOf` with wrong type or resultless node | Compile-impossible | Generic/non-generic node references provide static defense. |
| 33 | DAG `OutputOf` for undeclared/non-direct same-typed node | Promised build diagnostic | Not implementable through opaque `Func`; P1-3. A runtime-before-child-start check is the available fallback unless API changes. |
| 34 | Caller supplies ready/completed node sets or authors a public child node | Compile-impossible | Runtime owns progression; child bridge is internal/friend-only. |
| 35 | Add OrcaCore/`OrcaCore.Dag` reference to Kubernetes/AWS/companion/Job DTO | Architecture/package guard | Dependency direction is clear; current root `src/` scan found zero forbidden SDK/domain hits. |
| 36 | Use catch-all `AddOrcaCore`, separate hosted-service toggle, or codec replacement | Compile-impossible in final surface | Supporting hosting test document is stale (P2-5). |
| 37 | Put transient pools in durable options or durable pools in ephemeral options | Compile-impossible | Separate options types. |
| 38 | Configure host-wide advancement/general-body ceiling, fail-fast/capacity-wait timeout, or custom transient SPI | Compile-impossible/reflection absence | Explicitly deferred/absent. |
| 39 | Stack multiple transient pools on one step | Fluent-call rejection | At most one decorator per preceding step. |
| 40 | Expect `.Then<A>().WithTransientPool(pool).Then<B>()` to bind `B` | Documented preceding-step binding; guard must prove runtime binds only `A` | No following-step decoration exists. |
| 41 | Configure a throttle by base type/selector scope rather than exact `TStep` | No alternate API; exact supplied type only | `For<TStep>` is exact-type keyed and does not expand to derived/assignable types. |
| 42 | Register DAG hosting without the durable engine role | Registration/startup diagnostic | `AddOrcaCoreDag` must fail startup rather than add a hidden durable role. |

## 7. Durable lifecycle analysis

### 7.1 Operation identity and attempt state

The intended sequence is coherent until it intersects leasing:

1. Before a business-step invocation, the runtime persists or deterministically derives one occurrence coordinate and `StepOperationId`.
2. Retry, replay, expected-version conflict, host replacement, and competing drivers reuse that operation ID; each invocation increments positive `AttemptNumber`.
3. Loop re-entry, another branch/item, and a continue-as-new generation derive another ID.
4. Each attempt receives a fixed-codec-detached copy. Only a winning `Completed` or committed dynamic wait transition installs replacement state; failed, timed-out, and late copies are discarded.
5. External adapters perform create-or-observe with `StepOperationId`, not `AttemptNumber`.

This closes duplicate external creation under ordinary retries when the adapter obeys the contract. It does not close a retry inside a protected scope because P1-4 leaves the token's held/quarantined ownership unresolved, and it cannot prevent an adapter from using `AttemptNumber` or a cached prior operation ID (P1-11). The contract should also say when the operation coordinate commits relative to the first invocation and how a crash after ID allocation but before invocation is observed; current wording implies persistence/derivation but the guard should name that boundary.

### 7.2 Deadlines and late execution

- `CompleteWithin` is one start-relative absolute deadline across admission, retry delay, waits, lease queueing, restart, and every continue-as-new generation.
- A structural wait races event, wait timeout, and workflow deadline through one serialized winner; losing obligations cancel before progression.
- `WithStepTimeout` fences only one attempt, signals its token, discards its copy, and may start a retry with the same logical operation ID.
- A token-ignoring body may continue physically without a path token/commit authority but retains its step-throttle/transient slot.

These are honest semantics. The missing part is the lease transition for a still-running protected attempt and its retry (P1-4). The async-void overload also bypasses all of these otherwise sound rules (P1-2).

### 7.3 Durable `ForEach`

The selector may re-run only before a winning snapshot commit. The runtime validates count and codec, copies/detaches the complete finite list, commits it, and only then admits items. Stable identity is `(scope occurrence,index)`; item state/result, admission position, and terminal outcome survive restart. Ordered merge runs at most once, including the valid empty case. `WhenAll` fails after every item terminates if one failed; `WhenAllOutcomes` merges success/failure data. An ancestor terminal winner fences active items and suppresses merge. Parent path-token release/reacquisition plus separately counted admitted items makes ceiling-one progress sound. No blocking defect was found in this state machine.

### 7.4 Lexical durable leases and governance

The intended state model is:

```text
queued (0 reserved)
  -> pending-commit (reserved)
  -> held (reserved)
  -> marked / ambiguous (reserved)
  -> released (0 reserved)
  -> quarantine (reserved) --trusted exact proof--> released
```

Whole multi-pool requests grant atomically. Workflow pending obligation, governance reservation, workflow activation, and governance owner confirmation form a four-stage idempotent handoff. Normal/definite exit releases before parent continuation; ambiguity retains capacity. Review deadlines only mark. Downward resize creates debt and revokes nothing. Release may directly transfer to a FIFO waiter, so conservation—not return to a prior global availability snapshot—is the contended invariant.

The model is not ready because:

- P1-1 lets a public fluent chain violate ancestry;
- P1-4 does not place retryable timeout/process loss in one unambiguous state;
- P1-5 gives operators no complete discovery route;
- P1-7 gives guards no exact ownership facts or deterministic handoff barriers;
- P1-8 leaves resize conflict unrepresentable; and
- P1-11 leaves the trusted stop caller's causal proof outside runtime enforcement or a declared certification boundary.

Grant/cancel and normal-release/confirmation must serialize on the same governance aggregate. A confirmation must compare token, obligation, every ticket/pool/unit, provider generation, and confirmation binding, then release at most once; these checks cannot prove that the trusted caller actually stopped external work. Missing provider tickets must terminalize the owner as `LeaseLost`, never silently reacquire.

### 7.5 DAG execution

After direct dependencies succeed, the pure node projector may retry before one fixed-codec input commit. That committed input determines `MappedInputFingerprint` and precedes one deterministic internal child start. Duplicate drive or host replacement reattaches the same child. Failed/timed-out/terminated/cancelled child status maps to stable failure codes; transitive dependants become `DependencyBlocked`; independent ready/running nodes continue. `MaxConcurrentNodes` counts started nonterminal children separately from child path tokens/resources. DAG cancellation stops new admission, propagates to running children, and waits for their terminality before run cancellation.

The runtime lifecycle is coherent. The authoring-time direct-dependency guarantee is not: an opaque mapper cannot be exhaustively inspected (P1-3). Adopt structural mapping or move that one check to deterministic pre-child runtime validation.

## 8. Phase 0 guard-coverage/readiness matrix

| Task | Required executable coverage | Current readiness |
|---|---|---|
| 3.1 | Exact public/reflection/source baselines, tier graph, strong construction families | **Blocked.** Freezing the current companion would approve P1-1/P1-2; `OrcaCoreException` and package ownership are missing (P1-6/P1-9). |
| 3.2 | Clean packed consumers for every role/meta/companion | **Blocked.** No normative PackageId/reference manifest or complete production provider registration (P1-6). |
| 3.3 | Provider/custom-host edge, governance store, record validation | **Partially draftable.** Store load/append is exact; package manifest and stream-level copy/sequence validator are not (P1-6/P2-7). |
| 3.4 | Every concrete builder/join/completion signature and diagnostic parity | **Blocked.** The exact surface contains the unsafe leased join and async delegate, omits the base failure declaration, and lacks a complete diagnostic catalog (P1-1/P1-2/P1-9/P2-3). |
| 3.5 | Strong values, codec, structural fingerprint/version discipline, detached state/projections | **Conditionally draftable.** Normative behavior is mostly exact; clarify `Guid.Empty` and correct supporting fingerprint claims (P2-4/P2-5). |
| 3.6 | Ordered joins, failures, empty/bounded `ForEach`, merge suppression, path tokens | **Ready in isolation.** No planning blocker found. |
| 3.7 | Exact handles/events/hosting/options; absence of bulk/catch-all/provisional controls | **Blocked.** Ephemeral registration does not say it supplies the mode-neutral event client (P1-10); bulk and hosting supporting inputs also need routing/reconciliation (P2-5). |
| 3.8 | Typed journeys, event routes/statuses/dedup/non-consuming behavior/continuation | **Blocked.** Task/matrix select the guardable non-consuming `NoActiveWait` behavior, but a complete ephemeral event journey has no declared client/owner (P1-10); canonical mailbox text remains P2-1. |
| 3.9 | Retry/deadline/operation identity/late overlap/competing drivers | **Ready in isolation.** Ordinary retry, deadlines, operation identity, late-result fencing, and competing-driver semantics are exact. Protected-work timeout ownership belongs to 3.11b, not this lane. |
| 3.10 | Complete typed DAG build/operation/mapping/reattachment/friend boundary | **Blocked.** Required build-time direct-dependency check is impossible through the approved delegate (P1-3). |
| 3.11a | Lease factories/admission/replay/ancestry/loop/sibling/rollover defense | **Blocked.** Public chained acquisition escape and location-summary conflict (P1-1/P2-5). |
| 3.11b | Exact release and capacity-reserving quarantine before progress | **Blocked.** Retryable timeout/process-loss transition is contradictory (P1-4). |
| 3.11c | Confirmation matrix, reconciliation, release-gap/missing-ticket, no time reclaim | **Blocked.** No complete obligation discovery-to-confirmation seam (P1-5). |
| 3.11d | Aggregate/provider crashes, ownership equality, conservation, resize/tombstones | **Blocked.** Missing deterministic facts/gates and resize-conflict result (P1-7/P1-8). |
| 3.12 | Run all lanes, exact counts/blockers, refreshed status, independent approval | **Blocked.** It is the execution/re-review gate and cannot run faithfully while upstream guards require invention. |

**Explicit answer:** unresolved decisions and exact-contract defects still block guard implementation. A guard-only agent does **not** have sufficient authority to proceed across all 15 tasks. Individually ready lanes such as 3.6 and 3.9 do not make a coherent all-15 packet ready. The smallest safe packet is a planning-only correction followed by another independent review; task 4.0 remains blocked.

## 9. Validation and repository scans

All commands ran from `X:\Projects\GitHub\Workflow-orca` against the hashes at the top of this review.

```powershell
openspec.cmd validate reshape-developer-facing-interfaces --strict
# Change 'reshape-developer-facing-interfaces' is valid
# exit 0

openspec.cmd validate add-runtime-concurrency-limits --strict
# Change 'add-runtime-concurrency-limits' is valid
# exit 0

git diff --check
# exit 0; LF -> CRLF working-copy warnings only
```

The bounded file audit found **65/65 listed files present** and read the current content of each. The source dependency/signature scan found zero root-`src/` hits for `Kubernetes`, `KubernetesClient`, `AWSSDK`, `Amazon.EKS`, `EKS`, `batch/v1`, `JobDto`, or `SchedulerDto`.

A temporary local Roslyn syntax-tree harness parsed every `csharp` fence in this review: **6/6 fences, 0 syntax errors**. The harness and its build artifacts were removed after the check. Review-output hygiene also found zero trailing-whitespace lines and balanced Markdown fences.

Repository-wide removed/deferred-name scans intentionally still find the known provisional implementation and historical/planning references:

| Pattern | `src`/`tests`/`samples` hits | `docs`/`openspec` hits |
|---|---:|---:|
| `WaitLong` | 25 | 247 |
| `Yield` | 66 | 176 |
| `WhenFirst` | 68 | 232 |
| `RunExternalJob` | 58 | 72 |
| `RunChildren` | 197 | 165 |
| `RunChild` | 322 | 203 |
| `Saga` | 99 | 720 |
| `AddOrcaCore(` | 19 | 5 |
| `AddOrcaCoreHostedServices` | 13 | 12 |

Those counts are current-implementation/context evidence only. The prompt explicitly states source and guards are stale, so they are not reported as new planning findings. I did not build the known-stale product/guard projects and did not claim an expected-red count.

## 10. Required disposition

Keep task 4.0 and all product implementation blocked. Before a guard-only packet:

1. repair the two unsafe exact signatures (P1-1/P1-2);
2. choose inspectable versus runtime DAG mapping validation (P1-3);
3. close retry/late-attempt lease transitions and recovery discovery (P1-4/P1-5);
4. approve the package manifest and deterministic provider-certification seams (P1-6/P1-7);
5. declare resize conflict and application failure contracts (P1-8/P1-9);
6. assign ephemeral event-client ownership and explicitly govern the remaining trusted integration risks (P1-10/P1-11);
7. reconcile the pre-wait event text and document finite/perpetual continue-as-new intent (P2-1/P2-2);
8. reconcile the remaining P2 supporting/canonical contradictions; and
9. strict-validate, diff-check, then obtain a fresh independent planning/guard-readiness review.
