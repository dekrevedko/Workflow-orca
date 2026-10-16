# Independent review — DAG runtime-view contract RV remediation

Date: 2026-10-02
Change: `admit-dag-hosting-runtime-view`, prepared Task 1.1 before Task 1.2 approval
Base: `2a09b452af067fbd501215a572712ff08cf2bbc9`
Manifest: [developer-facing-interface-section-08-task-8-3-runtime-view-contract-rv-remediation-dirty-manifest-2026-10-02.txt](developer-facing-interface-section-08-task-8-3-runtime-view-contract-rv-remediation-dirty-manifest-2026-10-02.txt)

## Authority and immutable rejected predecessor

Review this single uncommitted contract-remediation target, including RV-1, RV-2 and all
five non-blocking observations. APPROVE authorizes only this exact checkpoint; it is not
canonical synchronization, registry promotion, a ninth friend attribute, codec access,
Task 8.3 source or completion of reshape 8.4/8.5. Current compiled friends remain eight;
both runtime-view rows stay Proposed, two operations stay pending and semantic approval is false.
Task 1.1 is prepared; Task 1.2 stays unchecked in main. Only its post-approval checkbox activation
is deliberately unpinned; 1.3–3.2 remain open and guarded. The separate atomic 1.3/1.4 target
must require 1.2 complete with the actual contract checkpoint, single-parent direct-child
evidence, added byte-exact terminal APPROVE and canonical transition before any source authority.

The prior process chain remains `dc8095c5536316cb772c985641e45179fa3c93b5`
→ evidence `0f4fafa3c3b3acfdb2c39227bba53f094782bd13` → checkbox-only activation/base above.
Its 11,549-byte APPROVE is unchanged at `bc72cd0eca94e77a1c09744e18aea6bb0133cfe5429ddf5a50fdfb4e0c70cd4c`.

The original 2026-10-02 request, 26-path manifest and REJECT are byte-exact and catalogued:

- request: 11,267 bytes / `71b1999a1863557a444ff508e155a693faf4f24110d6ac35ef25565fab0e5472`;
- manifest: 1,926 bytes / `604d1d3db38fe2ff3a842f3297ea1aae373adc275e3b07b9380a11ff7d8e286f`;
- verdict: 11,680 bytes / `1ddaa7a0e9008b483c75b37c8824ce72aa506caf8d7c74e619bd8dd6063f4858`.

The rejected tree `b77b09d5aae6e23e4225b897fa351ddc9463eb4b` and original contract artifact
remain historical evidence, not approval. A rejected-freeze row independently binds all three
review files. The old contract provenance artifact is unchanged and is the sixth permanent
superseded record. The new artifacts have distinct names; no dated record was rewritten.

## Self-inclusive freeze and reproducible recipes

The manifest includes this request, itself, both history/freeze fixtures, the retained rejected
packet and every semantic target. It preserves raw `git status --porcelain=v1 --untracked-files=all --no-renames`
order. Independently intersect tracked entries with `git diff --name-only HEAD` and add
`git ls-files --others --exclude-standard`; no content-identical phantom is admitted. Verify
the NUL-form status gives the same entries. Main has zero staged paths.

- manifest paths: 31 (22 modified, 9 untracked)
- manifest bytes: 2,560
- manifest SHA-256: `b365ebad63a0217cda8c6efaf706f61673385ff95048757993ccb28a25f915ae`
- raw NUL-form bytes: 2560
- raw NUL-form SHA-256: `d412c446b7da51b89df028e3062e36ad663f31a7e68c0fcc770a473476b195cb`
- semantic record rows: 24
- semantic record bytes: 3,386
- semantic record SHA-256: `8e422828f7a927c82326a4dcffa9cb9f11a610c123ce338c5c0171598a510574`

