# Authoring-friend implementation closeout — task 3.2

Date: 2026-09-29. Change: `admit-dag-authoring-friend-boundary`.
Base: `738c3b591b6f6e72de13a7750601cb36430c514f`.

## Authority and immutable chain

This target records an already independently approved implementation. It does not claim that
the closeout target itself is approved. Task 3.2 remains open pending its separate review,
checkpoint, evidence commit and review-state-only activation.

| Object | Exact identity |
| --- | --- |
| Task 8.2 source checkpoint | `a9f835f939d683500ca231c7ba491ab8eae2aaae` |
| Approved source tree | `cf11b3f6732f11250ba3a0255114bcdc4ae00060` |
| Direct-child source approval evidence | `055e7b8e71e8dfe79e76f267f8782b7f6f79f7b8` |
| Source review activation | `738c3b591b6f6e72de13a7750601cb36430c514f` |
| Source APPROVE verdict | `docs/review/developer-facing-interface-section-08-task-8-2-dag-authoring-source-zzz-remediation-independent-review-verdict-2026-09-28.md` |
| Verdict bytes / SHA-256 | 8,742 / `513871b82c87f5949d4801108f4f1c901b8f8cfe7084121217359cf96a9ccd4a` |
| Source manifest bytes / SHA-256 | 2,968 / `c9462505515394deb34865b8fd9e5c4dd94c6427733498c89cae586f98ec020a` |
| Source request bytes / SHA-256 | 6,000 / `c2e58b564b92bacd8ab32ee9f6ada943a0ffb28bbbcdf1fd8a9e837805b444bf` |

The immutable manifest and request are the ZZZ-remediation packet dated 2026-09-28. The source
checkpoint contains those bytes; the evidence commit adds the verdict, rather than merely
containing a prior copy. Both the contract and source approval validators now require exactly
one final nonblank APPROVE line and exactly one parent equal to the reviewed checkpoint.
All prior REJECT and APPROVE records remain untouched and cataloged.

## Permanent registry, not a second source approval

Schema 5 retains the exact two `Proposed` records, contract `ApprovedPending` evidence and two
approved-pending requirement records as history. The new `completeEvidence` object is the
current implementation stage, bound independently in guard source to the chain above and to
the five completed tasks 2.1–2.5. The guard additionally requires amendment 3.1 and reshape
8.2 to be complete. Dropping a task, weakening the count, substituting a verdict or later
containing commit, or changing a refreeze coordinate cannot be routine metadata refreshes.

Executable evidence is present both now and in the approved source checkpoint:

- `tests/OrcaCore.DeveloperSurface.Guards/DagInternalMemberReferenceGuards.cs`: exact sole
  product dependency, authoring friend, six member signatures, non-public type rejection and
  four permanent type-only probes.
- `tests/OrcaCore.DeveloperSurface.Guards/DagAuthoringBehaviorGuards.cs`: seven exact diagnostic
  cases, immutable snapshots, Build/TryBuild parity and 100,000-node chains in both directions.
- `tests/OrcaCore.Core.Tests/Compilation/PublicDefinitionCompilerContractTests.cs`: compiled
  workflow fingerprint determinism and cross-mode identity evidence.

The requirement blocks, canonical preambles, predecessor history and provenance classifications
do not change: 178 rows, 46,784 bytes, SHA-256
`0dd47120d11d2442fccc217902386dd36d4bbf57b7fb476bd7c670dbbbaa5257`, with 173 synchronized,
2 superseded by approved successors, 3 outside canonical, zero pending operations and
`semanticApprovalEligible: true`. The approved-pending provenance artifact is preserved as
dated historical evidence; it is not rewritten to suggest it always described Complete.

## Previous review observations addressed

1. **Stale status:** current `CLAUDE.md`, numbered documents 03/08/10/17, Decision 22's summary,
   solution architecture and overview now identify the independently approved source checkpoint.
   Earlier dated Decision 22 entries are unchanged; a 2026-09-29 entry supersedes their status.
   All affected Task 7.3 whole-document rows and the artifact digest are refreshed together.
2. **Loose approval:** one terminal APPROVE and an exact single-parent/addition rule replace
   accepting any approving line and checking only the first parent.
3. **SDK selection:** temporary metadata probe projects copy the repository's `global.json`
   byte-for-byte and run with that directory as their working directory. The probes remain
   type/member tests, not a new friend or runtime entry point.
4. **Historical content-record convention:** the frozen source request stated ordinal sorting,
   but its `ff924cd5…` semantic digest was rendered in raw Git manifest order. The reviewer
   independently disclosed ordinal `85522a3a…` instead. This closeout preserves both immutable
   documents without laundering that discrepancy; source approval binds the exact tree and
   manifest. Its own new request states one explicit, independently recomputable convention.

## Scope and next gate

No product source, canonical requirement, friend attribute, internal allowlist, API baseline,
package source-provenance fixture or declaration crosswalk changes in this closeout. Test
helpers and constants are not new Fact/Theory declarations. The current eight-product-friend
graph remains exact. No `Dag -> Dag.Hosting` friend, OrcaCore codec access or Task 8.3 source is
authorized. After this closeout's independent approval and checkpoint, prepare a separate
runtime-view amendment and reviewed 8.3/8.4/8.5 codec re-sequencing; do not stack successors on
an implementation-pending record or silently expand the authoring seam.

Validation and isolated negative-control results are recorded in the accompanying new review
request. This permanent disposition is itself SHA-256-pinned by the canonical gate.
