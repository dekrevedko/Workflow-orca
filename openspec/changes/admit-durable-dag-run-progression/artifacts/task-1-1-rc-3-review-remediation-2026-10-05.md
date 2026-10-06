# Task 1.1 RC-3 and round-97 remediation record

Date: 2026-10-05. PROPOSED only; all THREE earlier contract freezes are REJECTED.
Base dab498ff54321d802b36ce052100954bbe00487d is unchanged. No source/canonical/friend/API-baseline/test-
disposition changes. Lease protocol and immutable receipt semantics remain resolved.

## Dispositions

- RC-3 separate bound and isolation: internal DagEvaluationParallelismPerRun=1,
  dedicated run lanes and nonblocking round-robin dispatch. MaxConcurrentNodes is
  ONLY the per-run admitted-child ceiling. A stuck/cancelled mapper retains its own
  thread, not a shared host permit. Total workers scale with live or retired evaluated
  runs; no universal finite process-resource/progress guarantee is invented.
- RC-3 bounded speculation: evaluate only the lowest Ready ordinal in the free
  child window; F=0 starts none; zero look-ahead, one evaluation OR receipt and one
  coalesced admission request per run/host. No 10,000-receipt fan-out.
- RC-3 ordering: successful fenced commit requires F>0 and lower Ready count<F,
  with the stronger serial rule lower Ready count=0. CapacityDeferred retains the
  receipt for capacity/order waits. A NEW lower Ready node invalidates a higher
  receipt for later precommit reevaluation, avoiding single-host lane deadlock;
  time/unrelated version/full capacity do not. No overlapping worker replacement.
  Cross-host duplicate mapping is explicitly bounded one/run/boot host, not exactly once.
- P3-1: the exact ChildJoined table now distinguishes success readiness from
  non-success transitive Pending/Ready -> DependencyBlocked closure in the same append.
- P3-2: unaddressed ready hints acknowledge validated/coalesced bounded wake acceptance
  BEFORE mapping; no claim renewal or mapper-duration claim. Continuous authoritative
  nonterminal discovery wraps each full pass with <=5 seconds between passes and
  recovers acked/lost/poisoned hints and death during evaluation. It is not terminal-
  wait polling, and poison never fabricates terminal failure or frees admission.
- P3-3: sixteen doc blocks make RC-3 primary; RC-1/RC-2 links are explicitly rejected
  history. The misleading live-mapping-owner scenario is renamed. Dated decisions
  retain their original bytes, with a new proposed clarification appended.

DG-P22–24 require REAL-storage ordering ties on one/two hosts, slow/stuck cross-run
isolation/bounds, and wake/host-death recovery. They are future source/provider exits,
NOT newly implemented runtime tests. The unchanged exact public proposal remains
365 additive keys (7/193/165), with no removals. Doc 08 CP-025/027, doc 12
AC-609/611/617 and doc 17's distinct-limit taxonomy are explicitly dispositioned.
Only the same four heading identities change. New SHALL obligations remain before
scenarios. Task1.2 stays open and checkbox-unpinned; 1.3–3.2/reshape8.4/8.5 stay open.

## Third rejected freeze reconstruction

Manifest docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-refreeze-dirty-manifest-2026-10-05.txt: 4715 bytes /
5fd6806e0721312621af4353945a1955c943f63f927c79ebb94a30a596f93943.
Request docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-refreeze-independent-review-request-2026-10-05.md; verdict docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-refreeze-independent-review-verdict-2026-10-05.md: 24510 bytes / 3cbba061ca4f819ae310d997434de57b12efd2b7bf6c7ad1071712a19dc6ae6e.
Rejected rehearsed tree b81c0da8585f4c93fb1faa9e6a16768ee6a209b3; NOT a main checkpoint. Registered as
reshape-dag-run-contract-rc-3-rejected. First/second rejected reconstructions remain
unchanged in their own immutable remediation artifacts. The earlier unreviewed
RC-2 draft packet is retained byte-exact as disclosed history, not an approved freeze.

Render status<TAB>path<TAB>RAW byte length<TAB>lowercase RAW SHA from these rows.
Exclude ONLY review-manifest-provenance.json; ordinal-sort FULL rows, LF-join with
one terminal LF; UTF-8 without BOM. Exactly 52 rows / 8311
bytes / 7980b02f023451ca63f437e0d69bf2782f94a3c8c69188d0aa90b2641d853a4e. All 53 historical rows are written below.
No historical dirty record is falsely equated with an unmade checkpoint.