Semantic recipe: omit all `docs/review/` paths and exactly the two fixtures
`immutable-document-history.json` and `review-manifest-provenance.json` under
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/`. Read raw bytes; render
`path<TAB>byte length<TAB>lowercase SHA-256`, ordinal-sort the complete rendered strings,
LF-join with exactly one final LF and hash UTF-8 without BOM. Do not normalize file bytes or
prepend statuses to this semantic recipe.

The stronger active record includes every manifest path except the exact self-referential
`review-manifest-provenance.json`. Render `XY<TAB>path<TAB>raw byte length<TAB>lowercase SHA-256`,
ordinal-sort, LF-join with one final LF and hash UTF-8. Its final value is pinned after this
request and the immutable catalog are complete. Publish that value and the simulated tree
outside this self-inclusive request, avoiding circular hashes. Stage exactly the manifest in
a guarded disposable copy, confirm the tree's path set and every blob against raw bytes,
and check the staged and committed diff. Do not stage or commit main during review.

## Remediation claims to challenge

1. RV-1 is reproduced, not inferred: the rejected isolated gate is green with 1.2 open and red
   with it checked. The corrected gate leaves 1.2 unpinned and accepts both states, without
   weakening open 1.3–3.2 or Proposed/pending checks. Rehearse a committed checkpoint, freeze
   clearing and checkbox-only activation with no guard-source edit; the complete Infrastructure
   lane must stay green. This simulation is not a fabricated independent approval.
2. RV-2 is self-contained in both normative trees: both proposed delta requirements carry the
   identical exhaustive three-type/fifteen-member C# contract. Doc 17 §17.2.6 has the same block
   and fifteen decoded signatures; the misplaced strong-values paragraph is moved there.
   CP-020 points at §17.2.6. No canonical successor refers to “this change’s exact contract”.
   Each signature copy is independently checked against the same guard-source signature digest.
3. The complete boundary remains closed: one plan accessor, node-list getter, evaluator,
   eight descriptor getters and four result getters; no constructors, setters, drafts,
   delegates, extra types/overloads, test friend, package edge or codec access in Dag.
4. Every non-blocking note is addressed:
   - Active-or-archived predecessor resolution belongs atomically to 1.3/1.4; actual archival
     is a separate reviewed inventory/provenance target. Withdrawal is only planned here.
   - Mapper-derived Exception, including mapper-thrown OperationCanceledException, maps to
     DAG_INPUT_MAPPING_INVALID, excluding OutOfMemoryException, StackOverflowException and
     AccessViolationException. External run cancellation retains runtime cancellation.
     Ordinary declared-type decode/materialization failure maps to DAG_INPUT_MAPPING_INVALID
     on the durable bridge before mapper/commit/start; protocol/storage failure stays runtime
     failure. These are proposed policies for later reviewed source, not implemented claims.
   - The unchanged durable-runtime owner does not prohibit successful null; this explicitly
     specializes declared-type validity without changing success/readiness, commit order or
     codec ownership. Workflow-authoring's opacity owner has no OutputOf null rule. The full
     rationale is in design §7 and the disposition; any discovered contradiction still needs
     a separately approved owning delta before source.
   - Guard-source hashes durably bind the whole numbered proposed boundary/null/failure block,
     doc 17 §17.5 friend disposition, CP-020/CP-022 and the open reshape 8.3–8.5 handoff. They
     do not depend on the temporary freeze; false status or codec reassignment stays red.
   - Doc 17 §17.5 is explicitly an amended target, current eight plus proposed ninth.
5. Present successful null is valid only for reference or nullable-value declared types;
   absent, wrong-type, foreign/non-direct outputs and nonnullable-value null remain invalid.
   Current code rejects all null and is not declared compliant; Task 2.2 owns the later source
   behavior. The durable bridge decodes committed successful direct outputs before evaluation,
   and alone owns normalization, fingerprints, input commit and child start/reattach.
6. Provenance is independently reproducible: 180 rows / 47,327 bytes /
   `79f1638357e3b1aa35aef28a4f7c45233acc135036d5f6c1b274e687d454b947`:
   173 synchronized, 2 superseded reshape predecessors, 2 pending modifications, 3 bootstrap-only.
   Six permanent superseded artifacts render 1,041 bytes /
   `0eae8ac75f83fbfcba744f736a4acd8c71acb02e7718f50e95f47cf06df69c6c`.
   Active directories remain 20 / 1,645 bytes /
   `dfabdc307ab5c49d6bc15c140b7778a5dce0512faf601c83645574fa0e770d62`.
   All canonical specs and both earlier owners remain unchanged; no source, API baseline,
   package-source fixture, counted declaration or declaration crosswalk changes.
7. All 22 Task 7.3 rows and their artifact digest are recomputed for intentional edits; the
   Task 8.0 map remains pinned. Dated decision entries and all earlier review packets stay intact.

## Verification and controls

Author validation: Debug and Release non-incremental warn-as-error builds 0/0; Core 350,
Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96; real PostgreSQL
101, SQL Server 72, Integration 11. Infrastructure 240/240, focused corpus guards 15/15,
exactly the same fourteen intentional ExpectedRed failures (full suite 240/14/254). Twelve
exact packages, eight green consumers and the documented one expected-red dag-hosting fixture.
Strict OpenSpec 20/20. The staged/committed rehearsal and committed checkbox-transition
rehearsal must also pass Infrastructure and have clean committed diff checks.

Controls against a green isolated synchronization gate:

| Control | Mutation / expected outcome |
|---|---|
| A1 | Check Task 1.2 only: GREEN; same committed-state Infrastructure after freeze clearing |
| C1 | Change design result signature: RED |
| C2/C3 | Drop a getter from either delta: RED, exact signature check |
| C4/C5 | False approved status in doc 17 or doc 08: RED, durable numbered/composition pins |
| C6 | Put codec access into reshape 8.3: RED, durable handoff pin |
| C7 | Invert successful-null artifact: RED, independent artifact pin |
| C8 | Fabricate ApprovedPending: RED |
| C9 | Check 1.3 early: RED |
| C10/C11 | Hide pending operations / claim semantic approval: RED |
| C12 | Edit preserved rejected provenance record: RED, permanent catalog |
| C13 | Relabel preserved REJECT: RED, immutable verdict evidence |
| C14 | Point process evidence at activation and rebuild: RED, exact direct-child check |
| C15 | Edit completed authoring predecessor: RED |
| C16 | Claim ninth friend approved in §17.5: RED, durable boundary pin |
| C17 | Erase mapper exception exclusions from normative rule: RED |

Restore each probe byte-exact and rebuild any mutated guard. The first draft of the new
location check self-matched its sliced heading; it was corrected before the green control
and final probes. The metadata capture helper likewise initially cut at its own sliced
requirement heading; the complete-block hashes were independently recomputed and corrected.
Neither harness issue changed any historical file or canonical/source contract.

## Verdict and sequencing

Independently challenge the exact signatures, exception/null policy, owner rationale, documentation
coverage, preserved rejection and checkbox activation. Write a new immutable dated verdict
without editing this target or prior packets. It is an extra path and belongs in the subsequent
evidence commit, not the reviewed checkpoint. APPROVE → exact manifest checkpoint → sole-child
evidence adding/cataloguing the verdict and clearing the freeze → Task 1.2 checkbox only.
Then independently review atomic 1.3/1.4. No source or ninth friend is authorized by this contract
checkpoint, and no forward work begins while this remediation review is pending.
