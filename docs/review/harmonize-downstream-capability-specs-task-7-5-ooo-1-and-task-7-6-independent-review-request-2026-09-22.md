# Harmonization Task 7.5 OOO-1 hardening and Task 7.6 independent review request

**Date:** 2026-09-22
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** checkpoint the combined Task 7.5 post-review hardening and Task 7.6
qualified-owner audit

Review this frozen target on immutable base
`d6eee0d20d0e82135bc33caf2f8b25bdcd665772`. Do not edit, stage, or commit it. Use the dedicated
`Workflow-orca-review-task-7-5-7-6` worktree. The raw-order manifest is
`harmonize-downstream-capability-specs-task-7-5-ooo-1-and-task-7-6-dirty-manifest-2026-09-22.txt`.
This request does not authorize Task 7.7, final harmonization exit, reshape Task 8.0, or Section 8.

## Prior approved chain

The approved Task 7.4/7.5 remediation landed as:

1. checkpoint `7a262404a5459893d4d1807126ffb47b33dd7c61`;
2. approval evidence `bd7c01bfa7e01e37d70586ce3c2b997738324b34`; and
3. activation `d6eee0d20d0e82135bc33caf2f8b25bdcd665772`.

The chain is unchanged. This target starts only after activation and carries no product or canonical
OpenSpec edit.

## Part A — Task 7.5 OOO-1 post-review hardening

The active-tree classifier now directly exercises all eight natural-language regressions from the
approved review:

1. definition fanout is not supported;
2. fanout to every instance of a definition is not supported in v1;
3. Section 7B is not yet approved;
4. Section 7B has not been approved yet;
5. pre-wait events are dropped and must be redelivered;
6. the durable engine does not buffer pre-wait events;
7. publishing events from a workflow is unavailable in v1; and
8. the engine supports instance and correlation delivery only.

Two guard-source SHA-256 values bind the complete classifier name/expression catalog and the exact
ordered phrase/expected-classifier catalog. The dated hardening artifact is separately byte-pinned.
A coherent refresh of historical findings or mutable artifact data cannot silently delete a
classifier or one of the eight reviewed probes.

Required controls:

1. Delete any one phrase/expected-classifier entry; Task 7.5 must fail on the synthetic-catalog pin.
2. Narrow a classifier so its reviewed phrase stops matching; Task 7.5 must fail on the direct probe.
3. Delete a classifier and coherently refresh its historical rows and mutable artifact digest; the
   guard-owned classifier digest must remain red.
4. Alter the task/design decision or either dated-artifact digest; Task 7.5 must fail.

## Part B — Task 7.6 exact qualified-owner audit

The former 125-entry `ForbiddenPublicSymbols` catalog contained fifteen invalid identities:

- ten ephemeral management types named a nonexistent
  `OrcaCore.Engine.Ephemeral.Management` namespace and now name their actual historical
  `OrcaCore.Engine.Ephemeral` namespace;
- `IWorkflowPayloadCodec` and `IWorkflowPayloadSerializer` now name their pre-split historical
  `OrcaCore` assembly instead of the later provider-abstractions package; and
- three invented projection-statistics identities are deleted rather than retained as vacuous
  negatives.

The resulting 122-entry ordered catalog has SHA-256
`302b74d5ac5d52ddb2586002454f1f6771c37896beb643499b854ae85645c3a4` and is shared by the
reflection-negative guard, fresh-package compiler probes, and production deletion ledger.

The historical-owner regression reads source archives at immutable commits
`ac46d99543daf85c0fa3234272997ba40f47f96b` and
`666bc1e6ec57eb055f3fecbb8f74a64ebe2e1ea9`. Every retained identity must resolve beneath the
named assembly source root to the exact namespace and type declaration. A member identity must also
occur inside that named type's balanced body. The parser ignores braces in line/block comments,
ordinary/verbatim strings, characters, and raw strings. Its in-process semantic control proves that
a sibling type or wrong namespace cannot satisfy the owner.

Required controls:

1. Restore `.Management` on any corrected ephemeral identity; the owner regression must fail.
2. Restore the later provider-abstractions assembly on either payload codec; it must fail.
3. Reintroduce any deleted invented identity; it must fail historical-owner resolution.
4. Make member matching file-wide rather than type-body scoped; the synthetic sibling-type control
   must fail.
5. Remove or mismatch a fresh-package marker, ledger identity, count, or ordered catalog digest; an
   independent compiler/baseline/ledger guard must fail.
6. Alter the Task 7.6 completion decision or audit artifact without updating the guard-owned pins;
   the owning guard must fail.

## Self-review evidence

The isolated mutation worktree established a green 2/2 control and then produced red results for:

- restoring the impossible ephemeral `.Management` namespace;
- reintroducing `WorkflowProjectionStatistics` under the recorded provider namespace;
- widening member lookup from the named type body to the complete namespace region; and
- deleting one OOO-1 natural-language regression entry.

After each mutation the copied target was restored byte-for-byte; the final control passed 2/2 and
the disposable worktree was removed.

## Scope and validation

The target has no `src/**`, canonical `openspec/specs/**`, package-manifest, or migration change.
Task 7.6 is the only newly completed task; Task 7.5 remains complete with an appended hardening
record. Task 7.7 remains open.

Reproduce:

- Debug and Release non-incremental `-warnaserror` solution builds: 0 warnings, 0 errors.
- Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification:
  350 / 79 / 99 / 37 / 24 / 96.
- PostgreSQL / SQL Server / Integration: 101 / 72 / 11.
- Fresh-package forbidden probes: 129/129; exact public baselines: 14/14.
- Infrastructure disposition: 226/226; ExpectedRed disposition: exactly 14 documented failures;
  full lane: 226 passed / 14 failed / 240 total.
- Focused Task 7.5 and Task 7.6 guards: 2/2.
- Strict OpenSpec: 18/18.
- Harmonization ledger: 30 complete / 4 open / 34 total.
- Current-match refresh `-Check`: clean.
- `git diff --check`: clean.

Reproduce the raw manifest and scoped content-record anchors supplied with the handoff. Simulate the
checkpoint from a copy of the live index, stage only the manifest paths, and confirm the resulting
commit contains exactly the frozen path set with no line-ending-only additions.
