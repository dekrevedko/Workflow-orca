# Durable DAG child bridge contract independent review verdict

**Date:** 2026-10-04
**Reviewer:** independent review
**Scope reviewed:** the five-entry freeze on base `c85fd0f301f110db691a026bcdd0c0e35c6b26c1`,
named by
`developer-facing-interface-section-08-task-8-4-durable-dag-bridge-contract-dirty-manifest-2026-10-04.txt`.
This is the proposed reshape Task 8.4 durable DAG child bridge contract,
`artifacts/task-8-4-durable-dag-bridge-contract-2026-10-04.md` (21,406 B, `acbb4b70…5b50`), plus the
request, the manifest and the two freeze fixtures.
**Authorization requested:** checkpoint of this contract-only target.

This verdict authorizes no checkpoint, no bridge source, no 8.4–8.10 work and no archival.

## Summary

The packet is exact. It changes no product, canonical, public-API, package or task state. Much of
the contract is sound and should survive revision:

- **Package graph.** The Guid/ordinal descriptor fits the existing
  `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` friend. It needs no Dag type on the durable
  side and no tenth friend. The C# block compiles independently with only the `OrcaCore` reference.
- **Mapping and values.** These are faithful to doc 17 §17.2.6 and the open 8.4 text:
  - eager declared-type decoding of every successful resultful dependency;
  - an exact, copied map;
  - successful null;
  - the mapper exception and exclusion policy;
  - ordinary decode or normalization failure as `DAG_INPUT_MAPPING_INVALID` before
    commit or start;
  - `MappedInputFingerprint` over the committed fixed-codec bytes only (CP-022).
- **Commit and start.** Input and start intent are committed in one stream, the child start is a
  separate retryable commit, and the contract explicitly disclaims cross-stream atomicity. The crash
  points it lists are handled honestly.
- **Identity and lineage.**
  - The child identity is deterministic: a byte recipe hashed with SHA-256, checked against the
    full coordinate.
  - Every child has the DAG anchor as both parent and root; edges are not lineage.
  - Continue-as-new preserves the child.
  - No public child or enumeration API is added.
- **Evidence and carry-forward.** The §9 evidence matrix is demanding and requires both real
  providers. Round 91's notes and the stale null-status sentences are carried forward openly.

Two blocking contract defects remain. Both are in the questions the request itself asks the reviewer
to decide (Q1 and Q6), and an approval would lock them in as the "exact V1 seam" that the 8.4
source review is told to pin.

- **BR-1 (P2).** The three new DAG outbox kinds are routed into the existing external
  `IMessageDispatcher` partition. A compiled probe shows that partition silently completes a
  `dag-child-start-v1` record.
- **BR-2 (P2).** The six operations are declared "the complete proposed callable bridge", but they
  cannot create, persist, cancel, terminalize or wake DAG progression across the one-way package
  edge. The DAG-owner aggregate they commit into is never defined.

## Method

Validation ran in one disposable detached worktree. Probes and an independent signature compile ran
in a second. Both held the same five entries.

- Every script asserted its disposable location and that the main `HEAD` was still `c85fd0f`.
- Main was never modified, and the review created no ref in the reviewed repository.
- Both trees and the code were read directly for every contract clause:
  - doc 06 DU-031/032/033/043; doc 08 CP-020–028; doc 10 PR-015;
  - doc 12 AC-607–616; doc 16 DR-037/041, DR-AC-008/029; doc 17 §17.2.6;
  - canonical `durable-runtime`, `durable-persistence-and-outbox` and `workflow-contracts`;
  - the approved Task 8.0 map.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 5 lines, 534 B, `a7235ef3…04ef` | identical; the NUL form has 5 entries; the union of diff and untracked paths is exactly 5; nothing staged |
| Semantic record: 1 row | 185 B, `f498aa76…c90c` | identical with the stated recipe |
| Active record: 4 rows | 733 B, `46ef2f62…de3f` (fixture) | identical |
| All-file content record, 5 rows (reviewer) | — | 888 B, `e69f81c7821429f203176b08cd0169893a71140275805e12a919211c9c325992` |
| Simulated checkpoint | tree `80a4b5b68fc84d2b2a113ee840a487abd30a4cb1` (handoff) | identical from a copied index and from a committed disposable copy; parent `c85fd0f`; 3 A / 2 M; all blobs equal the raw bytes |

- **Closeout chain:**
  - `3270678` has tree `f675f04a` and parent `1d8941b`;
  - `496f30c` is its only child among all refs; it adds the round-91 verdict byte-exact
    (9,888 B, `25962bbf…`), catalogs it, clears the freeze and keeps the history pointer;
  - `c85fd0f` checks only runtime-view task 3.2, and equals the pushed remote branch.
- **Fixtures:**
  - the history fixture admits exactly the request (7,483 B, `bcd4da55…`) and the manifest, and
    repoints the active-freeze manifest;
  - the freeze fixture pins the active record;
  - no other bytes change.
