# Task 6.4 MaxActiveFibers disposition record

Date: 2026-09-01

## Result

Task 6.4 closes the stale current-implementation statement for `MaxActiveFibers` without deleting
its historical meaning. `docs/specs/18-semantic-appendix.md` now lists the former positive statement
only under `Deliberately excluded claims` and immediately records that Task 5.13 removed the third
live-fiber admission quantity. The current v1 model retains only host execution-path capacity and
node-local `ForEach` admission.

## Corpus disposition

The exact active non-review corpus is:

| Path | Occurrences | Disposition |
|---|---:|---|
| `docs/specs/18-semantic-appendix.md` | 1 | Explicitly excluded current claim |
| `openspec/changes/harmonize-downstream-capability-specs/artifacts/task-6-4-max-active-fibers-disposition-2026-09-01.md` | 4 | Reviewed disposition evidence |
| `openspec/changes/harmonize-downstream-capability-specs/tasks.md` | 1 | Completed remediation owner |
| `openspec/changes/reshape-developer-facing-interfaces/AMENDMENT-2026-07-28-root-only-fanout-and-authoring-lifecycle.md` | 13 | Dated amendment and revision history |
| `openspec/changes/reshape-developer-facing-interfaces/design.md` | 1 | Explicit removal decision |
| `openspec/changes/reshape-developer-facing-interfaces/proposal.md` | 1 | Explicit removal scope |
| `openspec/changes/reshape-developer-facing-interfaces/tasks.md` | 1 | Completed removal task |

Product source contains zero occurrences. Review records under `docs/review/` remain immutable
historical evidence and are not current implementation claims.

## Executable guard

`Task64_MaxActiveFibersMentionsAreHistoricalOrExplicitlyNegative`:

- recursively rejects the retired token in real `src/**/*.cs` files while excluding generated
  `bin`/`obj` paths;
- requires the exact non-review document inventory above;
- verifies the semantic appendix's excluded-claim context and removal sentence;
- verifies reshape proposal, design, task, and dated-amendment dispositions; and
- requires Task 6.4 to remain checked with its completion record.

The retired identifier is owned by the named `RetiredMaxActiveFibersName` constant rather than a
repeated semantic literal.

## Self-review corrections

The first complete Infrastructure run exposed an inherited Task 6.2 assumption: its Task 6.3
ledger block ended only at an unchecked Task 6.4 checkbox. Completing Task 6.4 made the older guard
red. The final implementation locates the Task 6.4 boundary for either checkbox state; Task 6.4's
own guard independently requires `[x]`. This retains source attribution while allowing sequential
ledger progress.

Five byte-restored negative controls covered product source, active documentation, appendix
negative context, reopened Task 6.4, and historical review evidence. The first four produced the
expected red result; the historical review occurrence remained green.

## Prior-review remediation carried forward

Before Task 6.4 began, commit `db0ae75c715c83bd0a45d4b94b6ef6b8310a278d` closed all four
Task 6.3 review findings: U-1 whole-block immutability for `CR-009a`; U-2 exact companion link
resolution; U-3 task-7.20 provenance text; and U-4 order-independent trait matching. Commit
`c23dc4e732881fb2ddb7792836e740d72ce2644f` activated that approval evidence.

## Accounting and scope

- Crosswalk: 337 physical sources / 1,388 declarations; 189 active files / 700 active declarations.
- Retired declaration ledger: 866 rows.
- Harmonization ledger: 22 complete / 12 open / 34 total.
- No `src/**` change.
- No `openspec/specs/**` canonical capability change.
- No public API, package manifest, or baseline change.

## Validation

- Debug and Release full solution builds, non-incremental and warnings-as-errors: 0 warnings /
  0 errors.
- Core 350; Ephemeral 79; Durable 99; Acceptance 37; Hosting 24; ProviderCertification 96.
- PostgreSQL 101; SQL Server 72; Integration 11.
- Infrastructure 219/219; expected-red exactly 14; full guards 233 total.
- OpenSpec strict 18/18.
- `git diff --check` exit 0.