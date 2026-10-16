# DAG run fingerprint storage-boundary contract amendment independent review verdict

**Date:** 2026-10-15
**Reviewer:** independent review
**Scope reviewed:** the fourteen-entry freeze on base `e634737420a490acceffae56c338ac9a20051b48`
(tree `1ba8709b…`), named by
`developer-facing-interface-section-08-task-8-4-dag-run-fingerprint-contract-amendment-dirty-manifest-2026-10-14.txt`.
It contains:
- the dated fingerprint contract addendum (15,349 B);
- a replacement full API catalog, replacement standalone signatures and a rehydration proof;
- appended proposal, design and tasks notes;
- a `Proposed` registry record with source-pinned artifact hashes and new guard checks;
- the request, the manifest and the freeze/history fixtures.

**Authorization requested:** checkpoint of this contract-only target and its direct-child
evidence. There is no activation.

This verdict approves the addendum and its six-key API refinement as the contract the next Task 2.1
source target must implement. It authorizes that checkpoint and the evidence child, in the sequence
the request states. It authorizes no product source, canonical sync, task checkbox or friend.

## Summary

The amendment fixes a real defect in the approved RC-3 contract, and it does so the right way.

- **Defect.** RC-3 puts `DagRunRecordCodecV1` in Runtime.Protocol, but its records carry
  `DefinitionFingerprint` and `PayloadFingerprint`, whose constructors are internal to `OrcaCore`.
  Runtime.Protocol, the providers and Durable.Hosting are not `OrcaCore` friends, so neither the
  decoder nor any provider load could be written. RC-3's signature proof never constructed them.
- **Fix.** The two storage fields become non-null strings: exactly six API keys change, every other
  key and all totals (7 / 193 / 165) are unchanged. Typed values are created only inside existing
  friends (Dag, Engine.Durable), following the existing workflow protocol, which already stores
  fingerprints as text.
- **Rules.** Exactly 64 uppercase hex characters, no normalization, scoped to these two fields only.
  Shape is not authenticity: the binding is compared ordinally with the installed definition, and
  the value text must equal the SHA-256 of the exact stored bytes.
- **Coverage.** The addendum inventories every reconstruction path (decoder, checkpoints,
  InMemory, both relational providers, the four ports, append/replay and the Hosting bridge) and
  records why the other options (a new friend, a public factory or converter, reflection, moving
  the codec) were rejected.
- **Evidence.** Every anchor reproduces, every lane is green, my own build of the proof behaves as
  claimed, and every targeted control is red.

There are no findings.

## Method

Validation ran in one disposable detached worktree at `e634737`, and probes in a second.

- Every script asserted its disposable location and that the main `HEAD` was still `e634737`.
- Main was never modified, and the review created no ref in the reviewed repository.
- The catalog and signature diffs, provenance, current-match refresh and artifact hashes were
  recomputed with my own scripts, and I compiled and ran the proof myself.

### Environment

- **Disk.** Drive X: filled up during my first validation run. The failures were confined to my
  disposable copy, which I deleted; the frozen target, the index and the repository were verified
  intact (anchors identical, `git fsck` connectivity clean). Validation and probes then ran from
  disposable worktrees on drive E:.
- **Reviewer slip.** I first built the proof inside the validation copy's `artifacts/` folder,
  where it was not git-ignored, so the freeze-manifest guard correctly flagged one extra untracked
  file. I removed it and reran the full guard project on the restored frozen state; that clean
  result is the one reported below.

## 1. Freeze anchors

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 14 lines, 1,336 B, `af5052d4…` | identical (`af5052d400a4c2aecaa492f4de05a1efc6831aa9afd89dcb0623402d824d8884`); the NUL form has 14 entries (`fede2106…`); diff (8) plus untracked (6) paths are exactly the 14; nothing staged |
| Semantic record: 10 rows | 1,575 B, `d2e08b5d…` | identical with the stated recipe |
| Active record: 13 rows | 2,174 B, `ee0efb58…` | identical (`ee0efb58604f5311a6116119a6e044af432b7b78441dec2ed151899c77b2f752`), equal to the fixture |
| All-file record, with status (my form) | not embedded | 14 rows, 2,329 B, `3bd20bc644dff3d32ff3162203b0b6c45a81ac629788d7f7c4d6999d1c9e1d8b` |
| Simulated checkpoint | tree `dcf4337daffef0630f917f57d563011e43d330ec` | identical from a copied index and from a committed disposable copy; parent `e634737`; 6 A / 8 M; all fourteen blobs equal the raw bytes; committed `diff --check` clean |

- **Package-fix chain.** Checkpoint `c390a11` has tree `de81b66a`, and its only direct child,
  `e634737`, adds my round-101 verdict byte-exact (`617c6db0…`) and clears the freeze.
- **History.** The RC-3 contract (`f8a1a7f6…`), catalog (`766ad5d8…`), signatures (`8301094b…`) and
  the transition addendum (`cff6f3a4…`) are unchanged.
- **Scope.** No `src/`, canonical, delta, numbered or guide document, `CLAUDE.md` or shipped API
  baseline changes. Provenance stays 184 / 48,504 B / `fd6ad044…` with zero pending, and my
  current-match emulation gives 16 entries, 100 matches and 0 stale.
- **Registry.** `dagRunFingerprintContractAmendment` is `Proposed` with null approval. It binds the
  real RC-3 checkpoint, tree, evidence and verdict, and pins the four new artifacts (`8bf3f169…`,
  `1d6f1b6e…`, `01c186aa…`, `6035dd9d…`), all of which match. My reconstruction of its digest equals
  the value pinned in guard source (`321b8fda…`).

