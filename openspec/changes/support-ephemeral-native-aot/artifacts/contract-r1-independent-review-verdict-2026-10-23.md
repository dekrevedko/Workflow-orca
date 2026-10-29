# Issue #13 R1 contract review — 2026-10-23

## Verdict: APPROVE (contract only)

No P1 or P2 findings, and no new findings carried forward. The F1/P3 revision
resolves the rejected contract finding and adds the requested controls. This is
a separate review pass over the frozen diff, not approval of product behavior or
an external human review. The user's instruction to keep it uncommitted remains
in force: do not checkpoint or start source implementation in this turn.

## Exact target

- Base/HEAD: `3b7f98aa0748a4d9c3f54d4604e566ed61f21bad`.
- Branch: `codex/issue-13-native-aot-trimming`.
- Frozen tree: `2b981a667a0c08aca7215118e73f0e40b91db8ac`, 29 manifest paths.
- Rejected comparison tree: `c0fe638a0143421f6470bd207e65966aa0d1698a`.
- Manifest: `artifacts/contract-r1-manifest-2026-10-23.txt`.
- Content SHA-256: `8a605624ffa8e1d90aea7972674ce3f802a44be0aaf03a0979b09c53c84f3c95`.

I checked all raw hashes, both porcelain hashes before adding this excluded
verdict, candidate tree blobs and exact path scope against the parent, staged
whitespace, and the unchanged empty author index. All 29 files match; no drift.
All eleven rejected dated artifacts/verdict are byte-exact.

## F1 is resolved at the contract level

The new Generated-mode .NET 10 restriction is a declared-shape rule combining
IsMemberInitializer=true with an actual JsonIgnore condition other than Never.
It applies to both zero/omitted and seven/present values, root and transitive
closures, with early SFE-TYPE-002 or direct-boundary NotSupportedException. It is
not mislabeled missing metadata. Common admitted shapes retain byte/value parity
and legacy Reflection behavior remains unchanged. The original positional value
state, mutable DTO and genuine constructor-bound controls remain admitted. An
unused excluded type cannot invalidate the whole context.

I re-ran the retained isolated framework probe without rebuilding or changing it:
the original counterexample remains 42 versus 0; classification reads the public
metadata before constructing a model; the mutable and genuine constructor-bound
controls agree across reflection/generated metadata. Native preservation of those
facts is still an explicit future source-qualification requirement, not assumed
from this framework test. The R1 contract does not relax any original AOT-01–16
positive scenario or accept Unsupported as completion.

## P3 controls are explicit

NS-02a/b require rejection of the baked string-enum policy and exact handwritten
renamed-property context despite ordinary runtime options/empty converter lists.
NS-02c requires a valid retry and original numeric/member encoding. NS-06a–f cover
the affected root/nested omitted/present shapes, admitted alternatives,
metadata-only classification and unused-root control. All nine named R1 subcases
are mandatory in their existing twelve groups. Configuration failures cannot
publish or seal metadata. The public API catalogue is unchanged.

## Validation and scope

- Independently re-ran strict OpenSpec: 25/25.
- Independently recomputed 206 provenance rows / 54,112 bytes, all seven pending
  additions, the unchanged 199-row master subset, current artifact hash and all
  22 predecessor artifacts against their original Git blobs. Semantic approval
  eligibility remains false while canonical operations are pending.
- Inspected the exact-target final TRX: 259/259, no failed/skipped tests. Release
  0 warnings/0 errors and twelve-package feed results are retained author
  results; I did not re-run those full lanes in this text/diff review.
- Source, canonical/numbered docs, declaration/executable baselines, runtime
  tests/workflows, dependencies/friends and old approval registry are unchanged.
  Guard changes only pins/catalog entries; no checks are loosened.

Compact review checks, OpenSpec log and the rerun probe output are under ignored
`.artifacts/publishing-validation/native-support-contract-r1/re-review/`. No new
build/temporary project was created. The existing small E:/oc13f1 cleanup-policy
block remains as recorded; no deletion retry was attempted.

## Authority and next gate

This excluded verdict is the only new path from this review. The 29-path target,
HEAD and index remain unchanged and uncommitted. No push, issue edit, canonical
transition or product implementation is authorized by this review pass. Obtain
explicit user approval before the checkpoint/source gate. Continue to review
each major coherent step before advancing; review implementation and native
support evidence separately from this planning verdict.
