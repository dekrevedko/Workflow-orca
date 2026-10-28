# Issue #13 task 2.2 R1 review request — 2026-10-25

Review task 2.2 only: the metadata API, the approved converter catalogue amendment,
behavior-first controls, the F2 failure-corpus resolution and matching canonical/
numbered/API/compile/package/provenance transitions. The whole task-2 target
remains uncommitted and incomplete; no task-2.3/native/publication advance.

- Base/HEAD: `10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd`.
- Exact 94-path [manifest](task-2-2-r1-review-manifest-2026-10-25.txt),
  including itself; candidate tree `2e276cbeddf908ff41d75c762582fec80e6737b8`.
- Content SHA-256: `45057db2f403190259b54c30a66949a5c2f383d20c3c55b07f2beb96348ff27b`.
- Porcelain SHA-256 at freeze: `537c7ea37c54fdcf8d807b952843f69346fe183f8beb0e1fe6c607af33b4aa52`.
- Content rule: ordinal UTF-8 repository-relative path, NUL, raw bytes, NUL.
  This request and the forthcoming verdict are excluded from the target/hash.
  The author index is empty. A temporary index verified all tree blobs and was
  removed; no source checkpoint was created.

[Implementation/evidence](task-2-2-r1-implementation-evidence-2026-10-25.md)
and [ReSharper triage](task-2-2-r1-resharper-review-2026-10-25.md) describe scope,
red/setup/green distinctions and limits. Exact compact receipts are retained
under ignored `.artifacts/publishing-validation/issue-13-task-2-2-r1/`.

Final checks: OpenSpec 25/25; Release 0 warnings/0 errors; Infrastructure 296/296;
six CI unit projects with coverage 969/969; 33 external PackageReference-only
process cases with both golden comparisons green; exact declaration and primary
package builds 0/0. ReSharper valid solution/consumer-graph reports both have zero
errors, with retained/triaged warnings. Five guard mutations reject and restore.
No native host, SQL live integration, runner migration or support claim.

Provenance: 206 rows (195 synchronized / 8 superseded / 3 outside / 0 pending),
24 immutable predecessor artifacts; exactly three approved delta/canonical block
hashes change. Test inventory 364 files / 1,510 declarations, active 212 / 822;
all 866 retired rows remain unchanged and uncredited. Only 2.2 is newly ticked;
2.3 onward is open. Source/native approval fields remain null.

Review against the earlier partial source tree ec1d2cad…: its twelve metadata
source paths are unchanged; this remediation changes only the factory's access
keyword and seven Ephemeral failure-publication paths. Read the new failure
aggregation/public snapshot flow and all new/amended controls, confirm no
assertion weakening, inspect the exact catalogue/approval binding and ensure no
broad DAG/source exemption or false native credit. The old rejected review and
all prior dated evidence remain unchanged; the Task-7.3 documentation table is
maintained live accountability, with only owned rows/digest refreshed.

This is a separate self-review pass, not an external reviewer or user source
approval. Even a favorable result leaves task 2 uncommitted and its later gates
open. Remove the owned E:/oc13t22r1 validation worktree/cache after review;
current author deliverables and compact receipts must remain available.