- **Provenance:** reproduced by my own script at 180 rows, 47,347 B, `40d4d8c0…`, zero pending,
  eligible.
- **Current-match refresh:** emulated, because Windows PowerShell 5.1 lacks `SHA256.HashData`.
  16 entries, 106 matches, 0 stale.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / 8 green, exactly `dag-hosting` red |
| Compile fixtures (`Green`) / public API baseline | green / 14/14 |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 243/243 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / strict OpenSpec | 15/15 / 20/20 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation | 243 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 243/243 |
| Activation | none planned: task 8.4 stays open, and checking it is red (E5) |
| InspectCode | not applicable: the target contains no C# source |
| Independent compile of the §3 C# block, repository SDK, `OrcaCore` reference only, Release `-warnaserror` | 0 warnings, 0 errors |

The checks are green, but this is a contract review: green checks say nothing about whether the
proposed choices are correct.

## 3. Blocking findings

### BR-1 (P2): DAG child-start/join/cancel records are routed to the external dispatcher partition

**What the contract says.** §7 assigns `dag-child-start-v1`, `dag-child-join-v1` and
`dag-child-cancel-v1` to "the existing logical provider outbox and its internal dispatcher". It
states that continue and workflow-event claims stay restricted and that "internal claims exclude
both."

**What that partition is in the code.** It is the external `IMessageDispatcher` pump:
- `OrcaCoreOutboxPumpHostedService` and the `DurableOutboxPump` default claim
  `Excluding(continue, workflow-event)`.
- They hand every other kind to the registered `IMessageDispatcher`, documented as dispatching
  "to an external transport" (`ProviderPorts.cs:592`).
- Durable hosting adds that pump whenever any `IMessageDispatcher` is registered, whether or not
  the host has the DAG role.

**Consequences:**
- **InMemory.** The provider registers itself as the dispatcher, and its `DispatchAsync` records the
  write and returns `Success`. Probe D1 commits a `dag-child-start-v1` record and pumps it with
  that exact selector and dispatcher. The record becomes `Dispatched`, and a later DAG-only include
  claim finds nothing. The child start is lost silently. Its node would stay admitted forever.
- **PostgreSQL / SQL Server.** No dispatcher is registered, so nothing claims the records unless
  an application registers one, and then they go to that transport.
- **Multi-host fleets.** Any durable host without the DAG role (which is optional) runs the same
  pump and can swallow DAG starts, and the joins that child completion emits.

**Normative conflict.** This conflicts with:
- canonical `durable-persistence-and-outbox`: "Internal continuation records SHALL use their own
  runtime pump";
- the approved Task 8.0 map, which classifies these handoffs as internal continuations: "only the
  internal runtime pump may claim it, and the application dispatcher never receives it".

DR-037 and PR-015 name only the continue and workflow-event isolation, so a fourth runtime-owned
partition needs explicit wording.

**Required.** The contract must:
- reserve the three kinds to a runtime-owned include-only consumer that runs whenever durable
  engines run, not only with an external dispatcher;
- require every host's external partition to exclude them, which changes the durable engine's
  default selector and not merely the DAG host's;
- state the poison/permanent-failure disposition per kind, so a run cannot hang with admission
  held;
- add a DR-AC-029-style provider certification row;
- name the doc 16/doc 10 wording or amendment that admits the new partition.

### BR-2 (P2): the "complete" six-operation seam cannot drive DAG progression

§3 declares `ReadMappingInputs`, `CommitMappedInput`, `StartOrReattach`, `GetChild`,
`WaitForChildTerminal` and `RequestChildCancellation` "the complete proposed callable bridge".

**What the contract assumes elsewhere.** The same contract relies on state transitions no
operation performs:
- the committed run-input bytes, read at §5, and "the node's persisted direct dependency list",
  read at §3: no operation creates the run, its anchor, its structure binding or those bytes;
- "the separate DAG progression transition", which records mapping failure and the
  `DependencyBlocked` closure (§5);
- "Run cancellation is serialized on the DAG aggregate", and `RequestChildCancellationAsync` is
  "allowed only for a committed run cancellation" (§7): nothing commits that cancellation;
- "The DAG owner consumes terminal child facts": no operation names that consumer or how the DAG
  run becomes terminal.

