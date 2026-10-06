# Task 1.1 RC-1 and P3 remediation record

Date: 2026-10-04. Proposed only; the prior 35-path target is REJECTED, not checkpointed.
Base remains dab498ff54321d802b36ce052100954bbe00487d. Rejected rehearsed tree cde03fd24984418ccc40886c28f848e97ff795c4 is historical evidence,
not a main commit. No source/canonical/friend/API baseline is changed in this target.

## Dispositions

- RC-1: Dag.Hosting owns admission/mapping coordination. Durable.Hosting owns bridge
  lease access, provider serialization, joins and cancellation. Begin/Renew/End/Abandon
  and a captured-turn child adapter are exact internal signatures, not provider bypass.
  Persisted FIFO requests, one append per turn, 5s lease/15s absolute cap, release-before-
  idle and independent watchdog prevent remote join/cancellation starvation. DG-P14–16
  require real-storage controlled-time fairness/crash/ceiling evidence before source.
- P3-1: the repeated process-only scenario is replaced with behavioral scenarios for
  fencing, fairness, role-free pumps, wrong-lane release, capacity, decode and null bytes.
  Canonical statement and original scenarios remain byte-exact; SHALL text is above them.
- P3-2: 30s observer subscription expiry and <=10s maintenance sweep remove dead-host
  addressed notices once claim leases end; no progression/child/poison truth is purged.
  Observer maintenance counts are separate from business pending pressure (DG-P17).
- P3-3: created-only full snapshot plus compact changed-node/value-write events, one
  overwritten current checkpoint and constant-size operation receipts bound retained
  space. Full checkpoint write amplification is explicitly retained/disclosed (DG-P18).

The original contract, API catalog and standalone signature artifact remain byte-exact
and durably source-pinned. New files with rc-1-remediation names are their successors.
The old provenance artifact remains unedited in the permanent superseded catalog.
Proposal/design/tasks and sixteen explicitly proposed doc blocks are re-pinned here.
Task 1.2 is unpinned for checkbox-only activation; 1.3–3.2 and reshape 8.4/8.5 stay open.
No fifth capability/heading, acceptance credit, waiver or expected-red disposition changes.

## Rejected freeze reconstruction

Original manifest: docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-dirty-manifest-2026-10-04.txt, 2471 bytes,
fa3604972b2dca65a55bc753841323af9171a6438a0011be26b64ca330658a02.
Request: docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-independent-review-request-2026-10-04.md. Verdict: docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-independent-review-verdict-2026-10-04.md, 15033 bytes,
5f2d075197e5390648d8b65fc4b2c0b3f8543f1dca06739eb4bf1fb137231229. Request/manifest/verdict are cataloged byte-exact and bound
by rejectedFreezes id reshape-dag-run-contract-rc-1-rejected.

Render status<TAB>path<TAB>raw bytes<TAB>lowercase raw SHA-256 from these rows,
exclude ONLY review-manifest-provenance.json, ordinal-sort whole rows, LF-join
with one terminal LF. This reproduces 34 rows / 4797 bytes /
38c40cc6de495e559445d9e96cf6056646a7707fe1d807b72fc629a1d07e79ad. These are historical dirty bytes, not current files.

