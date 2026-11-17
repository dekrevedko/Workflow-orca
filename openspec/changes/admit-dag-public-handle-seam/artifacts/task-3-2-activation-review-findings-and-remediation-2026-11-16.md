# Task 3.2 activation review and remediation

Date: 2026-11-16. Original target: 29 paths on `94e9aca5632bd77d0c6ecde9d7fe689bcdee18d1`, tree `e959997f48873c57d018e5990f75352766518f43`, manifest SHA-256 `95bdb057358f6e5f3d26431b2bbcefadb2b8bd1756dcd53d9d71346804e62631`. Its raw manifest, request, owner direction and validation/self-review records remain byte-exact. The binary patch `task-3-2-original-29-path-target-2026-11-16.patch` preserves the entire original target against its stated base.

The owner requested: "Independently review the 29-path Task 3.2 activation target in X:/ocv302. Fix any findings, then checkpoint the reviewed result and push. If it's completed and ready, then PR check CI then merge to main". The repository's actual default branch is master; there is no main branch. The latest instruction authorizes review, remediation, checkpoint, push, PR and a merge after green CI. The unrelated dirty main checkout remains untouched.

Reviewer involvement: this agent authored the original activation target. This is a fresh adversarial review, with empirical probes, but it is not authorship-independent. It does not manufacture an external verdict or a new independent contract approval. The existing independent implementation and merge/remediation verdicts retain their exact scope and bytes.

## Finding R1 — P2: historical source freeze was only partly retired

`DagStreamIdentitySliceGuards.ValidateApprovedFingerprintAmendment` still compared the current DAG source/API paths and untracked files against its old base. `DagPumpSliceGuards.ReviewBoundDagSourcePaths` still compared live protocol/provider files with old approved bytes. Adding harmless future files under Dag and Dag.Hosting caused the canonical-transition history test to fail; adding a comment to DagRunAppendV1 caused the pump-history test to fail. The earlier positive probe had covered different paths and only one guard, so its broad retirement claim was insufficient.

Both guards now compare the exact reviewed merge commit instead of the live worktree. The historical canonical path comparison is likewise commit-bound. The changes preserve exact historical path sets, source bytes, API baseline records and real review chains. Current whole-canonical, API, friend-boundary, maintained-document, registry ownership, active-freeze and behavior checks remain responsible for current work.

The two positive probes now pass and all temporary source edits are restored. A negative probe changes the historical candidate record and still fails, proving that retirement did not turn off immutable-history validation. An initial probe invocation selected a sibling approval test that did not exercise the changed check; it was corrected before the recorded before/after experiment. No credit is claimed for that mistaken filter.

## Other review conclusions

The three original poll fixes remain justified: actual child entry proves admission; the provider's maximum lease is five seconds and maximum owner turn is fifteen seconds; the helper bounds acquisition and cleans up the queued request or granted fence. The original real-provider fault/restart results apply because this remediation changes no helper or runtime behavior. Fresh Core.Tests confirms the unchanged wall-clock rule remains green.

The current-status text identifies the two real approval/evidence chains, and the runtime-view approval is correctly scoped. Missing/incompatible child binding is explicitly host-configuration behavior outside the five seam requirements, as the earlier reviewer allowed. Exactly five rows become Complete; earlier owner chains and all reviewed canonical/API bytes remain unchanged. The strict shutdown after-state checks, merge-aware historical addition queries, schema assertions and task accounting remain intact. Other Section 8 tasks and archival are outside this target.

Reproduction details, counters and hashed logs are in `task-3-2-activation-review-validation-2026-11-16.json`. The new remediation request and manifest supersede the original freeze. The original target needed correction; after R1 passes the complete infrastructure lane and final freeze checks, the owner authorizes checkpoint and push followed by hosted CI. A separate evidence child records the actual checkpoint and final review disposition; no speculative commit identifier is embedded here.
