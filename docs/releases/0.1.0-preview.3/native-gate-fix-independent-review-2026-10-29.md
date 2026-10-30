# Native release gate correction — adversarial self-review

Verdict: APPROVE for this workflow-only correction and exact-asset qualification.
This is an independent author review, not external approval. Publication and
necessary workflow fixes are authorized by the user's 2026-10-29 instruction.

Base: 6b4ab952376de366a102e236b5e2b5b1ddfade41. Scope: two workflow files and
release_gate.py / test_release_gate.py, four paths. No product source, C# test,
public API, existing consumer C# assertion, requirement or package byte changed.

Reproduction: release run 40997094707 completed its reusable native workflow with
the optional CI, matrix and records jobs skipped. Publication incorrectly ran.
The captured real jobs JSON is rejected by the new release gate. GitHub documents
skip propagation through dependency chains:
https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-jobs

The fix explicitly overrides skip propagation at both native/records boundaries,
while requiring their direct dependencies to succeed and respecting cancellation.
Only successful six-record validation emits qualified=true. The publishing job
requires that output and also reads the actual run's job inventory, rejecting
missing, duplicated, skipped, failed, cancelled, running or truncated cells and
an unsuccessful aggregate. Its added permission is actions:read, not write.

A read-only existing-release mode downloads the twelve immutable GitHub assets,
validates checksums and repository commit against the release tag, and rejects
any source/build-input difference. It cannot also build/consume another CI feed.
It exercises the same skipped-optional-CI path without rebuilding, replacing,
moving or republishing preview.3. Consumers and record assertions are unchanged.

Validation: 13/13 Python gate tests; the actual skipped-run reproduction rejects;
actionlint 1.7.12 passes; whitespace check passes. Independent YAML-graph checks
verify both explicit skip overrides, output propagation, output precondition,
and actual-job verification before publication. C# is unchanged; the approved
ReSharper result remains applicable. No blocking findings.

The hosted six-cell existing-release run remains required before the correction
is called complete. It must prove the exact published package hashes, 384 cases,
and unchanged source/build inputs; record both its success and the original
skipped qualification honestly. Preserve all prior approvals and frozen targets.
This verdict belongs only in a direct child of the four-path checkpoint.