## 2. Contract review

| Item | Finding |
|---|---|
| API catalog | key-level diff against RC-3: Runtime.Protocol replaces exactly six keys (`DagRunBindingV1` and `DagRunValueV1` constructor, property and `Deconstruct`, typed fingerprint -> `string`, non-null); OrcaCore, Provider.Abstractions and the before/after section are identical |
| Signatures | differ from RC-3 in exactly the two parameter types; the internal typed child receipt is unchanged |
| Format rule | matches every real producer: workflow definitions (`CompiledWorkflowPlan`), DAG definitions (`DagAuthoring`) and payloads (`DurableWorkflowInputFingerprint`) all emit 64 uppercase hex characters; the `pending-definition:` prefix appears only in conflict reports, never in a binding |
| Authenticity | ordinal comparison with the installed definition and a byte-hash check before any typed value is created; corruption is not relabeled as a mapping failure |
| Normative scope | no canonical spec or numbered document names either record; the delta files carry no typed-field rule, so no canonical sync is needed |
| Boundaries | no friend, public factory, converter, reflection or `UnsafeAccessor`; typed construction stays in Dag and Engine.Durable |

**Rehydration proof, reproduced.** Compiled with the request's recipe as assembly
`OrcaCore.Runtime.Protocol`, referencing only the built `OrcaCore.dll`: the Release
`-warnaserror` build is 0/0, the program rebuilds both records across its three simulated
boundaries and rejects all 21 negative cases, the `FORBIDDEN_FINGERPRINT_CONSTRUCTORS` build fails
with CS1729 for both constructors, and normal mode is green again. The proof uses no reflection,
`UnsafeAccessor` or friend access.

## 3. Validation

SDK 10.0.402, plain `-warnaserror`.

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / green fixtures pass, exactly `dag-hosting` red |
| Compile fixtures (`Green`) / public API baseline | green / 14/14 |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 354 / 79 / 99 / 37 / 24 / 100 |
| PostgreSQL / SQL Server / Integration | 103 / 74 / 11 |
| Full guard project on the frozen state (clean rerun) | 247 passed plus exactly 14 expected-red |
| `Disposition=Infrastructure`, Release | 247/247 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / Debug / strict OpenSpec | 16/16 / 16/16 / 21/21 |
| Guards on the committed simulation | 247 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 247/247 |
| Vulnerable packages | none in 29 projects |

## 4. Independent negative controls

Fourteen cases on the corpus and identity lanes (19 tests). Every case except F0 cleared the
active freeze first; F11–F13 also edited guard source coherently and rebuilt.

| # | Mutation | Result |
|---|---|---|
| F0 / F1 | none (freeze active) / freeze cleared | green / green |
| F2 | addendum rule changed (uppercase -> lowercase) | **red**: artifact hash |
| F3 | same edit with the fixture's artifact hash refreshed | **red**: registry digest in guard source |
| F4 / F5 | registry stage forged `ApprovedPending` / approval forged non-null | **red** / **red** |
| F6 | approved RC-3 contract edited by one byte | **red**: immutable RC-3 packet |
| F7 | unrelated canonical text edited | **red**: canonical content pin and no-sync boundary |
| F8 | `OrcaCore` -> Runtime.Protocol friend added in source | **red**: no-source boundary |
| F9 | product source comment edited | **red**: no-source boundary |
| F10 | task 2.1 checked | **red**: whole `tasks.md` pin |
| F11 | typed `PayloadFingerprint` restored in the signatures, fixture and guard digest coherently refreshed | **red**: "only the two storage-record parameter types change" |
| F12 | a seventh API key added to the catalog, coherently refreshed | **red**: independent six-key count ("found 7") |
| F13 | activation `d59f830` cited as RC-3 evidence, coherently refreshed | **red**: real Git single-parent check |
| R0 | restored and rebuilt | green; worktree equals the manifest and every target byte equals main |

## 5. Notes

No findings. Recorded:

- **The new no-source tripwire stays in force after checkpoint.** The guard fails on any change
  under `src/`, canonical specs, the DAG deltas or the shipped API baselines relative to `e634737`,
  for as long as the registry record reads `Proposed` with null approval. This is intended. The
  next source target must bind this amendment's checkpoint, tree, evidence and verdict and update
  the guard before any source can pass, and so must any unrelated maintenance that touches those
  paths first.
- **DTO validation is a future obligation.** The proof validates in its readers; the addendum
  correctly requires every future DTO constructor, port and decoder to revalidate the two fields,
  since `with` and init setters bypass positional constructors.
- **Round-100 carry-forwards remain open,** as the request says: lock task 1.4 complete (its
  checkbox is still exempt from the `tasks.md` pin), test rejection before storage, and correct the
  stale `CLAUDE.md` sentence. None is claimed fixed here.

## 6. Reviewer hygiene and checkpoint sequence

`HEAD` is `e634737420a490acceffae56c338ac9a20051b48`, with the fourteen frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

Checkpoint sequence, as the request states:
1. Confirm zero drift against the 14-entry manifest and the anchors above.
2. Stage exactly those 14 paths on `e634737`, expecting tree `dcf4337d…`, and commit.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.

There is no activation. Task 2.1 and every later task stay open. The next source target binds this
approval and implements the string representation together with the round-100 carry-forwards.

## Determination

The RC-3 contract could not be implemented as written, and this amendment repairs it with the
smallest change that respects every existing boundary: two storage fields become canonical text,
strong values stay with the existing friends, and authenticity is checked against the installed
definition and the exact bytes. The proof compiles without friend access, the history is
preserved, and the controls show every decision is guarded.

**Verdict:** **APPROVE**
