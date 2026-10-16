# Independent exit review request — harmonization task 8.1

Date: 2026-09-23. Base: `b660d2d5b58d4b06663dd24f6a94a9c71ab37342` (tree
`ec1da3eaecc8b1c0193fef00b125f1d3bddbb323`). The target is the uncommitted, commit-real
worktree named byte-for-byte by
`harmonize-downstream-capability-specs-task-8-1-final-audit-dirty-manifest-2026-09-23.txt`.
Please review this as the final synchronized canonical/docs target for task 8.2. The reviewed
target includes the task 8.1 audit, not a task 8.2 approval or a task 8.3 checkpoint.

## Scope and claims

1. The dated `task-8-1-final-harmonization-audit-2026-09-23.md` derives an exact binary-safe
   canonical diff from the post-Section-7 checkpoint to the audited base: 11 changed canonical
   files, 471 additions, 107 deletions, and a raw patch digest. This target itself edits no
   `openspec/specs/**` or `src/**` file. The audit records 14 canonical capability directories,
   190 canonical headings, 16 active delta directories, 176 active headings, zero duplicate owners,
   173 synchronized operations, three declared bootstrap-only operations, zero pending, and zero
   missing `spec.md` files. The capability-directory record is 1,321 bytes with a pinned digest.
2. The audit's evolving active-document vocabulary check finds zero positive removed/deferred
   calls and zero stale Section 7B negatives. Its local Markdown link check covers 85 active
   files, 195 relative file targets and eight fragment targets, all resolving. It does not claim
   to fetch or validate external URLs, and excludes immutable review/archive files and change
   artifacts from active guidance.
3. The active normative-source map's former 147/199 review/archive counts were stale. It now
   records a dated base snapshot of 269 review and 200 archive files, and classifies the
   non-normative semantic appendix separately from the 19 normative package files. This is the
   only intentionally changed Task 7.3 source: row 10 of its 22-source artifact and that
   artifact's guard-source digest are refreshed together in this exact target. Its other 21
   source hashes and the approved Section 7B wording are unchanged.
4. The two non-blocking Task 7.7 review observations are closed in the same guard edit.
   Historical record and archived-prompt worktree content use the established CRLF-to-LF
   normalization, while `HEAD` must still point to the exact move-commit prompt blob. The archive
   index assertion now binds the exact old-path routing key to both the prompt and provenance
   links. No immutable archived or review record was edited.
5. Task 8.1 is checked complete; task 8.2 (independent exit approval) and task 8.3
   (post-approval coherent checkpoint) remain open. The reshape task 8.0 block is not lifted by
   this request. The provenance fixture refresh removes only current-worktree pins whose source
   bytes were intentionally changed; every historical row and earlier verdict remains fixed.

## Validation to reproduce

- Debug and Release warning-as-error, non-incremental solution builds: 0 warnings, 0 errors.
- Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96,
  PostgreSQL 101, SQL Server 72, Integration 11.
- Infrastructure guards: 226/226. The separate ExpectedRed disposition: exactly 14 documented
  reshape Section 8 scenarios red, zero unexpected failures. Focused canonical ownership 1/1,
  vocabulary 2/2, Task 7.3 source pin 1/1, and Task 7.2/7.7 immutable history 1/1.
- `openspec validate --all --strict`: 18 passed, 0 failed. Harmonization ledger: 32/34 complete;
  reshape ledger: 129/159 complete. `git diff --check`: no findings.

## Review procedure and authority limit

- Reproduce the base commit/tree, the raw commit-real porcelain against the frozen manifest,
  the scoped content record in `review-manifest-provenance.json`, and the capability-directory
  inventory before and after probing. Simulate staging exactly the manifest paths in an isolated
  index and compare its tree and path set. Do not edit the main worktree to run mutations.
- Recompute the canonical diff directly from its two committed Git objects and verify every
  blob/line-count row. Enumerate all active change delta directories and `(capability, requirement)`
  owners independently. Check that canonical preambles and requirement provenance still satisfy
  the executable guard; structural strict validation alone is not semantic approval.
- Re-run the active vocabulary and local-link sweeps. Check the source-map file counts against
  the named base, the deliberate Task 7.3 row-10 refresh, and the complete 22-row artifact pin.
- In a verified disposable checkout, mutation-test the Task 7.7 CRLF worktree path and exact
  committed blob comparison, then retarget the archive index old-path key and require red.
  Stop any probe script unless its resolved working directory is the disposable checkout.
- Keep the 14 intentional expected-red product scenarios separate from Infrastructure failures.
  Confirm all prior immutable rejection and approval verdicts remain unchanged.

Please write a new dated immutable independent `APPROVE` or `REJECT` verdict for this exact target.
The verdict is not part of this freeze. Only a matching `APPROVE` may authorize task 8.3's
zero-drift checkpoint, followed by a separate evidence commit and mechanical approval activation.
This review does not itself authorize reshape task 8.0 or Section 8 source work.
