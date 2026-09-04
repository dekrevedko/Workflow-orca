# Harmonization Task 6.5 independent review request

**Date:** 2026-09-03
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the Task 6.5 checkpoint

The Task 6.4 post-review-hardening chain is complete:

- checkpoint `737f2fd4e08b6468f9193221c833b1eb427afb95`;
- approval-evidence commit `7960bd0dc56c8fbeb30e3ca7362ecf07f78de724`; and
- mechanical activation `5a4a2871f4394645e40250295adc2e46aef9d461`.

The exact target is named by
`harmonize-downstream-capability-specs-task-6-5-dirty-manifest-2026-09-03.txt`.
Recompute the commit-real raw manifest and scoped content record from base
`5a4a2871f4394645e40250295adc2e46aef9d461`.

## Claims to verify

1. `docs/specs/17-public-authoring-contract.cs` remains byte-unchanged at 53,745 bytes and
   SHA-256 `41f6472c2774363d2ab922c608922e787ec241333e1d1c0b76b0c6d529ab8ec3` because authoring-session
   lifecycle state, sessions, handles, join tokens, and lexical tokens remain implementation-only.
2. The approved `OrcaCore.Core` public API baseline still contains no exported declarations, and
   the exhaustive twelve-assembly baseline independently rejects any lifecycle type becoming public.
3. `Task65_PublicAuthoringCompanionRemainsUnchangedAndLifecycleInternalsStayNonPublic` binds the
   unchanged companion, internal declarations, empty Core baseline, exhaustive public surface, and
   recorded Task 6.5 decision without reflection or a new product bridge.
4. Round-49 finding X-1 is closed: the immutable semantic-appendix source is pinned at 8,415 bytes
   and SHA-256 `131d22bea736b6c7c4ac8a310ef1db72c992dcc867776b664c01fe2988d57be6`
   before the complete canonical projection is compared.
5. The declaration crosswalk is updated only for the new infrastructure fact: 337 sources / 1,389
   declarations, 189 active files / 701 active declarations, and the unchanged 866-row retirement
   ledger. Reshape task 7.20 names harmonization task 6.5 as the producer of those current counts.
6. No `src/**`, canonical `openspec/specs/**`, public-authoring companion, public API baseline,
   package manifest, or behavior-test path changes in this slice.

## Negative controls

- coherently alter the semantic-appendix source and canonical projection: Task 6.4 guard red on the
  immutable source SHA-256;
- add an internal authoring lifecycle type to the companion and coherently update its fixture hash:
  Task 6.5 guard red on forbidden companion content;
- expose an authoring lifecycle type from `OrcaCore.Core`: the exact public API baseline red; and
- alter the Task 6.5 completion decision or remove its explicit completion marker: Task 6.5 guard red.

## Validation to reproduce

- Debug and Release warnings-as-errors builds: 0 warnings / 0 errors;
- Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, Provider Certification 96;
- PostgreSQL 101, SQL Server 72, Integration 11;
- Infrastructure 220/220 and exactly 14 separately classified intentional expected reds;
- OpenSpec strict 18/18, task ledger 23 complete / 11 open / 34 total, and `git diff --check` clean.

Do not edit, stage, or commit the target. Return one dated immutable `APPROVE` or `REJECT` verdict.
Task 6.6, change archival, and reshape Task 8.0 remain outside this authorization.
