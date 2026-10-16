# Tasks 7.4 and 7.5 review remediation

**Date:** 2026-09-22
**Rejected freeze:** Tasks 7.4 and 7.5 combined target on base
`7b1bf74ee83c97a19f2c8b08ddcd30d54d443658`
**Verdict:** `REJECT`, 13,548 bytes,
SHA-256 `7af6d9be8651d2526fe8318a67c4dc8b5c9e09f542f88c7c215f5c05112ef6b7`

## LLL-1 — exact historical classifier replay

The Task 7.5 evidence artifact now records all 56 exact
`HISTORICAL<TAB>path<TAB>line<TAB>classifier` results from the 23 Task 3.1 sources at
`89e3ed55357e849852c1a0f6fefa2124433d7e30`. The guard compares the complete ordered replay and
requires every classifier name to occur. It no longer accepts one unrelated hit per historical
file as proof that the complete classifier set remains intact.

Mutation evidence:

- deleting the formerly non-sole definition-fanout classifier turns the focused guard red;
- deleting the complete `returns/yields ... NoActiveWait` alternative turns it red; and
- both mutations restore byte-exactly before the final green control.

## MMM-1 — current event vocabulary

- DU-055 now names `WorkflowEventAcceptanceResult`, the exact EV-012 public acceptance result.
- AC-005 now names `Rejected(DirectInstanceTerminal)` for direct durable terminal ingress.
- Task 7.3 artifact rows 16 and 19 alone are refreshed, followed by its guard-source digest.
- Task 7.3 and Task 7.5 both reject the superseded client method and event result/status vocabulary.

## NNN-1 — review observations

- The classifier accepts natural stale phrasings rather than only the original sentences. A probe
  containing eight rewordings, including `DeliverToInstanceAsync`, `yields NoActiveWait`, an
  instance/correlation-only route claim, and deferred durable `Publish`, turns red.
- The Task 7.5 artifact's 86-source count is a reviewed snapshot, not a live equality. A benign new
  active document stays green while the runtime enumeration still scans it.
- The Task 7.4 evidence has no trailing whitespace; committed-diff validation owns the check.
- Task 7.4 semantically pins the numbered approval prerequisite and Orleans-only adapter boundary,
  derives the archived inventory from disk and the immutable fixture, and rejects any additional
  Orleans task-ledger block.
- The implementation README correction retains its paragraph continuation indentation.
- The superseding request and verdict use the singular `task-7-4-...` discovery stem.

## Disposition

The rejected request, manifest, and verdict remain immutable and registered. This remediation does not authorize a checkpoint, Task 7.6, final harmonization exit, reshape Task 8.0, or Section 8.
