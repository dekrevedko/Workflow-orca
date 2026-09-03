# Harmonization Task 6.4 rejection-remediation independent review request

**Date:** 2026-09-02
**Requested verdict:** `APPROVE` or `REJECT`
**Authorization requested:** create only the remediated Task 6.4 checkpoint commit

This request supersedes the rejected 2026-09-01 Task 6.4 freeze while preserving its request,
manifest, artifact, and verdict byte-for-byte. The new target remains on base
`c23dc4e732881fb2ddb7792836e740d72ce2644f` and is named by
`harmonize-downstream-capability-specs-task-6-4-remediation-dirty-manifest-2026-09-02.txt`.
Exact raw commit-real porcelain, scoped content-record, and capability-inventory anchors are supplied
with the handoff after this request and the active-freeze registry entry are final.

An `APPROVE` verdict authorizes only this remediated Task 6.4 checkpoint. It does not complete Tasks
6.5-8.3, archive the harmonization change, or authorize reshape Task 8.0.

## Rejection to verify first

- The immutable verdict
  `harmonize-downstream-capability-specs-task-6-4-independent-review-verdict-2026-09-01.md`
  is exactly 13,640 bytes with SHA-256
  `3f972188281bced25dbcd0ac0a6709b611e1660c91ff1da258c7cc46bd74a79d` and terminates in `REJECT`.
- The original ten-path manifest remains 795 bytes with SHA-256
  `3fb52cfea40ec2f0d083ca20355f5c5c18734c13aee0e56aaa382e8ce670f0e9`.
- The provenance fixture records its complete 1,502-byte historical content record with SHA-256
  `00027a858d2d92e7627a0e20b0f7e714babb6514ca83dbacb6d0792127c611e9`.
- No checkpoint commit or tree is attributed to the rejected target.

## Remediation claims

1. The deleted fan-out-rank excluded-claim bullet is restored byte-for-byte in its original position.
2. The entire normalized `## Deliberately excluded claims` block is 1,182 bytes and SHA-256
   `a989ad5ea0773cdd22eb13b65194651b61035eef9da469369134921b730c56b1`.
3. Task 6.4 now rejects the retired `MaxActiveFibers` token in both Markdown and C# documentation
   under `docs/` and `openspec/`, except immutable `docs/review/` evidence.
4. Product source still contains zero token occurrences; test source is explicitly outside the
   documentation inventory.
5. The original 2026-09-01 disposition artifact remains immutable; the dated remediation artifact
   records R-1, V-2, V-3, the rejected evidence, and the complete negative-control set.
6. The inherited Task 6.2 successor-boundary correction remains load-bearing and state-independent.
7. Crosswalk accounting remains 337 physical sources / 1,388 declarations and 189 active files /
   700 active declarations; retired declarations remain 866.
8. No `src/**`, `openspec/specs/**`, public API baseline, or package-manifest change exists.

## Mutation controls

Reproduce at least these controls against a byte-restored target:

- delete the fan-out-rank bullet: red on the whole-block hash;
- add a contradictory excluded claim: red on the whole-block hash;
- add the retired token to `docs/specs/17-public-authoring-contract.cs`: red;
- add an active Markdown occurrence: red;
- add a product-source occurrence: red;
- reopen Task 6.4: red;
- add a historical `docs/review/` occurrence: green by design; and
- revert the Task 6.2 boundary repair: red in Task 6.2.

## Validation to reproduce

- Debug and Release full solution builds, `--no-incremental -m:1 -warnaserror`: 0 warnings /
  0 errors.
- Core 350; Ephemeral 79; Durable 99; Acceptance 37; Hosting 24; ProviderCertification 96.
- PostgreSQL 101; SQL Server 72; Integration 11, run sequentially.
- Infrastructure guards 219/219.
- Expected-red guards: exactly the same 14 `ExecutableBehaviorExpectedRedGuards` failures.
- Full guards: 219 passed / 14 expected red / 233 total.
- OpenSpec strict 18/18.
- Harmonization ledger: 22 complete / 12 open / 34 total.
- `git diff --check`: exit 0; zero `src/**` and zero `openspec/specs/**` entries.

## Reviewer instructions

Recompute both freeze anchors from the commit-real target and compare the old rejected evidence first.
Review the semantic appendix diff against base, its source artifact, and the rejected diff. Mutation-
test the whole-block hash and both documentation extensions. Confirm no reviewed file was rewritten,
no rejected checkpoint is implied, and no content outside Task 6.4 remediation entered the target.
Do not edit, stage, or commit the reviewed target. Return one dated immutable `APPROVE` or `REJECT`
verdict for this remediation freeze.