# Developer-facing interface Section 6 canonical entry review

**Date:** 2026-07-29  
**Repository:** `X:\Projects\GitHub\Workflow-orca`  
**Branch:** `feature/v3-rebuild`  
**Change:** `reshape-developer-facing-interfaces`  
**Disposition:** Section 6 source work is authorized; Section 7 remains blocked.

## Gate evidence

The independent Section 4/5 remediation re-review at
`developer-facing-interface-section-04-05-amendment-remediation-independent-rereview-verdict-2026-07-29.md`
approved the exact 414-entry frozen target and authorized task 6.0. Before this review, the live
worktree contained those 414 entries plus only that approval verdict. Removing the verdict from
`git status --short` reproduced the frozen manifest exactly: 414 entries, zero differences.

Repository provenance also reproduced:

- baseline `8c2dd712284f3b638f9bf812ad2172f24d0a8863` is an ancestor of `HEAD`;
- `HEAD` is `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`;
- the `HEAD` tree is `2264e670493ecc76359d42ee5273028eb287a566`; and
- `openspec.cmd validate --all --strict --no-interactive` passed 17/17 before Section 6 edits.

## Canonical mapping review

Decision 17 maps Section 6 to deadlines, durable runtime, management, leasing, and quality. The
approved Revision 8 synchronization already placed the required contract in canonical specs, so
task 6.0 requires review rather than another normative rewrite.

| Area | Canonical owners reviewed | Section 6 implementation boundary |
|---|---|---|
| Deadlines and authoring | `workflow-authoring`, `workflow-contracts` | One root-only positive finite `CompleteWithin`; operation-local step and wait timeouts; eager duplicate/decorator diagnostics; authoring-session ownership |
| Durable execution | `durable-runtime`, `workflow-contracts` | Persist the workflow deadline and complete operation coordinate before dispatch; preserve it across replay, replacement hosts, and continue-as-new; retry only after committed eligible transitions |
| Management | `management-and-querying` | Application projections retain typed business facts and terminal timeout failure while lease quarantine/recovery remains an advanced diagnostic surface |
| Durable leasing | `workflow-authoring`, `workflow-contracts`, `durable-runtime`, `management-and-querying`, `runtime-resource-governance` | Factory-only request values, scoped acquisition, inclusive ancestry defense, persistent obligation identity, exact capacity reservation, quarantine, trusted stop confirmation, governance reconciliation, and resize semantics |
| Verification | `quality-and-verification` | Executable deadline/operation-coordinate, lease lifecycle, recovery, cancellation-race, conservation, provider-fence, public-surface, and expected-red disposition checks |

No mapped canonical requirement conflicts with the selected-mode matrix or exact public authoring
companion. Section 6 may implement those requirements in the current physical assemblies. Package
ownership, final facade relocation, and provider package topology remain Section 7 work and must
not be pulled forward.

## Entry decision

Task 6.0 is complete. The implementation owner may proceed with Section 6 after recording the
task-6.12 scoped-deadline decision. This record does not complete any product task and does not
authorize task 7.0.
