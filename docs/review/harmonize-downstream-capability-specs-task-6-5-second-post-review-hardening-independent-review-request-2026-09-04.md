# Harmonization Task 6.5 second post-review-hardening independent review request

**Date:** 2026-09-04
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 6.5 second post-review-hardening checkpoint

The approved Task 6.5 post-review-hardening chain is:

- checkpoint `fba2c9a3476511025625e58dad46839b81851f0d`;
- approval-evidence commit `59cab939d48c6e93742896d39462d09cb7d56ac5`; and
- mechanical activation `d501083a1879c241ed784565ad828997f620bb00`.

The immutable post-review-hardening verdict is 16,269 bytes with SHA-256
`3cde1ea07bd4d0c5265b4e538a02a6f1b0bbe9e3bbccdf8b7bd7f5d3dd0246f8`.
This request reviews only finding Z-1 from that verdict. It does not authorize Task 6.6, change
archival, or reshape Task 8.0.

The exact target is named by
`harmonize-downstream-capability-specs-task-6-5-second-post-review-hardening-dirty-manifest-2026-09-04.txt`.
Recompute the commit-real raw manifest and scoped content record from base
`d501083a1879c241ed784565ad828997f620bb00`.

## Claims to verify

1. The Task 6.5 guard requires the exact post-review-hardening ledger decision that records the
   guard-source companion digest and the coherent companion-plus-fixture regression closure.
2. The guard also requires the second-review ledger marker recording Z-1's durable closure.
3. The guard reads `design.md` and requires the exact guard-source ownership decision that prevents
   mutable fixture data from authorizing coherent documentation drift.
4. Removing either the second-review ledger marker or the design decision independently makes the
   focused Task 6.5 guard red; both files restore byte-exactly after the probes.
5. Named constants own the semantic strings. No product source, canonical OpenSpec requirement,
   companion, public API baseline, or declaration-accounting fixture changes.
6. The approved Task 6.5 hardening checkpoint, both verdicts, evidence commit, activation, and
   archived freeze remain registered exactly.

## Negative controls

- remove the Task 6.5 second-review ledger marker: focused Task 6.5 guard red;
- remove the design's guard-source ownership paragraph: focused Task 6.5 guard red;
- weaken or remove the original post-review-hardening ledger decision: focused Task 6.5 guard red;
- alter any immutable Task 6.5 request, manifest, verdict, or archived freeze: provenance guard red.

## Validation to reproduce

- Debug and Release warnings-as-errors builds: 0 warnings / 0 errors;
- focused Task 6.5 and review-provenance guards: green;
- Infrastructure 220/220 and exactly 14 separately classified intentional expected reds;
- OpenSpec strict 18/18, task ledger 23 complete / 11 open / 34 total, and `git diff --check` clean;
- zero changes under `src/**`, canonical `openspec/specs/**`, the companion, or public API baselines.

Do not edit, stage, or commit the target. Return one dated immutable `APPROVE` or `REJECT` verdict.