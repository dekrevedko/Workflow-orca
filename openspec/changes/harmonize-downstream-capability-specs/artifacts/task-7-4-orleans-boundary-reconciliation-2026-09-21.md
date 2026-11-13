# Task 7.4 Orleans boundary reconciliation

**Date:** 2026-09-21
**Task:** harmonization 7.4
**Disposition:** active future boundary corrected; superseded plan retained byte-unchanged

## Active boundary

`docs/orleans-engine/README.md` is the only file under the active `docs/orleans-engine/`
directory. Its LF-normalized UTF-8 record is 3,307 bytes with SHA-256
`e4bf37e2b53b6c66d29a1028276a73237b16f0f665c353c21fb877a808270d8e`.

The note is intentionally non-authorizing. It requires a new independently approved OpenSpec change
before source, package, migration, or implementation-task work and preserves:

- one ordinary cold-capable `Wait`;
- caller-created inbound identity and direct, correlation, definition-fanout, and start-or-deliver
  routing with retained pre-wait acceptance;
- atomic workflow-event outbox persistence and application-owned
  `IWorkflowEventDispatcher` dispatch;
- fixed `orcacore-json-v1` payload and detached-state encoding;
- exact application, durable-runtime, durable-hosting, dispatcher-port, and provider ownership; and
- Orleans-specific ownership limited to future activation, transport, and lifecycle integration.

The former stale claim that delivery had only instance/correlation routes and non-buffering
`NoActiveWait` behavior is absent.

## Superseded plan

The 25 files under `docs/archive/plans/orleans-engine-pre-v1/` remain unchanged. The archive
record format is:

```text
archive-relative path<TAB>LF-normalized UTF-8 byte length<TAB>lowercase SHA-256
```

Rows are sorted by ordinal path, LF-joined, and terminated by one final LF. The record is 2,497
bytes with SHA-256
`c8a39515f7c8a8af49674e3d03f6733804b1455d2dc7f7dddee950b9b4a18da2`.

| Path | Bytes | SHA-256 |
|---|---:|---|
| `01-architecture.md` | 13,768 | `5be33d86e38d3754651fa03c8c78cf2fe656e117b9eb8104205fbb6ad24451d2` |
| `02-requirements.md` | 13,359 | `16e10110ea2e2ab9f60abdcb7cc9f3b79f2ce90a7f15a85040f0a8148fd6cc39` |
| `03-acceptance-criteria.md` | 9,062 | `fc39c718c912257018d5f2366c0c5a54f8bdaa901fd2d5185ce5338da394b9ae` |
| `04-traceability.md` | 6,409 | `a726894996d9313274e931d129da6e9c3a973d261419f15e5207ae8f0b8351c3` |
| `README.md` | 6,299 | `cbd7a9ab97fbdee9bbd45130d63dc86ff4ad2ee0a31287dc36e295473a0b56d3` |
| `plan/OT0-01-project-skeleton.md` | 2,390 | `f25586a79198f30a1d5c033de912c6bfcb4f677e1877712cfec55261e7ffb659` |
| `plan/OT0-02-testinghost-smoke.md` | 2,357 | `2c89189be5ed58bd0f39ef4ccee5ee8ed793e649e5d2ab8268b4631fb1e0f5bc` |
| `plan/OT0-03-transport-envelopes.md` | 3,884 | `350091e75e7c4d2bf079f942592b7dc229ea384f130e76742547cc7813eb6023` |
| `plan/OT0-04-silo-composition.md` | 2,856 | `5443cac01ce8e28e43e2cb05abd7cc5664bacf01a9f7de6ca43c7477b6b4457f` |
| `plan/OT1-00-durable-seams-review.md` | 3,385 | `7e0d19df2944a74327cce4bd19c96c77af18cdbe1d7c7f60576eb2cfc05e17d8` |
| `plan/OT1-01-instance-grain-start.md` | 3,622 | `1556ba1c3bdccad813780d2082a480ae08586e97ddc68a56b8bb1a1e17749399` |
| `plan/OT1-01a-public-delivery-seam.md` | 3,023 | `62e32ca24ef2ad4184dd9c4ed582314e3ec4b06242e50ffff4ffe9ae08fcbc4d` |
| `plan/OT1-02-wait-and-resume.md` | 2,536 | `9ac6cd9b59648c1409706e974fa127383700670e0db04a933934698039f83c2f` |
| `plan/OT1-03-engine-facade.md` | 4,204 | `0db86e5add6696b0234a2b02dcc9b249a8be1917fb54bb67c27cff2b2af484d5` |
| `plan/OT1-03a-atomic-start-reservation.md` | 3,433 | `d9d3f9ac11d181fc7a679817e387423f3988dc393453384e52e02063de5dbf06` |
| `plan/OT1-03b-postgres-start-reservation.md` | 2,236 | `8666b743b35d924a6d4e9f61a11e099db737297b5ab1efeb1fb2196f4c3f04b6` |
| `plan/OT1-04-event-routing.md` | 3,858 | `38016be848f57c5a652c7e684be81e455c0d16f4ab1735eb0fe79de0266c9101` |
| `plan/OT1-05-concurrency-defense.md` | 2,485 | `ae724496468d2ef24104636b12aa05cb4b902b46bcade662f4e5856e98a72e70` |
| `plan/OT1-06-deactivation-rehydration.md` | 2,617 | `408f9ef8e125e1192da189ba01bda47566541f6bd71a4508b0f7f5cf1982e80b` |
| `plan/OT2-00-expand-task-index.md` | 2,722 | `6d2524c5ea4ddc7d2481839aa8fc601a4c9b0a0e7c6968a6d25cdc335ab2e88b` |
| `plan/OT3-00-expand-task-index.md` | 2,175 | `300109a24ef198665419cca3d8f11b084cba581c09d9808501dc9d66c9463f97` |
| `plan/OT4-00-expand-task-index.md` | 1,497 | `e9fa69051e6b590a3a108722ed14d1270182dcfa4d2b27ed1ee11d54d026f4a1` |
| `plan/OT5-00-expand-task-index.md` | 2,326 | `1d1e4cc554a316b564ab8f5e3be8214ea4c88173d91f28bce8571adb5897d35b` |
| `plan/PROGRESS.md` | 172 | `44a21f364021f1a0323cee59413d98af5464790c417b73a60357e0b1be99e75d` |
| `plan/README.md` | 9,315 | `02e7f89aa77b75f4c6467543e09bf5b8e81eafaedf535389e0a3f40611ab725d` |

The active note links to the archived root README. No archived file is edited or promoted back into
implementation input.

## Review carry-forward

The same frozen target closes every non-blocking finding from the Task 7.2/7.3 approval: all
AC-trait attribute spellings are rejected in the Markdown corpus guard, PR-040's durable-hosting
member list and provider/DAG no-split rule are semantic assertions, EV-032 retains the `Active`
wait state, lineage diagnostics name the owning task, and only historical Task 5.3 may use the
retroactive owner-authorization lineage rule. The previously truncated approval-history decision
is completed and executable.
