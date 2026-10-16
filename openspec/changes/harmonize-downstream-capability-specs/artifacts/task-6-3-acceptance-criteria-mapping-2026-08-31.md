# Task 6.3 acceptance-criteria mapping

Date: 2026-08-31

## Result

Task 6.3 gives the two numbered requirements introduced by Tasks 6.1 and 6.2 exact acceptance
criteria and executable evidence:

- `CR-009a` links to `AC-028`; and
- `CR-014a` links to `AC-029`.

Each acceptance criterion appears exactly once in `docs/specs/12-acceptance-criteria.md`, and each
numbered requirement links back to only its assigned criterion. `AC-022` remains owned solely by
structural-versus-opaque fingerprint evidence.

## Normative mapping

| Criterion | Requirement | Contract covered |
|---|---|---|
| `AC-028` | `CR-009a` | `Open` / `JoinPending` / `Frozen`; successor epochs; immutable terminal snapshot; invalid-handle and losing-race rejection before mutation; repeated-build stability |
| `AC-029` | `CR-014a` | creation-time authored/runtime provenance; unchanged single-failure propagation; one owning multi-cause join failure; authored-branch and item-index ordering; non-negative item indexes; closed fixed-codec rejection |

`AC-029` explicitly cites the synchronized `quality-and-verification` executable-evidence
requirement, the canonical `structured-fiber-execution` join-ordering contract, and document 17's
public-contract companion. This records every normative source identified by the Task 6.2 review
in the criterion itself rather than relying on task prose.

## Executable evidence

- `AuthoringLifecycleTests` carries `AC=AC-028` alongside `Requirement=CR-009a`.
- Core `FailureProvenanceTests` carries `AC=AC-029` alongside `Requirement=CR-014a`.
- The active ephemeral and durable ordered-failure runtime regressions each carry `AC=AC-029`.
- CI owns one exact `Acceptance harmonization evidence` step: Core runs `AC-028|AC-029`, and the
  Ephemeral and Durable projects each run `AC-029`.
- The pre-existing acceptance-catalog guards continue to require trait coverage or an explicit
  waiver for every catalogued criterion.

Focused evidence is Core 28/28, Ephemeral 1/1, Durable 1/1.

## Structural guard and negative controls

`Task63_AcceptanceCriteriaMapLifecycleAndFailureProvenanceBidirectionally` requires the exact
reverse links, complete criterion clauses, normative companion citations, test traits, repository
catalog guards, and normalized CI command. Five byte-restored mutations each made it red:

1. replacing the `CR-009a` backlink with `AC-999`;
2. replacing AC-029's `[CR-014a]` binding with `[CR-999]`;
3. removing the active ephemeral runtime AC trait;
4. narrowing the Core CI filter to `AC-028`; and
5. removing the `structured-fiber-execution` normative citation.

The restored control passed after every mutation. The older Task 6.2 guard now requires the
completed `[x] 6.3` heading, so reverting the ledger state cannot silently weaken its source pin.

## Self-review corrections

The first complete draft mapped every behavior but left the three companion sources only in the
task ledger. Self-review identified that Task 6.3 requires the criterion itself to cite them. The
final AC-029 includes explicit links, and the fifth negative control proves the attribution is
load-bearing.

The first broad Infrastructure run also exposed Task 6.2's stale open-checkbox locator for Task
6.3. The final guard requires the completed heading and the full lane is green.

## Accounting and scope

- Declaration accounting is 337 physical sources / 1,387 declarations and 189 active files /
  699 active declarations. Only the new guard fact changes the declaration totals.
- The retired-declaration ledger remains 866 rows.
- Historical current-worktree pins were mechanically refreshed to 9 / 4 / 3 / 3 / 4 for Tasks
  5.1 / 5.2 / 5.3 / 6.1 / 6.2.
- No `src/**` file changes.
- No `openspec/specs/**` canonical capability changes.
- No public API, package manifest, or baseline changes.

## Validation

- Debug and Release solution builds, non-incremental and warnings-as-errors: 0 warnings / 0 errors.
- Core 350/350; Ephemeral 79/79; Durable 99/99; Acceptance 37/37; Hosting 24/24;
  ProviderCertification 96/96.
- PostgreSQL 101/101; SQL Server 72/72; Integration 11/11, run sequentially.
- Infrastructure guards 218/218.
- Expected-red guards: exactly the same 14 `ExecutableBehaviorExpectedRedGuards` failures.
- OpenSpec strict 18/18.
- Harmonization ledger 21 complete / 13 open / 34 total.
- `git diff --check` exit 0.
