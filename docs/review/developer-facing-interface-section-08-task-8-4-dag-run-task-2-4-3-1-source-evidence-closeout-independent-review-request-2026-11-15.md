# Independent review: DAG Task 2.4/3.1 source-evidence closeout

Date: 2026-11-15. Main checkout, branch codex/dag-task-2-4-3-1-closeout.
Parent: da1a4729ea2cdb9474eb219eb507760574f83dcb; parent tree: d59fbdb4cd1183de9c85a0431b62f5c518442791.
Review this [self-inclusive dirty manifest](developer-facing-interface-section-08-task-8-4-dag-run-task-2-4-3-1-source-evidence-closeout-dirty-manifest-2026-11-15.txt) as one target.

## Authorization and scope

Task 2.4 records the five independently approved source and regression slices in Git.
Task 3.1 presents the evidence inventory for review, but remains open: the inventory is complete
as a disposition, not as acceptance/provider qualification. Task 3.2, the registry Complete
transition, reshape 8.4/8.5 closure, public Task 8.6, and archival are not part of this request.
No product source, public API baseline, canonical spec, delta, waiver, expected-red disposition,
or independently reviewed packet changes. The four run rows stay ApprovedPending.

## Review questions

1. Does the [source evidence inventory](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-2-4-3-1-source-evidence-closeout-2026-11-15.md) bind each of the five checkpoints,
   reviewed trees, direct-child evidence commits and immutable verdict bytes? The canonical gate
   verifies the single-parent checkpoint, exact tree, direct-child verdict addition and current ancestry.
2. Does each DG-P01–24 row claim only the assertions the cited provider/Hosting tests exercised?
   In particular, the two-host public DG-P15 scenario cannot run without Task 8.6, and the covered
   one-host scheduler, fences and recovery cases do not qualify every controlled-clock, production
   crash, two-host ordering, or observer/wake failure schedule.
3. Does the status refresh accurately describe the checkpointed internal slices and outstanding
   qualification? Task 7.3 rows 1, 6, 7, 9, 12, 15, 16 and 22 and the artifact hash are refreshed
   only for the exact edits shown here. Six numbered docs pinned by the separate ephemeral-failure
   settlement approval stay byte-exact. Reshape Task 7.20 rewords the checkpointed slice status
   without changing its counts or checkbox. No task other than 2.4 is checked by this target.
4. Are the old review records, earlier contract artifacts, provider/source scopes and task gates intact?
   Future targets may not read these status changes as public implementation, final approval or waivers.

## Validation scope

Debug/Release solution builds use plain -warnaserror; Hosting and strict OpenSpec are rerun.
The focused Release provider filter FullyQualifiedName~Dag passed on InMemory (24 selected cases),
PostgreSQL (20) and SQL Server (20), including all 14 named shared DAG cases on each. The wider
filter also selects some related cases by substring. The two SQL runs use real containers.
The new review binding is guard-only. The handoff reports the final guard counts and post-validation
drift check. No claim of a full provider suite rerun or committed checkpoint rehearsal is made.

## Freeze anchors and next action

- Manifest: raw Git porcelain v1, all untracked paths, no renames, LF and final LF; the NUL
  form converted to LF must match. Exact tree and full manifest/active digests are in the handoff
  to avoid self-reference. Rehearse staging only these paths into a copied index.
- Semantic record: exclude docs/review/**, immutable-document-history.json and
  review-manifest-provenance.json. Ordinal-sort path<TAB>raw byte count<TAB>lowercase SHA-256 rows
  and join with LF and final LF. 16 rows / 2042 B / 936045b162ec01fee41d763da331bff99bed20ec497a39135d501b37956b5f19.
- Active record: ordinal-sort XY<TAB>path<TAB>raw byte count<TAB>lowercase SHA-256, LF and final LF.
  Exclude only the self-referential review-manifest-provenance.json.

Leave this combined target uncommitted pending a new independent verdict. A later APPROVE
can authorize the exact checkpoint and its direct-child evidence; it cannot activate the
registry, close any remaining acceptance/qualification task, or start public Task 8.6.
