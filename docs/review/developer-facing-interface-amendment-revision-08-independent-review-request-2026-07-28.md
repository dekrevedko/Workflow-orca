# Independent review request — amendment Revision 8 / task 4.15

**Date:** 2026-07-28  
**Requested verdict:** `APPROVE` or `REJECT`  
**Scope:** proposal, amendment, delta, and planning coherence only  
**Authorization requested:** task `4.15` only

## 1. Correct lifecycle and gate effect

Revision 8 restores this required order:

1. proposal, amendment, active deltas, and change-local publication drafts;
2. independent approval under task `4.15`;
3. canonical synchronization under task `10.14`;
4. implementation beginning with task `4.16` and the gated remediation slice.

The reviewer must assess proposed text against the unchanged accepted canonical baseline. Approval
authorizes the implementation owner only to mark `4.15` complete and begin `10.14`. Approval does
not authorize product implementation, `4.16`, `5.10`, or `6.0`.

Tasks `4.15`, `4.16`, `5.10`, `9.10`, `9.12`, `10.14`, and `6.0` must remain open during review.

## 2. Target provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Target | `HEAD` plus the current dirty worktree, including this request |
| Self-inclusive sorted porcelain entries | 386 |
| LF-normalized sorted-status SHA-256 | `e53ef4ec598797237f92154fd7f8437487c1d9a8dcd9e1817ab713a1e34e297e` |
| Reshape tasks | 61 complete / 75 pending / 136 total; 0 duplicate IDs |

The historical Section 5 packet and every prior amendment request/verdict remain immutable. Do not
edit, replace, rename, or delete them.

## 3. Authority and review order

Review in this order:

1. accepted baseline `docs/specs/17-selected-mode-capability-matrix.md`;
2. unchanged `docs/specs/17-public-authoring-contract.cs`;
3. accepted canonical baseline under `openspec/specs/`;
4. active proposal, design, and deltas under
   `openspec/changes/reshape-developer-facing-interfaces/`;
5. coordinated `add-runtime-concurrency-limits` change;
6. Revision 8 amendment;
7. change-local `artifacts/semantic-appendix.md` publication draft;
8. live tasks and phased plan.

Historical requests and verdicts are evidence, not current normative authority.

## 4. Required determinations

### A. Proposal isolation

Confirm:

- proposed authoring-lifecycle, failure-provenance, fingerprint, root-fan-out, and
  `state-driven-runtime` text is absent from canonical specs;
- the capability matrix and ephemeral guide contain no Revision 8 application;
- `docs/specs/18-semantic-appendix.md` does not exist;
- the semantic appendix exists only as the change-local publication draft;
- product source and the exact public-authoring declaration companion are unchanged by Revision 8.

### B. Delta completeness

Confirm every proposed normative change is represented in an active delta. In particular:

- `state-driven-runtime` contains a MODIFIED requirement for
  `Interpreter executes control flow deterministically`;
- it replaces unreachable nested fan-out with supported nested `If` and linear child execution;
- every MODIFIED/REMOVED requirement resolves against the current canonical baseline or the
  historical `HEAD` baseline where earlier accepted synchronization already removed a requirement;
- no capability/requirement has duplicate delta operations.

### C. Amendment decisions

Re-derive all eight section 10 decisions:

1. scoped root-only observational simulation and its honest residue;
2. the root-fan-out reference model;
3. removal of `MaxActiveFibers` in all four roles with no replacement branch-width limit;
4. the `Open`/`JoinPending`/`Frozen` authoring lifecycle and restricted atomicity;
5. exact `FailureOccurrence` provenance and equality behavior;
6. the nine semantic laws, 15 citations, 15 exclusions, and reserved L4;
7. `state-driven-runtime` reconciliation;
8. compiler-format compatibility and fingerprint contributor ownership.

### D. Ordering and task state

Confirm:

- task `4.15` approves Revision 8 before canonical synchronization;
- task `10.14` performs canonical synchronization only after approval;
- task `4.16` and the remediation source slice are blocked by both gates;
- task `6.0` remains blocked;
- tasks `10.9` and `10.13` certify proposal coherence only and do not authorize synchronization or
  implementation.

## 5. Validation to reproduce

Run at minimum:

```powershell
openspec.cmd validate --all --strict --no-interactive
git diff --exit-code -- docs/specs/17-public-authoring-contract.cs
git diff --check
```

Also independently:

- parse every delta operation and resolve MODIFIED/REMOVED headings against the accepted baseline;
- prove zero duplicate capability/requirement operations;
- resolve every publication-draft citation to an exact `### Requirement:` heading;
- validate every local link in the Revision 8 proposal packet;
- count the fifteen excluded semantic claims;
- count task checkboxes and task-ID uniqueness;
- prove the capability matrix and guide have no Revision 8 diff;
- prove the published appendix is absent;
- reproduce the self-inclusive status count/hash;
- reproduce the immutable historical hashes.

The implementation owner recorded:

| Check | Recorded result |
|---|---|
| OpenSpec strict validation | 16 passed / 0 failed |
| Delta operations | 130 total; 50 MODIFIED/REMOVED; 0 unresolved; 0 duplicates |
| Baseline resolution | 49 current canonical; 1 historical `HEAD` requirement |
| Draft semantic citations | 15 resolved / 0 unresolved |
| Draft excluded claims | 15 |
| Proposal-packet local links | 25 checked / 0 missing |
| Tasks | 61 complete / 75 pending / 136 total; 0 duplicate IDs |
| Matrix diff | empty |
| Ephemeral guide diff | empty |
| Public authoring contract diff | empty |
| Published semantic appendix | absent |

These are claims to reproduce, not evidence to trust.

## 6. Verdict instructions

Write one new dated immutable verdict under `docs/review/`. Do not edit any existing request,
verdict, manifest, proposal, spec, task, plan, source, or test file.

Return `APPROVE` only if the proposal package is complete, canonical artifacts remain unmodified,
and no gate-blocking finding remains. Otherwise return `REJECT`.

Whether approved or rejected:

- leave task `4.15` open for the implementation owner to consume;
- leave task `10.14` open;
- leave tasks `4.16`, `5.10`, and `6.0` blocked;
- do not review or edit product implementation.
