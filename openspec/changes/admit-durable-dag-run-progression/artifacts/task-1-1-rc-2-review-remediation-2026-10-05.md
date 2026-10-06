# Task 1.1 RC-2 and round-96 remediation record

Date: 2026-10-05. PROPOSED only; both earlier contract freezes are REJECTED.
Base dab498ff54321d802b36ce052100954bbe00487d has not moved. No source/canonical/friend/baseline/test-disposition changes.

## Dispositions

- RC-2: eager declared-type decode, application mapping and fixed-codec normalization
  happen OUTSIDE lease/turn queues. One physical evaluation per node/host, globally
  bounded by local MaxConcurrentNodes, remains occupied until return even if cancelled.
  Immutable prepared results survive unrelated version conflict/capacity deferral and
  commit under a fresh short turn after authoritative recheck. No mapper time budget,
  expiry-failure code or repeated abandoned workers. DG-P19 requires slow valid mapping
  and concurrent join/cancel evidence on real storage, not proof supplied by this target.
- P3-1: fifteen exact advanced WorkflowOperationalPressure properties and named BCL
  gauges classify internal child/unaddressed ready, addressed observers and external
  kinds disjointly, including DAG stream/checkpoint/stuck pressure. Existing operational
  snapshot/stuck-refresh APIs remain; DG-P20 verifies storage/projection/gauge parity.
- P3-2: production DB UTC sample after ownership locks and internal instance-scoped
  owning-provider test clock make virtual-time certification concrete without a public
  hook, extra friend, mock store or changed workflow time semantics.
- P3-3: one per-run boot broker FIFO across purposes; explicit OwnerBusy/LocalQueueFull,
  stable local/provider sequences and additive predecessor bounds; DG-P21 covers it.
- P3-4: OpenChildBridgeForTurn captures the explicit positive host-limit argument;
  direct admission uses the SAME local validated limit. No hidden registration source.
- P3-5: another DAG-role host's public DagRunHandle initiates cancellation. Role-free
  durable hosts process committed child cancellation/join, not an unreachable public call.
- P3-6: no persisted node Running; StartAuthorized projects as public Running. The exact
  eight-kind transition table binds StartedAt to logical admission and preserves slots.
- Recorded wording: successor is primary in all sixteen proposed doc blocks; prior
  dated decisions retain their text with a new 2026-10-05 clarification appended.

RC-1 bridge-only bounded FIFO ownership, one append, 5s lease/15s hard cap and release-
before-idle remain. Original behavior/observer-cleanup/compact-storage fixes remain.
No fifth requirement owner, public child API, new friend or codec permission is added.
All three original contract artifacts plus all four RC-1 contract/API/signature/
remediation artifacts remain unchanged and permanent source-pinned. Both rejected
request/manifest/verdict packets are immutable and cataloged. Task 1.2 stays open
but checkbox-unpinned; 1.3–3.2 and reshape 8.4/8.5 remain open.

## Second rejected freeze reconstruction

Manifest docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-dirty-manifest-2026-10-04.txt: 3456 bytes / ba7b90e912eff5fd2db3c7d2253a74d34390872a02e411543e91595919ca6e6d.
Request docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-independent-review-request-2026-10-04.md; verdict docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-independent-review-verdict-2026-10-04.md: 21654 bytes / 0ef31625a268066ba81d8f849447e15099b93b70a3080c0b8fda4dd087169415.
Rejected rehearsed tree 2302ee0879e7b913a69bd93290b0cfcea3ccb0a9; NOT a main checkpoint. Registered as
reshape-dag-run-contract-rc-2-rejected beside the first rejection, whose complete
reconstruction remains in the RC-1 remediation artifact unchanged.

Render status<TAB>path<TAB>RAW bytes<TAB>RAW SHA from these historical rows;
exclude ONLY review-manifest-provenance.json; ordinal-sort FULL rows; LF-join
with terminal LF. Exactly 42 rows / 6347 bytes /
010cf1fa0b77ff963973c659a4c515817bb6c6ba30a83413c551fbc8e5262aa7. Current frozen bytes have their OWN record; no historical
dirty-byte digest is asserted to equal an unmade checkpoint.