**Why the one-way edge makes this blocking.** Canonical `durable-runtime` ("DAG hosting starts a
child") has `OrcaCore.Dag.Hosting` progressing ready nodes. Only Dag.Hosting can evaluate a mapper,
because the runtime-view friend is Dag-to-Dag.Hosting only. Durable.Hosting cannot reference
Dag.Hosting. So when a durable join makes a dependent ready, or a host restarts, the contract must
say how Dag.Hosting learns which runs and nodes need mapping (CP-024/025, AC-608/610). Options
include:
- an internal callback interface declared in Durable.Hosting and registered by `AddOrcaCoreDag`;
- a run/node state read with a recovery claim;
- something else.

CP-024 and the contract's own "never polling" rule for terminal observation rule out polling.
Whatever is chosen is part of the very metadata allowlist the 8.4 source review is told to pin.

**The aggregate itself is undefined.**
- `CommitMappedInputAsync` already performs "one expected-version DAG-owner commit" with readiness
  and admission.
- §4 places that aggregate at the run GUID as a "distinguished DAG-run record" that ordinary lookup
  must not reopen.
- The existing store models only `workflow-{InstanceId}` streams. Their events come from the fixed
  `WorkflowEventCodec` table in Runtime.Protocol, which rejects unknown types.
  Canonical "Workflow store persists first-class durable records" lists no DAG-run record, and
  first-release schemas are exact.

The contract therefore needs to state:
- which existing ports, stream identity, record types and kind discriminator carry the aggregate;
- whether that needs the canonical amendment that the approved 8.0 stop rule requires for a provider
  record outside the internal-continuation category.

**Required:** either of these:
- **Define the closed V1 seam:**
  - run create/reopen binding the structure and run-input bytes;
  - recovery and readiness reads;
  - node mapping-failure/blocked transitions, run cancellation and terminalization;
  - the durable-to-Dag.Hosting wake/recovery direction;
  - the aggregate's persistence representation.
- **Narrow the claim:** make it explicitly the 8.4 child protocol, and name the reviewed target and
  allowlist extension that will own each missing operation, its direction and its persistence.
  Remove "complete" in that case.

## 4. Independent probes

| Probe | Mutation / check | Result |
|---|---|---|
| D1 | InMemory: `dag-child-start-v1` committed, pumped with the hosted external selector and the DI-default InMemory dispatcher | `Dispatched`; later DAG include-claim empty (BR-1 confirmed) |
| E0 | unmodified target, focused `OpenSpecCorpusGuards` (15) | green |
| E1 | artifact edited by one byte, freeze active | **red**: active content record |
| E2 | request edited by one byte | **red**: immutable history and packet record |
| E3 | evidence state: freeze cleared | green |
| E4 (informational) | evidence state, identity recipe changed from "first" to "last" 32 hex characters | green: the artifact is unpinned after evidence (P3-4) |
| E5 | evidence state, reshape task 8.4 checked | **red**: 8.3–8.5 handoff pin |
| E6 | evidence state, manifest edited by one byte | **red**: immutable history |
| R0 | restored | green; porcelain equals the manifest; the 5 target files equal main byte-for-byte |

## 5. Non-blocking observations (P3)

- **P3-1: the start-key namespace is shared.** `StartIdempotencyKey.Create` accepts any trimmed
  string, and the idempotency store is one global string namespace. A caller start that uses
  `orcacore-dag-child-v1:<digest>` first would turn the DAG child start into a fail-closed integrity
  conflict. The prefix should be reserved, or idempotency should rest on the deterministic
  `InstanceId`.
- **P3-2: the null mapped-input representation is undefined.** The approved view may return a
  present null input. Ordinary durable start stores no payload for null input
  (`DurableWorkflowRuntime.cs:186`). The contract's serialize, round-trip and fingerprint step
  should define null's committed bytes and fingerprint, so that reattach compares exactly.
- **P3-3: the `MaxConcurrentNodes` scope is implicit.** "Validate … the host ceiling at that
  serialized decision" serializes only one run's stream. That is correct only if the ceiling is per
  run, which "a DAG has reached" suggests. Say so for 8.7.
- **P3-4: the contract artifact is unpinned after evidence.** Once the freeze clears, only the
  request's prose records `acbb4b70` (see the E-probes). Pin it, and link it from task 8.4, in the
  next target.
- **Recorded (still stale, not in this target):**
  - `CLAUDE.md` "Complete promotion is prepared only in the separately reviewed closeout";
  - doc 08 CP-022 "but not implemented" (disclosed in the request).

## 6. Reviewer hygiene and next step

`HEAD` is `c85fd0f301f110db691a026bcdd0c0e35c6b26c1`, with the five frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

Do not checkpoint this target. Keep this request and manifest byte-exact as a rejected freeze, and
catalog them as before. Revise the contract for BR-1 and BR-2, ideally also P3-1 to P3-4, then
refreeze for a new independent review. Task 8.4 stays open, and no bridge source may start.

## Determination

The mapping, value, identity and commit-ordering parts of the proposed bridge are faithful to both
trees. The seam does not fix two decisions this contract was meant to fix:
- DAG runtime records can be consumed by the external dispatcher;
- there is no defined, one-way-safe path that creates, persists, wakes and ends DAG progression.

Approving it as the complete V1 seam would bind the 8.4 source to an allowlist that 8.5 must
immediately break.

**Verdict:** **REJECT**
