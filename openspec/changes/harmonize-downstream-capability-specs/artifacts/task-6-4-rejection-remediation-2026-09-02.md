# Task 6.4 MaxActiveFibers rejection remediation

Date: 2026-09-02

## Supersession

This record supersedes only the rejected Task 6.4 target described by the immutable 2026-09-01
request, manifest, disposition artifact, and `REJECT` verdict. Those four files remain byte-exact.
No rejected target is presented as approved or checkpointed.

## R-1: unrelated normative content restored

The semantic appendix again contains the fan-out-rank excluded-claim bullet in its original position
between the unconditional item-admission claim and the scope-tree claim. The Task 6.4 edit is now
limited to rewriting the retired admission quantity as an explicitly searchable negative statement.

The complete `## Deliberately excluded claims` block is 1,182 UTF-8 bytes after LF normalization and
has SHA-256 `a989ad5ea0773cdd22eb13b65194651b61035eef9da469369134921b730c56b1`.
The must-green guard pins that whole block, so deleting, adding, or changing any independent bullet
fails even when the Task 6.4 sentence remains present.

## V-2: documentation inventory widened

The active-document inventory now recursively scans both `.md` and `.cs` under `docs/` and
`openspec/`, using one named extension allowlist and excluding only immutable `docs/review/`
evidence. A token added to `docs/specs/17-public-authoring-contract.cs` is therefore rejected. Test
source remains outside this documentation contract; product `src/**/*.cs` retains its separate
zero-occurrence assertion.

## V-3: closed enumeration pinned

The semantic appendix check no longer relies on one required sentence. It identifies the complete
excluded-claims section using the next `##` boundary (or end of file), hashes every normalized byte,
and compares it with the reviewed digest above. The restored fan-out-rank bullet is consequently
load-bearing rather than merely present in the remediation diff.

## Rejection evidence

The independent rejection verdict is preserved at
`docs/review/harmonize-downstream-capability-specs-task-6-4-independent-review-verdict-2026-09-01.md`:
13,640 bytes, SHA-256 `3f972188281bced25dbcd0ac0a6709b611e1660c91ff1da258c7cc46bd74a79d`.
The provenance fixture records the original ten-path raw manifest and every historical content row
under `Rejected`, then freezes this remediation separately.

## Negative controls

The remediation adds or reruns byte-restored controls for:

1. deletion of the fan-out-rank bullet;
2. any other excluded-claim block change;
3. an occurrence in `docs/specs/17-public-authoring-contract.cs`;
4. a Markdown active-document occurrence;
5. a product-source occurrence;
6. reopening Task 6.4; and
7. an immutable `docs/review/` occurrence, which remains green by design.

## Scope and accounting

No new guard fact is introduced, so the crosswalk remains 337 physical sources / 1,388 declarations
and 189 active files / 700 active declarations. Product source, canonical `openspec/specs/`, public
API baselines, and package manifests remain untouched.