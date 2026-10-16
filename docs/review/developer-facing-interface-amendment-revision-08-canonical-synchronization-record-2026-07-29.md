# Revision 8 canonical synchronization record

**Date:** 2026-07-29  
**Scope:** post-approval canonical synchronization under task `10.14`  
**Implementation scope:** none

## 1. Approval consumed

Task `4.15` consumed the immutable independent `APPROVE` verdict:

- path:
  `docs/review/developer-facing-interface-amendment-revision-08-task-4-15-independent-review-verdict-2026-07-28.md`
- SHA-256:
  `B149F7E33AEE3F8C32BEC55994486A625972D78A2130B44E83AB6AEBB682F2CE`

The approval authorized canonical synchronization only. No task `4.16` product implementation was
performed by this synchronization.

## 2. Exact OpenSpec synchronization

The approved operation set contains 137 operations:

| Change | ADDED | MODIFIED | REMOVED | Total |
|---|---:|---:|---:|---:|
| `reshape-developer-facing-interfaces` | 80 | 44 | 6 | 130 |
| `add-runtime-concurrency-limits` coordination | 2 | 5 | 0 | 7 |
| **Total** | **82** | **49** | **6** | **137** |

Before synchronization, the reshape operation disposition was:

- ADDED: 31 already exact, 5 present with older content, 44 absent;
- MODIFIED: 23 already exact, 21 present with older content;
- REMOVED: 1 already absent through accepted task `4.9`, 5 present.

All seven coordinated runtime-governance operations were present with older content. After
synchronization:

- all 82 ADDED requirements are present and exact;
- all 49 MODIFIED requirements are present and exact;
- all 6 REMOVED requirements are absent;
- unresolved or duplicate synchronized operations: 0.

Canonical files and post-sync SHA-256 values:

| Canonical artifact | Lines | SHA-256 |
|---|---:|---|
| `openspec/specs/developer-facing-surface/spec.md` | 194 | `337831E7797662EDF9B75A3A333B37135A93ACD00D4268526217420DA491C075` |
| `openspec/specs/durable-runtime/spec.md` | 364 | `AF7E569E2C0FECE953F429A4C6ACE0311A2168E39FB5A94AE8489188A485EC4C` |
| `openspec/specs/management-and-querying/spec.md` | 261 | `C577E4CCA3540C6BFFA42076DC8D2C02A48184EDEFBA77DB3E4FD1DDA67CE593` |
| `openspec/specs/quality-and-verification/spec.md` | 362 | `8B1CC2543ED84BEE50A34B358AC79C1C9E1426E855F579854DFB8B58DEE9D568` |
| `openspec/specs/repository-foundation/spec.md` | 111 | `E2D51C8ED6107A3D29748084C7F06ED25657E13518D4EFA02FA6E66092E11B46` |
| `openspec/specs/runtime-resource-governance/spec.md` | 256 | `044C61BE5EDA221F441F089B66AF67D07FEEB097B82A2AA2599F79B133B31AEF` |
| `openspec/specs/saga-orchestration/spec.md` | 15 | `0195E36E9DF9D974EF9B3692ADEE3F4C056CC946B6E69A301951BE636C5ECB90` |
| `openspec/specs/state-driven-runtime/spec.md` | 88 | `410CF7AE231E42FC999D7A20792EE137BF4A2AD17D0882B0D8FBB73BEAB28E61` |
| `openspec/specs/structured-fiber-execution/spec.md` | 160 | `EA31E1B005734B0FC109EAB7C673B19E95C8EAAA826B10D623A60C22CF410BC4` |
| `openspec/specs/workflow-authoring/spec.md` | 313 | `41B840B010C8DE4176182842DA7696FF434AEF9033F2DB58BB6796195E764B61` |
| `openspec/specs/workflow-contracts/spec.md` | 294 | `401F6C6AFC9EB25C6F27E593B10F7ABF15184AE8EA07D7CC29FD3F0AF23A1F1A` |

## 3. Publication synchronization