| Status | Path | Bytes | Raw SHA-256 |
|---|---|---:|---|
|  M | `CLAUDE.md` | 20662 | `65a069a678bd1cd3c61ae0a0175641d40a21007416367af05acd64842a3cb646` |
|  M | `docs/durable-driver-lane-host.md` | 3165 | `3b372b3f998d3c8d72f475192c0777052c086c07717244e3ca4ad0748937f0a6` |
|  M | `docs/implementation/00-stack-decisions.md` | 29264 | `b8989fbf887e89dd3e9c1d569e67174f8032c556b16e982a9dddf91e7287326a` |
|  M | `docs/implementation/01-solution-architecture.md` | 20765 | `7aee50566523ab3ffb7c0c54af7c654381fbc7122068709f13f090b040c3a4b8` |
|  M | `docs/implementation/README.md` | 11787 | `6d283a4165d0e8e15e4666cc68214bdf8bf06e8a1a32b45e8c047ed62a7b8242` |
|  M | `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md` | 42068 | `cdab684ea7133738bf3629c350bb8c765249b1f9bc5ea351d44bc387f3a6bec6` |
|  M | `docs/project-technical-overview.md` | 10751 | `f11ac3e4b39705a4dbe112426d906934052853e66b376a3a46578a799ac15c20` |
|  M | `docs/specs/03-domain-model-and-glossary.md` | 26323 | `502a05b6695359e932fbe4869ffe25f9b65e18943180ce2e2b7f60ad5ecce66a` |
|  M | `docs/specs/04-requirements-core-runtime.md` | 26459 | `86cd43a151ea2aff408f73ad680028d2af77d060baa4eb0c0fff25114cecafd3` |
|  M | `docs/specs/05-requirements-events-waits-timers.md` | 13673 | `c8f5c5e98ab60404f58c875d479bf3c7a8086b6bfa151aa3f2adca326a4d8c8e` |
|  M | `docs/specs/06-requirements-durable-execution.md` | 17172 | `b6628eaf76f43e2ce913950090d1b86a37b80d056e0325b9381dc2683c8cca5a` |
|  M | `docs/specs/08-requirements-composition.md` | 18500 | `5949e6795aba734b9ccbab8f8e025b57f6dbc2842f637544ce38f4602e8fc2dc` |
|  M | `docs/specs/10-provider-model-and-extensibility.md` | 16928 | `93b861f8c76c474ca1afdebe027a2bf842a8a8f6d9888b7fb2748e2d7a251737` |
|  M | `docs/specs/12-acceptance-criteria.md` | 46059 | `ebf442abd7ec045944dab2a3cbc7a5166a65725550779366acd68a42d54d7d80` |
|  M | `docs/specs/16-requirements-durable-driver.md` | 58951 | `5a3af63a64a42b8373fc6233e599c2c538d691464e32bb0ad53d9d9df3188b7d` |
|  M | `docs/specs/17-selected-mode-capability-matrix.md` | 153630 | `60f7f7b466f433914b3074d473851e77fe4a65a960bb259757a5509708fae768` |
|  M | `openspec/changes/admit-durable-dag-run-progression/design.md` | 12932 | `4e2331124c4adb5d889c594f22d83bbe2fb12ed41732dd32cc44c6968e49f4de` |
|  M | `openspec/changes/admit-durable-dag-run-progression/proposal.md` | 3513 | `a049b55a3698a0c24aff04f4bc0c3bdff40b5934063290a8172a32cda66d3acf` |
|  M | `openspec/changes/admit-durable-dag-run-progression/specs/durable-persistence-and-outbox/spec.md` | 8860 | `c53598bb7ad7a38e56ca9dcd5c0938390e5baa5d77f50e80ca30dd66a4c55a85` |
|  M | `openspec/changes/admit-durable-dag-run-progression/specs/durable-runtime/spec.md` | 6258 | `66596b907f0546572dd65d04f99c53a909bcb12fa2caa91e1c9b9c1752060892` |
|  M | `openspec/changes/admit-durable-dag-run-progression/tasks.md` | 3980 | `a2695974be3572a81daf64efa8a9ad24bb7dda88903806fc6dc37f1d4059a1c0` |
|  M | `openspec/changes/harmonize-downstream-capability-specs/artifacts/task-7-3-active-documentation-reconciliation-2026-09-18.md` | 8557 | `93073a05fefe4b292d253099ef2ac5013878502ce4b2272d2365601768f0989e` |
|  M | `openspec/changes/reshape-developer-facing-interfaces/artifacts/task-8-0-section-8-requirement-gate-2026-09-26.md` | 20599 | `d4f38bf0617051a59cb8e5edaea373f57bce230e48448b684b8f31556bb7ce6a` |
|  M | `openspec/changes/reshape-developer-facing-interfaces/tasks.md` | 78932 | `83617624cdbb97d2ee116622e3efd4c2853e04b3b0df588abb3b9eef545fc3f6` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json` | 121706 | `a7dc72f997119b5d2c6f10814b53e6c124c81ccae8a112c7f53d68af595a6e47` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json` | 15820 | `aa30621f26bdd8734aa622df9cfedee750a1b4107be27d9f089bbf0f62a6c4c6` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json` | 18151 | `5de63a01e569bd7365ae3f071fa66f751389ae87fc1ee50e1f4dc94bbf0c1617` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json` | 155025 | `9544fa86fa71bb687671985380d265ea33e14c8be2d1bc6140ddbc63f4814325` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/OpenSpecCorpusGuards.cs` | 361802 | `771e8dcb9cfe97c0334c0f77be4c8fb8809cfa5165742b1c3e75805fe6ebc6a8` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-dirty-manifest-2026-10-04.txt` | 2471 | `fa3604972b2dca65a55bc753841323af9171a6438a0011be26b64ca330658a02` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-independent-review-request-2026-10-04.md` | 13827 | `842cbec6023f214e88e2fb3ad5deaa89ee82b0760ef749adca873c346a9fbff6` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/contract-openspec-provenance-2026-10-04.md` | 4232 | `056ab2f1988260f2d7739e30840af1aabffffd8b27127cf308ae6d7f244b1a3a` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-2026-10-04.md` | 62837 | `61afd2af3b2e6a633bdafc113080de518195970d34e084815537784ddff7e0f8` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-2026-10-04.md` | 43056 | `a792b9d44968612d852fe772e1b7195c4880291530895aa082453fde23729d7b` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-2026-10-04.cs` | 11220 | `5dab933db0b6f4aff8903e26f8f535ebd1478a6f66866e916e699cfe57277c75` |

The current packet records its OWN new anchors and validation, never reuses these
as a claim of approval. Canonical sync and source remain blocked pending review.
