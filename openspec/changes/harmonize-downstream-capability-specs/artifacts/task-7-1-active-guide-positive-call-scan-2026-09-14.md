# Task 7.1 active-guide positive-call scan

Date: 2026-09-14

This dated checkpoint reruns Task 3.1's positive removed/deferred API scan after canonical
synchronization and the future-capability registry reconciliation. It records the Task 7.1 result;
it does not replace the broader stale-negative documentation closure owned by Tasks 7.3-7.5.

## Scope

The scan enumerates:

- `CLAUDE.md` and root `README.md`;
- active `docs/` Markdown and C# contract documents, excluding immutable `docs/archive/` and
  `docs/review/` records;
- every canonical `openspec/specs/*/spec.md`; and
- every non-archived change's `proposal.md`, `design.md`, `tasks.md`, and delta `spec.md` files.

Artifacts are excluded because they are dated evidence rather than active how-to or contract
sources. The post-edit population is:

| Source class | Files |
|---|---:|
| Root guidance | 2 |
| Active documentation | 42 |
| Canonical OpenSpec | 14 |
| Active change planning and deltas | 28 |
| **Total** | **86** |

## Reproducible source record

For each source, normalize CRLF and lone CR to LF, encode as UTF-8 without BOM, and render:

```text
<forward-slash path>\t<normalized byte length>\t<lowercase SHA-256>
```

Sort rows by ordinal path, join with LF, retain one final LF, and hash the resulting UTF-8 bytes.
The exact rendered record is preserved at
`openspec/changes/harmonize-downstream-capability-specs/artifacts/task-7-1-active-guide-positive-call-source-record-2026-09-15.tsv`
so the aggregate digest can be reproduced without inferring row content from counts alone.

- record rows: 86
- record bytes: 10,655
- record SHA-256: `56b6d27ece05d0ff536d9ca5114ba3e1856f0f3288f4bf5226bab341e36963f2`

## Positive call forms

The scan applies culture-invariant identifier-boundary patterns for:

- `WaitLong(`, `Yield(`, `WhenFirst(`, `Saga(`;
- `RunExternalJob(`, `RunChild(`, `RunChildren(`; and
- `.Pause(`, `.Resume(`, `.Archive(`, `.Purge(`, `.Cancel(`.

Whitespace between a member name and `(` is accepted. The result is **zero findings**.

The active ephemeral, Kubernetes scheduler, and Orleans guides retain concise negative/deferred
notes and exact links to `docs/specs/13-phasing-and-open-questions.md` §13.4, "Future-capability
registry". They expose no positive v1 call form for the removed or deferred members.

## Review carry-forward

This slice also closes the two observations from the approved Task 6.6 second-hardening review:

- **HH-1:** OpenSpec provenance artifacts are discovered recursively throughout
  `openspec/changes/**`, so moving an uncatalogued predecessor into a nested `superseded/` directory
  cannot escape the current-or-permanent-catalog requirement.
- **II-1:** the removed-concepts region ends at the next `###` subsection. Removed identifiers are
  rejected from the entire remainder of §13.4 outside that exact subsection, so a later future-work
  subsection cannot promise `WaitLong` or authored `Yield`.

Task 7.5 remains the owner of the complete recurring active-tree vocabulary and stale-negative
fixture. Task 7.1 adds the narrower durable positive-call regression now so its zero-finding result
does not depend on this dated artifact alone.