Task `9.10` added the approved bulk-synchronous fork-join description, tagged-item flattening and
sequential-staging preconditions, future-capability scoring preamble, and nested-fan-out re-entry
bar to the selected-mode matrix. The existing v1-corrections block in the ephemeral guide now
records the same root-only encoding and phase/scope lifecycle constraints.

Task `9.12` published the non-normative semantic appendix. L4 remains reserved until task `4.16`;
the appendix contains 15 exact canonical requirement citations and all 15 deliberately excluded
claims.

| Published artifact | Lines | SHA-256 |
|---|---:|---|
| `docs/specs/17-selected-mode-capability-matrix.md` | 2537 | `65D9F746BAA408B560E3A556CCE136AE1DBA235CF68DC4E257392F34FBD2B8D1` |
| `docs/ephemeral-engine-developer-guide.md` | 925 | `BECC8CDCAE1265F694B8A763038548008E1E360F0E531ED6313980C1DE1775E0` |
| `docs/specs/18-semantic-appendix.md` | 168 | `5B2FE27123F29AA6B2B20C5AB3981AB2F96BD99B2C86CDECE92781A587F17CD3` |

`docs/specs/17-public-authoring-contract.cs` remains unchanged.

## 4. Task and planning state

Completed by this approval/synchronization sequence:

- `4.15`
- `5.10`
- `9.10`
- `9.12`
- `10.14`

Task accounting is 66 complete / 70 pending / 136 total with zero duplicate IDs. Tasks
`4.16`–`4.21`, `5.11`, and `5.13`–`5.15` are the next remediation source slice. Task `6.0`
remains open and blocked.

The reviewer’s informational terminology finding is reconciled: the task list now describes
`MaxActiveFibers` and unauthorized fingerprint contributors as current canonical-to-source
`ExpectedRed` gaps after synchronization.

## 5. Validation evidence

| Command/check | Result |
|---|---|
| `openspec.cmd validate --all --strict --no-interactive` | 17 passed / 0 failed |
| UTF-8 exact operation resolver over both approved changes | 82 ADDED exact; 49 MODIFIED exact; 6 REMOVED absent; 0 unresolved |
| Canonical encoding-marker scan | 0 suspicious replacement/double-decoding markers |
| Published semantic-appendix local-link resolver | 15 checked / 0 missing |
| Published semantic exact-heading resolver | 15 resolved / 0 unresolved |
| Published semantic exclusions | 15 |
| Task checkbox and ID parser | 66 complete / 70 pending / 136 total; 0 duplicate IDs |
| `git diff --exit-code -- docs/specs/17-public-authoring-contract.cs` | exit 0 |
| `git diff --check` over synchronized and bookkeeping artifacts | exit 0 |

The sorted-status hash pipeline used for this record is:

```powershell
$entries = @(git status --porcelain=v1 --untracked-files=all | Sort-Object)
$normalized = ($entries -join "`n") + "`n"
$bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($normalized)
$hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
```

Final self-inclusive status entries: `397`  
Final LF-normalized sorted-status SHA-256:
`9B220AE161387FC236DEF785CD1267466A612E9DEAC209BAC0ECD3A204218114`

## 6. Historical preservation

| Immutable artifact | SHA-256 |
|---|---|
| Section 5 dirty manifest | `F001016F92CF056AA1CF6E99203353112504500C1C5E582625AE3B607A41BBAD` |
| Section 5 review request | `53FCB5818FAE78F725B1676A2EBAF6FAB00C1595AC83FC7BD1BCF3DCE17BFA2E` |
| Revision 4 approval verdict | `055D4CA879EC7B431F7D92F14074DCD7BA2E61CBD66DD2F38028D188E79B758C` |
| Revision 6 rejection verdict | `C8420E8819377F3037BF303680EA60B57F42DC2375D8F88DEA4F0DDDA9BDD026` |

All historical requests, manifests, and verdicts remain unchanged. Existing dirty product-source,
test, sample, and benchmark work was preserved; this synchronization added no product
implementation change.
