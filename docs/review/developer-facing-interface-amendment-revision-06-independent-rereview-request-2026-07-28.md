# Independent rereview request — amendment Revision 6 / task 4.15

**Date:** 2026-07-28  
**Requested verdict:** `APPROVE` or `REJECT`  
**Scope:** planning and documentation mapping only  
**Implementation authorization requested:** task `4.15` only

## 1. Gate and stop conditions

Revision 4 was rejected because its exact documentation mapping was incomplete. Revision 6
remediates that mapping without changing product source.

The reviewer must keep these tasks open while reviewing:

- `4.15` — pending this independent verdict;
- `4.16` — must not begin before an unconflicted approval of Revision 6; and
- `6.0` — remains blocked regardless of this verdict.

An approval authorizes the implementation owner only to mark `4.15` complete and begin tasks
`4.16`–`4.21`. It does not approve product conformance, Section 5 exit, task `6.0`, or Section 6.

## 2. Review target and provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Target | `HEAD` plus the current dirty worktree, including this request |
| Sorted porcelain status entries | 387 |
| LF-normalized sorted-status SHA-256 | `fd8e1914ca6b00eec19fb974ae4abb5b608246a548302926e13bc4634702b97a` |
| Historical Section 5 manifest | 370 entries; SHA-256 `F001016F92CF056AA1CF6E99203353112504500C1C5E582625AE3B607A41BBAD` |
| Historical Section 5 request SHA-256 | `53FCB5818FAE78F725B1676A2EBAF6FAB00C1595AC83FC7BD1BCF3DCE17BFA2E` |
| Conflicting Revision 4 approval SHA-256 | `055D4CA879EC7B431F7D92F14074DCD7BA2E61CBD66DD2F38028D188E79B758C` |

The historical 370-entry request/manifest and every prior verdict are immutable. Do not edit,
rename, delete, or replace them. The conflicting Revision 4 verdicts remain evidence; Revision 6
supersedes Revision 4 only as the live gate target.

## 3. Authority order

Review in this order:

1. `docs/specs/17-selected-mode-capability-matrix.md`
2. `docs/specs/17-public-authoring-contract.cs` — expected to have no diff
3. affected canonical requirements under `openspec/specs/`
4. active deltas under
   `openspec/changes/reshape-developer-facing-interfaces/specs/`
5. `openspec/changes/reshape-developer-facing-interfaces/`
   `AMENDMENT-2026-07-28-root-only-fanout-and-authoring-lifecycle.md`
6. `openspec/changes/reshape-developer-facing-interfaces/tasks.md`
7. `docs/specs/18-semantic-appendix.md`
8. `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`

The exact public builder declaration companion is deliberately unchanged. Fixed `Parallel`,
bounded `ForEach`, and `While` remain root-sequence-only; only `If` nests.

## 4. Revision 6 corrections to rederive

Do not trust the implementation owner's validation claims. Reproduce each item:

1. **L6 citation:** the semantic appendix links to the active reshape durable-runtime delta, and
   that target contains the exact requirement
   `Durable leases are lexical occurrence-owned obligations`. The appendix must state that
   canonical promotion remains Section 6 work and must not imply current product conformance.
2. **Matrix registry preamble:** §17.6 contains the complete authoring-shape scoring rule,
   dated v1 residue, recovery distinction, and nested-fan-out re-entry bar. The bar must say
   **every applicable root-only encoding**, admit budget-acceptance evidence, and disclaim general
   equivalence.
3. **`state-driven-runtime` traceability:** pending task `10.9` explicitly owns creation and
   verification of the missing reshape change delta for the already-applied canonical rewrite.
   This named later owner was accepted in the consolidated disposition; the task must remain open.
4. **Semantic exclusions:** the appendix contains all fifteen deliberately excluded claims,
   including reachable `TryBuild` after eager diagnostics and the rejection of “fan-out rank one”
   as a computational-complexity claim.
5. **L4 gate:** every live statement withholds L4 until implementation task `4.16` lands.
6. **Task state:** `9.10`, `9.12`, and `10.13` are reopened; `10.9` remains pending; reshape
   accounting is 60 complete / 75 pending / 135 total with no duplicate identifier.
7. **Review state:** the amendment and phased plan identify Revision 4 as rejected, Revision 6 as
   the corrected live target, and tasks `4.15`, `4.16`, and `6.0` as blocked.
8. **Scope:** Revision 6 changes documentation/planning only and leaves
   `docs/specs/17-public-authoring-contract.cs` unchanged.

## 5. Required validation

Re-run at minimum:

```powershell
openspec.cmd validate --all --strict --no-interactive
git diff --exit-code -- docs/specs/17-public-authoring-contract.cs
git diff --check
```

Also independently:

- resolve every semantic-appendix citation to an exact `### Requirement:` heading;
- validate every local Markdown link in the Revision 6 packet;
- count task checkboxes and test task-ID uniqueness;
- reproduce the target sorted-status count and SHA-256 before and after review; and
- reproduce the three immutable hashes in §2.

The implementation owner recorded:

| Validation | Recorded result |
|---|---|
| `openspec.cmd validate --all --strict --no-interactive` | 16 passed, 0 failed |
| Semantic citations | 15 resolved, 0 unresolved |
| Local Markdown links in corrected artifacts | 29 checked, 0 missing |
| Reshape tasks | 60 complete, 75 pending, 135 total; 0 duplicate IDs |
| Public authoring contract diff | empty |
| `git diff --check` | exit 0 |

These are claims to reproduce, not evidence to trust.

## 6. Verdict instructions

Write exactly one new dated immutable verdict under `docs/review/`. Do not edit any existing file.
Record:

- `APPROVE` or `REJECT`;
- target provenance and before/after status-set comparison;
- a determination for every item in §4;
- exact validation results;
- every finding with priority and whether it blocks task `4.15`; and
- explicit confirmation that task `6.0` remains blocked.

An `APPROVE` is valid only if no gate-blocking finding remains. Otherwise return `REJECT` and leave
task `4.15` open.