| Status | Path | Bytes | Raw SHA-256 |
|---|---|---:|---|
|  M | `CLAUDE.md` | 21444 | `cb56a030896ad0950a7b242eba24bd8961bf6bd3d44f50512a782a4ea8911ed9` |
|  M | `docs/durable-driver-lane-host.md` | 3950 | `4e4c424ce8b1b0a0ca32609244c5181a18d0154d65c3fb0fed8a3535eda0122b` |
|  M | `docs/implementation/00-stack-decisions.md` | 30052 | `e27ae3a15bd8bdc3162c1a7c5ef220763e3741d605508ade14dea0356879046e` |
|  M | `docs/implementation/01-solution-architecture.md` | 21553 | `aa1ddfa345bcf45cbf4fb6bf5eab2c89d3ed3c2a07e72a6eb0ac6e85d18aeeb4` |
|  M | `docs/implementation/README.md` | 12575 | `f4007314117c35c729382f2236a6207b99b78a86e759823f08d0734586a80c2f` |
|  M | `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md` | 42856 | `0badc3da11b6cb7b9e66c209a257424a33203ba8bf3665c139e7ba4a463e7584` |
|  M | `docs/project-technical-overview.md` | 11536 | `bbf1189a0fff68dda2af52ac9117e1f99110de05c075b1387f185033055eef52` |
|  M | `docs/specs/03-domain-model-and-glossary.md` | 27111 | `8156f6dc2a96098c18dd4d9928d52f920eb6f780910c090240e20710a9b5588d` |
|  M | `docs/specs/04-requirements-core-runtime.md` | 27247 | `a74d675cf28fb494f79fc91f9f3939c74ec046f73cd774e56508cf5e7f5763c0` |
|  M | `docs/specs/05-requirements-events-waits-timers.md` | 14461 | `d678a6d79e42772a6be6939d89a78b23064cd5dca24f6cc521a26e2e4409104c` |
|  M | `docs/specs/06-requirements-durable-execution.md` | 17960 | `76aef41a2fb22855e0f77937dade773ab012b964ff0331962e33d37dbfe04bb4` |
|  M | `docs/specs/08-requirements-composition.md` | 19288 | `a280a47de8873e2aac2021814f8c9db2e18b37463577179e40962417a0dc573f` |
|  M | `docs/specs/10-provider-model-and-extensibility.md` | 17716 | `b9a17106c6ea07a0ab392de476a3ef96208d402a90eed1faa61fb64a73319f3b` |
|  M | `docs/specs/12-acceptance-criteria.md` | 46847 | `f68c9f7ad75ac075bdd6e729dcd6f2d8cc158f58ef76cfb180c86c3f6770937e` |
|  M | `docs/specs/16-requirements-durable-driver.md` | 59739 | `a839b8d64e10c3f5ee0eb07faefdfdd15266735b383b57be3b21f956a6fadac4` |
|  M | `docs/specs/17-selected-mode-capability-matrix.md` | 154418 | `a3795b1189d0e3b63e1107ad3a6e76af08804ad0c27dc693679134d2299fff25` |
|  M | `openspec/changes/admit-durable-dag-run-progression/design.md` | 14490 | `d04dc39c7409f35c4856fd7130425d46cd9101849738cc8f80829c5e6192496e` |
|  M | `openspec/changes/admit-durable-dag-run-progression/proposal.md` | 4130 | `ee265ae7b18240780a356d1122b85047584804d7d5f7d0869882d743fb6f67db` |
|  M | `openspec/changes/admit-durable-dag-run-progression/specs/durable-persistence-and-outbox/spec.md` | 11329 | `cbe3a9134b8979eed7005ad4b32ab46926ae4774eaaa0c4071bc02e7b17e6bdb` |
|  M | `openspec/changes/admit-durable-dag-run-progression/specs/durable-runtime/spec.md` | 8483 | `5b5da85d8c3b7d68d21bce2989f1bcd089e656b7de8b3015b0f7703fce9bf2af` |
|  M | `openspec/changes/admit-durable-dag-run-progression/tasks.md` | 4381 | `6ea13334d25bf08d33d63b74d7c098d4d08fd6439dd584398f5f679b29e4acdb` |
|  M | `openspec/changes/harmonize-downstream-capability-specs/artifacts/task-7-3-active-documentation-reconciliation-2026-09-18.md` | 8896 | `2908ed403433cb185734b1bae208d1eebd157b2b570c9a20b8094227129735a9` |
|  M | `openspec/changes/reshape-developer-facing-interfaces/artifacts/task-8-0-section-8-requirement-gate-2026-09-26.md` | 20810 | `37639b7bca575246754adc8d4e2fedeb72c6846c619092010f988ef0033219ac` |
|  M | `openspec/changes/reshape-developer-facing-interfaces/tasks.md` | 79297 | `761f9f5bdd93e8800403305a7cc511e733cb8cab2f912afc3576a3f22eb69618` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json` | 122500 | `aade4a75cccf4481344e9a6bfdd0ed074872920cc1426ed535fb3219ebf48a7a` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json` | 15837 | `dc31f630f291b93a0bff0df68684fd6319f0c7299d3d70b337b6163a523d55da` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json` | 18151 | `bafe5023d482d406921bcc64e1b045bbe5b84037285577a59b80ca701fde3ec6` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json` | 156177 | `6833778619dd208c20619459ee2a62c96c05e7b2964ac21611670704937ec0bc` |
|  M | `tests/OrcaCore.DeveloperSurface.Guards/OpenSpecCorpusGuards.cs` | 363327 | `599375e2547dc7316c73950efeb8d08b224102668f0b6cecf691e888db049735` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-dirty-manifest-2026-10-04.txt` | 2471 | `fa3604972b2dca65a55bc753841323af9171a6438a0011be26b64ca330658a02` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-independent-review-request-2026-10-04.md` | 13827 | `842cbec6023f214e88e2fb3ad5deaa89ee82b0760ef749adca873c346a9fbff6` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-independent-review-verdict-2026-10-04.md` | 15033 | `5f2d075197e5390648d8b65fc4b2c0b3f8543f1dca06739eb4bf1fb137231229` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-dirty-manifest-2026-10-04.txt` | 3456 | `ba7b90e912eff5fd2db3c7d2253a74d34390872a02e411543e91595919ca6e6d` |
| ?? | `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-independent-review-request-2026-10-04.md` | 9224 | `7c814469c29d399094a41d739740e53e1fb41b3160811f39a8b21b553af346fb` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/contract-openspec-provenance-2026-10-04.md` | 4232 | `056ab2f1988260f2d7739e30840af1aabffffd8b27127cf308ae6d7f244b1a3a` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/contract-rc-1-remediation-openspec-provenance-2026-10-04.md` | 4293 | `82221fa6f9c6a6008906016093f3896eb0302ebee04b64b241afdd4d2574c0e6` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-2026-10-04.md` | 62837 | `61afd2af3b2e6a633bdafc113080de518195970d34e084815537784ddff7e0f8` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-exact-api-additions-rc-1-remediation-2026-10-04.md` | 71325 | `3b776f5884681a02313372589c85b237e3e053590b1081c295be669cb7ef30e0` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-rc-1-review-remediation-2026-10-04.md` | 8916 | `12e166d5e3177a530b04715af91aac36cd81ebff9aaedd98d967396c9e87d014` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-2026-10-04.md` | 43056 | `a792b9d44968612d852fe772e1b7195c4880291530895aa082453fde23729d7b` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-1-remediation-2026-10-04.md` | 58168 | `24fa34f3957bbfe8c288dc74163562d4b808763193f114e48fc2e8bc78009ac9` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-2026-10-04.cs` | 11220 | `5dab933db0b6f4aff8903e26f8f535ebd1478a6f66866e916e699cfe57277c75` |
| ?? | `openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-standalone-signatures-rc-1-remediation-2026-10-04.cs` | 13208 | `f655afc1b72e1c3d2af7d56ed23d72581d73d8b8b5c97586e4c695abb3d25918` |

The next request records fresh anchors, validation and controls. Source remains
blocked until separate contract and atomic canonical/registry review/checkpoints.
