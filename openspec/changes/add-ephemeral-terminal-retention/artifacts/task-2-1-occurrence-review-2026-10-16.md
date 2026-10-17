# Task 2.1 occurrence review and carried findings

Base: `f1eb86e31306d0aab88d8f84d38c925ef2eaf4c7`. This is a transition draft for
independent review, not a completed task or source qualification. The original R1 occurrence
record and all earlier dated contract/request/verdict artifacts remain byte-exact.

## Individual review record

`task-2-1-occurrence-ledger-2026-10-16.tsv` contains exactly 499 individually written
decisions over the original 54 paths, in the original occurrence order. There is one header
and one data row per original hit. Each row carries its baseline path/line/heading and literal,
the decision and its own rationale, and its current line/heading/literal or replacement range.
Backslash, tab and newline characters in cells use `\\`, `\t`, `\n` escaping.

The decisions are 449 Retain, 33 Augment and 17 Edit. Retain means the occurrence's meaning
continues to apply, often because it concerns durable storage, a strong-value/API identifier,
or historical repository material. Augment means an adjacent rule or new section now supplies
the lifetime qualification. Edit means its surrounding rule or declaration needed a change.
These semantic decisions are distinct from literal-line matching: thirteen original matching
lines are replaced, while other edits change adjacent lines or add a full new requirement.
The ledger includes exact old/current text so this distinction is reviewable; it does not
claim that all 499 lines needed mutation.

Every rationale was written for its occurrence, rather than selected from the former
file-wide template. The mechanical step only joins those decisions to committed source
and maps unchanged lines/replacement ranges using the file diff. All 499 rationales are
distinct. Check each decision and surrounding passage independently; uniqueness or a green
guard does not prove its semantic correctness.

The original record remains 499 rows / 128,197 bytes /
`6d19956655c8951c72517204b460437916e5172d135b56bb45f91418491c5e4b`.
The new ledger is 499 data rows / 294,432 bytes /
`d275769fae1798364fcad275747ddb8fdd579b4a06179d75376acea080cc78df`.
To reproduce the source binding, split each original row on tabs, read that path at the
base commit, and compare its one-based line with the ledger's baseline literal. Compare
each current literal/replacement against the working path, then inspect the rationale and
the full diff. The baseline never depends on shifted working line numbers.

## Numbered-document decisions

| Document | Disposition and reason |
|---|---|
| 01 | Retain: durable activation caching and restart goals are already mode-qualified; broad public archive/purge stays deferred. |
| 02 | Qualify the StartOrGet lesson: ephemeral key reuse lasts through retained-unit lifetime; acceptance does not reserve a wait and replay can repeat effects. Prior-art descriptions stay intact. |
| 03 | Distinguish durable activation/rehydration from ephemeral lifetime, qualify queryability and start-key lifetime, and classify internal lifecycle observations. |
| 04 | Add the retained-admission/output-pin and provisional-start-token cross-reference; no core graph/step identity is changed. |
| 05 | Retain: these hits concern durable persisted waits, correlation and start-or-deliver input; no ephemeral durable-inbox promise is introduced. |
| 06 | Preserve durable history/cleanup rules; qualify the explicitly shared DU-053 start paragraph with ephemeral lifetime and admission references. |
| 07 | Retain: Saga remains deferred and its ephemeral limitations are unaffected. |
| 08 | Retain: DAG child keys are durable lineage/admission records, not ephemeral retained-result policy. |
| 09 | Add MG-054–058; qualify MG-003/010/030/032/050/051/053 and distinct-limit prose; classify MG-020 post-removal evicted observation. |
| 10 | Add policy ownership/fingerprint and mandatory abort-cleanup scope under PR-040; provider maintenance/tombstone contracts stay durable. |
| 11 | Separate durable resident-activation caps from eligible-terminal count; neither becomes an ephemeral active-work admission ceiling or byte bound. |
| 12 | Add AC-533–542 for the policy, physical ownership/abort, coherent cleanup, count/age, pins, start-to-wait gap, key expiry, consumer qualification and telemetry. All are source-pending. |
| 13 | Record that automatic host policy is outside deferred destructive management commands and keep source/qualification gates pending. |
| 14 | Retain: scheduler starts and resource lifetimes are durable; Job effect deduplication and stop proof cannot expire through ephemeral policy. |
| 15 | Add OB-026's internal-abort origin and post-removal observation rules with bounded diagnostic categories. |
| 16 | Retain: durable-driver persistence, start input, protocol retention and restart gates are unaffected. |
| 17 matrix | Add full policy/host-options signatures, namespace/assembly ownership and retention-versus-capacity taxonomy; qualify shared start/wait prose and source status. |
| 17 authoring companion | Explicitly exclude: it declares authoring/builders but no host options. No isolated policy declaration or executable expectation is added there. |

The new canonical blocks are exact working-delta copies: one developer-facing requirement
and four management requirements. Every pre-existing canonical requirement and preamble
remains unchanged. Canonical wait/query requirements in other capabilities remain intact;
the added management scenarios specify the retained-admission boundary and new not-found
case, rather than changing the notification semantics of an already-admitted waiter.

Guides now state that not-found after Accepted is not proof that no work ran, that an expired
key can run effects again, and that the retained count should exceed completions possible
during the start-to-observe gap. They document one-tick validity/overhead, accepted-instance
null defaults, and mandatory abort cleanup in both policy modes. Prerelease notes continue
describing current binary limitations and do not claim the pending policy is available.

## Five carried P3s

| Finding | Treatment in this target |
|---|---|
| Master drift | Base stays at f1eb86e. Read-only remote verification still reports master `1c86c239fe11b893cc9bd526ce2cf64472323024`. No merge/rebase is included. Source integration with upstream needs a new freeze and independent review. |
| Proposal omission | The What Changes bullet now explicitly includes settlement cleanup for aborted unaccepted starts with retention disabled. |
| Omitted-policy scenario | The developer-facing delta and its canonical copy now narrow unchanged lifetime to accepted instances and retain mandatory provisional cleanup. This clarification is part of the present review. |
| Termination telemetry | MG-020, OB-026 and AC-542 classify internal abort origin separately from application termination command counts/duration. Sharing a fence primitive does not create a caller-issued command; source evidence remains pending. |
| Template occurrence dispositions | The new ledger replaces current routing claims with 499 separate source-bound rationales. The earlier R1 file remains immutable historical evidence; this review must assess each new entry individually. |

## Process prerequisites carried in the same target

Two permanent source-pending registry entries bind the actual contract checkpoint/verdict,
all five identities, numbered/acceptance paths and still-open source tasks. They explicitly
leave canonical/source approval evidence null; neither the working sync nor green validation
approves those stages. Current provenance records zero unresolved operations, with all
predecessor provenance artifacts preserved and catalogued.

The Task 7.3 live documentation table refreshes only its permitted current-content hash cells
and digest. The historical review-manifest fixture drops only the now-stale current-worktree
match for the source map, using its existing refresh script; historical rows stay untouched.
The normative ownership guard checks the exact amended hosting row without changing shipped
API baselines. The pending DAG freeze guard continues forbidding product/DAG-delta/API edits
and allows only these two canonical append targets, checking every pre-existing byte and the
exact ADDED-block composition. Whole-canonical guard pins still cover all fourteen files.

No product source, public API baseline, positive compile fixture, package-source record,
authoring companion, other issue, remote branch, GitHub comment or issue status changes here.
Tasks 2.1 and 2.2 remain unchecked until their reviewed checkpoint/activation is authorized.
