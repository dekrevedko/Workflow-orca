# Issue #13 corrected whole target — review, 2026-10-28

**Self-review verdict: no blocking findings; ready for user checkpoint approval.**

This is an adversarial author self-review, not an external review or an invented
user approval. It closes the implementation/evidence findings from the whole-AOT
review against the corrected target. Current source/native approval fields remain
null. No issue-branch commit, push, PR, merge, closure or release is authorized by
this report. The checkpoint manifest excludes this verdict.

## Findings checked

- Whole-AOT F1: write-only descriptors no longer narrow Generated admission.
  Their materialization, exact input fingerprints and workflow results match
  legacy JIT. Serialized forbidden maps/converters still reject. The separately
  approved synthetic-initializer restriction remains checked before that filter.
- Whole-AOT F2: actual serializer getter results validate included fields/internal
  properties, collection elements and ordinary properties returning a new runtime
  type on the second read. Unapproved subtypes/collections reject in both modes.
  Approved polymorphism, ignored members and valid detached values remain correct.
  Public getter reads remain two; included getters and a child under an included
  field remain one. Rejected inputs run no Init, invalid Init state runs no later
  body, keys remain reusable and an accepted instance keeps its exact wait/state.
  The existing abort/shutdown ownership paths remain authoritative and joined.
- P3: acceptance text describes regression obligations. Living proposal/design,
  guide and revised PR text now distinguish the old approved checkpoint from the
  corrected candidate and its new native execution. The legacy included-member
  bug fix is described explicitly rather than claiming the invalid values retain
  their old acceptance.

I re-read all three production edits and the member regression tests. Serializer
callbacks retain metadata/getters, not application models or providers. The
reflection resolver is lazy and fenced to legacy JIT; Generated mode has no
fallback. The fixes add no public API, serializer option, root or suppression.
All other product source remains identical to the approved implementation. The
earlier local code-review report remains valid for these exact production bytes.

## Native evidence review

[Run 40762764617](https://github.com/dekrevedko/Workflow-orca/actions/runs/40762764617)
has eight successful jobs: one feed, six real native cells and their agreement
gate. Commit 7f1034c4f0733da48caf6814f37933f7c38ac9af has tree
b5ce42f910b95a90ce3acf8d149d9d54210a2a1e. Actual runner labels/host records are
Windows Server 2025 win-x64 and Ubuntu 24.04 linux-x64, not local substitutes.

I downloaded and reran the verifier against all six raw reports. Each host passes
82 JIT, 55 fully trimmed JIT and 55 Native AOT cases, 384 total. Every original
journey, twelve NS groups and thirteen mandatory subcases remain, with the new
Metadata/Default member cases and legacy controls added. JSON reflection is
disabled in trimmed/AOT and dynamic code is unavailable in AOT. Nothing is skipped
or classified Unsupported. Golden/fingerprint and strong-value corpora match the
earlier approved native baseline as well as the cross-cell comparison.

All 360 source/build-input and 20 harness identities match the current bytes,
with only the documented temporary workflow-trigger normalization. All six cells
bind one twelve-package feed; the three selected archive DLL hashes also match
the independently downloaded packages. No product code changed after validation.

Every one of the 24 diagnostic records has the same body, code, severity and count
as its earlier reviewed handwritten-context counterexample, after shifting only
Program.cs lines 349/351 to 363/365. Registration still rejects that context.
There is no newly reached supported-path diagnostic. The exact individual pairings
are retained in diagnostic-review.json; blanket approvals are not substituted.

The new guard binds the native index/report hashes and current source/harness,
and does not inherit source/native approval from the old checkpoint. Historic
protocol-2 records/verdicts stay unchanged; protocol 3 is required for the fixes.
Scoped Git attributes preserve raw Windows CRLF and tool-banner bytes in the new
evidence only; the retained raw records are never normalized to pass a check.
Live event delivery/publishing, other engines/RIDs and broad compatibility badges
remain excluded. Source execution evidence does not assign or publish a version.

## Validation and process

Fresh Release build: zero warnings/errors. OpenSpec: 25/25. Infrastructure: 309/309;
after the living documentation addenda, its affected corpus/native/binding guards
also pass 25/25. The first fresh-worktree run had 307 passing and two missing-feed
prerequisite failures; packing the exact local feed resolved both with no product
change. An author evidence-comparison script initially missed line-only diagnostic
locations without columns; fixing that normalization confirmed all 24 exact pairs.
Neither setup problem is reported as a product failure or hidden from the record.

The unchanged corrected-source receipts retain unit 969/969, metadata 41/41,
provider/integration 188/188, requirement selectors 60/60, fifty consecutive
unfiltered dotnet test stress runs (17,600 test executions passed) and engine line
coverage 15,956/20,112 = 79.3357%, above the 75% gate. No unchanged runtime lane was
replaced by a different runner or claimed as freshly rerun in this native step.

ReSharper 2026.2.3.1 on the updated guard, Release / Any CPU / net10.0, reports zero
errors, zero warnings and two unchanged collection-style notes. It used compatible
fresh build outputs with no-build. The prior source/fixture SARIF remains valid for
the unchanged production/test inputs, with its existing warning triage retained.

The temporary validation push was authorized by the user's “Ok continue” after
the hosted-matrix plan. The issue branch remains at 1ad2a59 with an empty index;
tasks 3.1/3.2 remain open. The new manifest/freeze receipt supplies the exact
checkpoint target; this verdict stays outside it for a separate child after
approval. Preserve compact raw evidence and remove only the owned validation
worktree/build outputs. PR CI and publication/merge approval remain later gates.