| Status | Path | Bytes | Raw SHA-256 |
|---|---|---:|---|
|  M | `CLAUDE.md` | 22156 | `a28bce783599b30bce0b4c7a3e459137094b034441f29270e01f657c74965b35` |
|  M | `docs/durable-driver-lane-host.md` | 4662 | `eec06de6f07d8cc2b346c2f6b108477c9eb8fc54a85315f8b706dde71e817883` |
|  M | `docs/implementation/00-stack-decisions.md` | 30764 | `bb658c32d4dc1498f8b1010b3db586d507979136fbce3096fb19df23afbdae0e` |
|  M | `docs/implementation/01-solution-architecture.md` | 22265 | `05df1ec6f1172ddacb11ea1393d918143ca7a1d7d0788adae6745bb3bc604f5e` |
|  M | `docs/implementation/README.md` | 13287 | `59be1b8b67ddbf1b136e3995bbd9247d41689bbf7b2b05a0fe9b478246b87a0c` |
|  M | `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md` | 43568 | `768217a4bbcc2da4cdf8ef1f88edf880cb10ad5ba1069f89589616cdf6ff4158` |
|  M | `docs/project-technical-overview.md` | 12248 | `6d97302876f2c6e0b6be87e06e50121850c411e7a02e6ed6b6d7d5c653e8df3c` |
|  M | `docs/specs/03-domain-model-and-glossary.md` | 27823 | `d5e672b64dfa3f6674a01a7bd89c555b00b522a630c8e4a6f3b163681991d7b5` |
|  M | `docs/specs/04-requirements-core-runtime.md` | 27959 | `00591b6436ab7f269890292ae1806d268d1b0d29e01efb571c39dc3847c84b26` |
|  M | `docs/specs/05-requirements-events-waits-timers.md` | 15173 | `6cbe921fc48216e9cab05487fc514a8d490cdae814a2841748f503e5bf5acf03` |
|  M | `docs/specs/06-requirements-durable-execution.md` | 18672 | `52e8e9382ae8f5d50aed21584c7e77e3d1e3c369a8739a1f5e7d6b142104ca3e` |
|  M | `docs/specs/08-requirements-composition.md` | 20000 | `80871d1f4eb05994b48e41dc58668d773c43e44ab1a895369e0dd6ef25df96c6` |
|  M | `docs/specs/10-provider-model-and-extensibility.md` | 18428 | `1775208f2f1d9dd97cb1e6b37b676becfc6eab47cc49ff954c3350e1f887debe` |
|  M | `docs/specs/12-acceptance-criteria.md` | 47559 | `07dbac8b4bbbe88e86414aaf1f8897b738c9935cde450591a09691531a1f5f0c` |
|  M | `docs/specs/16-requirements-durable-driver.md` | 60451 | `bfdf3f785f8944a459a2af8f842291b3ad7b52e08a527f2db6341729dbbffb86` |
|  M | `docs/specs/17-selected-mode-capability-matrix.md` | 155130 | `fc7f88d02250834582e4253ba685cf28287c334ef4e10974b2cc310bc3b05ef4` |
|  M | `openspec/changes/admit-durable-dag-run-progression/design.md` | 15281 | `a7e73ae2b83fdee5827c41c984dd0229e812f01a3dfa2f279656ed7623492b7f` |
|  M | `openspec/changes/admit-durable-dag-run-progression/proposal.md` | 4669 | `981c70976b526489f4fca1992109545b0b7e890b7d235df8466e19208c7e01f0` |
|  M | `openspec/changes/admit-durable-dag-run-progression/specs/durable-persistence-and-outbox/spec.md` | 12380 | `0b2475bb2c84b71181395b9ace4e1c7a3502474ee23a645f23305bd9098d9f13` |
|  M | `openspec/changes/admit-durable-dag-run-progression/specs/durable-runtime/spec.md` | 9582 | `c654cf36fb268eedbd4ccefd910c061e05775e95ed8a5261cd05986059398577` |
|  M | `openspec/changes/admit-durable-dag-run-progression/tasks.md` | 4825 | `40b5eb74fce8eb6d6fabb894d5677f96145fd3c5b41f4de9ff4d5ec2ab34a6b7` |
|  M | `openspec/changes/harmonize-downstream-capability-specs/artifacts/task-7-3-active-documentation-reconciliation-2026-09-18.md` | 9188 | `8bd561b01dc5b7267da808ecc8802401d2d0bdf83d65512acb96b26562517e52` |
|  M | `openspec/changes/reshape-developer-facing-interfaces/artifacts/task-8-0-section-8-requirement-gate-2026-09-26.md` | 21060 | `a0da8a26995a3151b3225a70e2b3f29b6e796198c33f0bc117c422207e336d62` |
|  M | `openspec/changes/reshape-developer-facing-interfaces/tasks.md` | 79594 | `bce2ac6262f9bdb613285a8246f4e4f8b3e1bd0095b0eaad8da4081e0f5e1640` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json` | 123810 | `b3cc758fa5f078056b47184dbe4fd798b83d98f408a0f46079d8a83e78651383` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json` | 15837 | `f6c534f7393998d3f9316cc7645a1d4913d705805dae8ae2478d12822fe2517c` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json` | 18151 | `30e36b96b828be09594bffe48b882df8048f1b251762534842835ace67bf4f54` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json` | 157336 | `2d401ee7c9856bfcf41b92adcd243753b6275ba15b80a7fd7ca0ba8bb4024cb4` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/OpenSpecCorpusGuards.cs` | 364356 | `40584d16222b588d0d7e008ec3bc5e03cdea46eaac4fd49434be3bab8a410e48` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-dirty-manifest-2026-10-04.txt` | 2471 | `fa3604972b2dca65a55bc753841323af9171a6438a0011be26b64ca330658a02` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-independent-review-request-2026-10-04.md` | 13827 | `842cbec6023f214e88e2fb3ad5deaa89ee82b0760ef749adca873c346a9fbff6` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-independent-review-verdict-2026-10-04.md` | 15033 | `5f2d075197e5390648d8b65fc4b2c0b3f8543f1dca06739eb4bf1fb137231229` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-dirty-manifest-2026-10-04.txt` | 3456 | `ba7b90e912eff5fd2db3c7d2253a74d34390872a02e411543e91595919ca6e6d` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-independent-review-request-2026-10-04.md` | 9224 | `7c814469c29d399094a41d739740e53e1fb41b3160811f39a8b21b553af346fb` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-independent-review-verdict-2026-10-04.md` | 21654 | `0ef31625a268066ba81d8f849447e15099b93b70a3080c0b8fda4dd087169415` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-refreeze-dirty-manifest-2026-10-05.txt` | 4715 | `5fd6806e0721312621af4353945a1955c943f63f927c79ebb94a30a596f93943` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-refreeze-independent-review-request-2026-10-05.md` | 8638 | `052739c64629009fd3b5cbf35b869ba8c1c4253700312a280e83dec9f723e674` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-remediation-dirty-manifest-2026-10-05.txt` | 4458 | `c32708838b3372a326ca7372be83ce1c50f63cad6ad78506374183855054844e` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-remediation-independent-review-request-2026-10-05.md` | 8309 | `cc0a13187f64ad7841f00f561691ca2b3e0dc1e45c687b0a7a0ec83bb156ebd9` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/contract-openspec-provenance-2026-10-04.md` | 4232 | `056ab2f1988260f2d7739e30840af1aabffffd8b27127cf308ae6d7f244b1a3a` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/contract-rc-1-remediation-openspec-provenance-2026-10-04.md` | 4293 | `82221fa6f9c6a6008906016093f3896eb0302ebee04b64b241afdd4d2574c0e6` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/contract-rc-2-remediation-openspec-provenance-2026-10-05.md` | 4293 | `ed72ca1557b8de8f288aa4f4daab827ec1805e80054b003efb13de4919e8abde` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-2026-10-04.md` | 62837 | `61afd2af3b2e6a633bdafc113080de518195970d34e084815537784ddff7e0f8` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-rc-1-remediation-2026-10-04.md` | 71325 | `3b776f5884681a02313372589c85b237e3e053590b1081c295be669cb7ef30e0` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-rc-2-remediation-2026-10-05.md` | 74904 | `a11d2c44ce97baa62840545732d412b2d81710f39f56be39a8c892791eee9aeb` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-rc-1-review-remediation-2026-10-04.md` | 8916 | `12e166d5e3177a530b04715af91aac36cd81ebff9aaedd98d967396c9e87d014` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-rc-2-review-remediation-2026-10-05.md` | 11485 | `bb9269d9b489de36a3fb4d4dbfb4a1533e93d11a7cc725b9ad28e451d438a5c3` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-2026-10-04.md` | 43056 | `a792b9d44968612d852fe772e1b7195c4880291530895aa082453fde23729d7b` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-1-remediation-2026-10-04.md` | 58168 | `24fa34f3957bbfe8c288dc74163562d4b808763193f114e48fc2e8bc78009ac9` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-2-remediation-2026-10-05.md` | 71351 | `4cab5d4e8a6385505e6875e26767d92b728b740f98d2a70a489989f48552ec2b` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-2026-10-04.cs` | 11220 | `5dab933db0b6f4aff8903e26f8f535ebd1478a6f66866e916e699cfe57277c75` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-rc-1-remediation-2026-10-04.cs` | 13208 | `f655afc1b72e1c3d2af7d56ed23d72581d73d8b8b5c97586e4c695abb3d25918` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-rc-2-remediation-2026-10-05.cs` | 13605 | `8301094ba0a14634f9c8ba08ac2642eb2b8e5128981da453ecd0623762926189` |

The new packet/handoff supplies fresh anchors, tree and completed validation. No
checkpoint, source, archival or canonical synchronization is authorized here.
