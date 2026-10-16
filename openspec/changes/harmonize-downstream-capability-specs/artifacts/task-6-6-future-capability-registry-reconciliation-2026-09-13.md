# Task 6.6 future-capability registry reconciliation

Date: 2026-09-13

## Decision

The two normative trees now use one exact identity for the deferred-capability inventory:
`docs/specs/13-phasing-and-open-questions.md` §13.4, "Future-capability registry". The numbered
document exposes a `Deferred capabilities` table for future promises and a separate
`Removed concepts` subsection for names that must remain absent without being represented as
future work.

Canonical `developer-facing-surface` and `saga-orchestration` requirements cite the exact path and
section name. Their active reshape-owned delta blocks carry byte-identical requirement bodies, so
the later cross-reference does not create a duplicate harmonization owner. Active guide links use
the generated `#134-future-capability-registry` anchor, and the normative source map records the
same name, path, classification boundary, and ownership split.

Harmonization task 6.6 owns the shared name, cross-reference, and deferred-versus-removed
distinction. Reshape task 9.6 remains open and owns final membership, including removal of shipped
durable `Publish` and definition-targeted fanout from the future table. This slice does not silently
take that separate cleanup.

## Executable evidence

`OpenSpecCorpusGuards.Task66_FutureCapabilityRegistryUsesOneCrossTreeNameAndSeparatesRemovedConcepts`
requires:

- the exact §13.4 heading and ordered deferred/removed subsections;
- the deferred table to exclude `WaitLong` and authored `Yield` while the removed subsection keeps
  both names searchable;
- the two canonical requirements to cite the exact registry and remain byte-identical to their
  active reshape deltas;
- every active Markdown/C# document outside immutable review/archive provenance to omit the old
  heading and anchor;
- both active guide links to contain the exact new anchor;
- the normative source map, completed task record, and full design decision to retain their exact
  cross-tree disposition.

Named constants own the semantic paths, headings, anchor, requirements, completion record, and
design decision. No new magic protocol or policy values are scattered through the assertions.

## Review carry-forward

Task 6.5 review finding AA-1 is closed in this target. The Task 6.5 guard now pins the entire design
decision that keeps authoring lifecycle types internal and assigns the companion SHA-256 to guard
source. Deleting or inverting the previously unpinned first five sentences makes the focused guard
red without relying on the active review freeze.

Self-review also found and closed an arbitrary-anchor typo gap: rejecting only the retired anchor
was insufficient, so both active guide paths now positively require the exact new anchor.

## Provenance and accounting

The 2026-08-18 Task 4.2 artifact remains byte-immutable. Because Task 6.6 changes two synchronized
canonical/delta requirement blocks, the live fixture now points to the new dated
`task-6-6-openspec-provenance-refresh-2026-09-13.md` record. The requirement-operation record remains
176 rows and 46,211 bytes with 173 synchronized operations, three new-capability requirements
outside canonical, zero pending operations, and SHA-256
`ea8719e768182f4eea097cb280d3487426a9a7450ca7becb72616e567e30b756`.

The new infrastructure `[Fact]` moves the executable crosswalk to 337 physical sources / 1,390
declarations and 702 active declarations. Both the crosswalk's derived pin and reshape task 7.20's
maintained completion note name Task 6.6 and match those values.

## Mutation evidence

Against a green focused control, each mutation was red and every file was restored byte-exactly:

1. invert Task 6.5's internal-lifecycle design decision;
2. restore the old §13.4 heading;
3. restore the retired active-guide anchor;
4. place `WaitLong` inside the deferred table;
5. remove the exact Saga registry cross-reference from canonical OpenSpec while leaving its active
   delta unchanged; and
6. replace a guide's new anchor with an arbitrary typo.

## Validation

- Release and Debug non-incremental warnings-as-errors builds: 0 warnings / 0 errors.
- Core 350; Ephemeral 79; Durable 99; Acceptance 37; Hosting 24; Provider Certification 96.
- PostgreSQL 101; SQL Server 72; Integration 11.
- Infrastructure 221/221; expected-red exactly the same 14 documented executable scenarios; full
  guard inventory 235.
- OpenSpec strict 18/18; harmonization ledger 24 complete / 10 open / 34 total.
- `git diff --check` clean; no product source changes.

