# Normative source map

**Purpose.** Name every documentation source, state whether it is normative, and link the two
normative trees to each other. Two independent consistency failures on 2026-07-31 were both caused
by nothing structurally connecting these sources — see
[the consistency record](review/developer-facing-interface-cross-capability-consistency-record-2026-07-31.md).

**Audience.** Humans and agents. The rules an agent must follow are in
[`CLAUDE.md`](../CLAUDE.md); this file is the map they refer to.

---

## 1. The two normative trees

| Tree | Role | Shape | How it changes |
|---|---|---|---|
| [`docs/specs/`](specs/README.md) | **WHAT** — product requirements + acceptance criteria | Hand-maintained prose; requirement IDs (`CR`/`EV`/`DU`/…) and `AC-` entries | Edited directly, under a change's gate task |
| [`openspec/specs/`](../openspec/specs) | **WHAT** — capability specs in `SHALL`/scenario form | **Derived artifact** | Delta in `openspec/changes/<id>/specs/` → approval → `openspec archive` |

A third tree is binding but not a requirements source:

| Tree | Role |
|---|---|
| [`docs/implementation/`](implementation/README.md) | **HOW** — stack decisions, conventions, TDD discipline |

**Neither normative tree is authoritative alone.** `docs/review/README.md` judges code against both.
On 2026-07-31 they disagreed on four semantics and `docs/specs/` held the correct answer, so
"OpenSpec is downstream, therefore more current" is a false assumption — being downstream made the
gap invisible, not smaller.

**Precedence when they disagree:** the approved change proposal that introduced the semantics wins.
Do not silently prefer either tree; find the owning proposal or amendment and reconcile both. If no
approved change covers it, that is itself the finding.

---

## 2. Crosswalk — OpenSpec capability ↔ `docs/specs`

Verified 2026-07-31. When you change one side, check the other side's listed files.

| OpenSpec capability | `docs/specs` counterpart | Req. prefix |
|---|---|---|
| `workflow-authoring` | `04`, `08`, `17-matrix`, `17-public-authoring-contract.cs` | `CR`, `CP` |
| `workflow-contracts` | `03`, `04`, `17-public-authoring-contract.cs` | `CR` |
| `state-driven-runtime` | `04` | `CR` |
| `structured-fiber-execution` | `04`, `08` | `CR`, `CP` |
| `event-routing-and-waits` | `05` | `EV` |
| `durable-runtime` | `06`, `16` | `DU`, `DR` |
| `durable-persistence-and-outbox` | `06`, `10` | `DU`, `PR` |
| `management-and-querying` | `09`, `15` | `MG`, `OB` |
| `saga-orchestration` | `07` | `SG` |
| `runtime-resource-governance` | `04`, `08`, `11` | `CR`, `CP`, `NF` |
| `repository-foundation` | `10`, `11` | `PR`, `NF` |
| `quality-and-verification` | `12`, `11` | `AC`, `NF` |
| `developer-facing-surface` | `17-matrix`, `17-public-authoring-contract.cs`, `10` | `PR` |
| `event-driven-prototype` | *(none — historical)* | — |

### Requirement prefix → file

`CR`→`04` · `EV`→`05` · `DU`→`06` · `SG`→`07` · `CP`→`08` · `MG`→`09` · `PR`→`10` · `NF`→`11` ·
`AC`→`12` · `JS`→`14` · `OB`→`15` · `DR`→`16`

### Deferred vs removed — opposite documentation treatment

`developer-facing-surface` distinguishes two kinds of absence. Both are absent from v1 public
assemblies; they differ in what the **record** must say:

| Kind | Members | Documentation obligation |
|---|---|---|
| **Deferred** | `WhenFirst`, Saga, public `RunExternalJob`, public `RunChild`/`RunChildren`, nested `Parallel`/`While`/`ForEach`, durable lambda steps, definition-wide retry, management retry, pause/resume/archive/purge, authored `Publish`/`Cancel`, definition-targeted event fanout | **Must stay recorded** with rationale and re-entry criteria |
| **Removed** | `WaitLong`, author `Yield` | **Must disappear** — no alias, tombstone, or placeholder |

So a deferred capability is documented *as deferred*; do not erase its mention. A removed one is
erased. Getting this backwards in either direction is a defect.

**The registry lives at [`specs/13-phasing-and-open-questions.md`](specs/13-phasing-and-open-questions.md)
§13.4 "Explicitly deferred or removed capabilities"** — a table of every deferred capability and what
a future amendment must close. Note the name mismatch: `openspec/specs` calls this "the
future-capability registry" and nothing links the two terms, so searching either tree for the other's
name finds nothing. Owned by reshape task `9.6`.

### Known asymmetries

- **`OB` (observability, file `15`) has no dedicated OpenSpec capability.** It is partially covered
  by `management-and-querying` and `repository-foundation`. Either create the capability or record
  the split deliberately; today it is neither.
- **`JS` (file `14`) is a driving scenario, not a capability.** It intentionally has no counterpart.
- **`13-phasing-and-open-questions.md` and `18-semantic-appendix.md` carry no requirement IDs.**
  `18` is explicitly non-normative.
- **`17-public-authoring-contract.cs` is the compile/reflection guard baseline** (reshape proposal
  item 34), but the 2026-07-28 amendment deliberately did not amend it (its §5). Confirm that
  exclusion still holds before relying on it as a complete surface.

---

## 3. Document status

Every file under `docs/` is exactly one of these.

| Status | Meaning | Consistency obligation | Location |
|---|---|---|---|
| **NORMATIVE** | Defines required behavior | Must match the approved contract | active dir |
| **BINDING** | Defines how work is done | Must match the approved contract | active dir |
| **GUIDE** | Explains current behavior to developers | Must match; may be less complete | active dir |
| **RECORD** | Dated evidence of a review or decision | **Frozen.** Never edit to match a later contract | `review/` |
| **HISTORICAL** | Superseded; preserved for provenance | **None** | **`archive/` only** |

**Historical documentation is separated physically, not by banner.** Superseded material moves to
[`archive/`](archive/README.md) with `git mv`. A banner in an active directory is not sufficient:
an agent grepping for a removed term cannot distinguish a live requirement from a historical design
proposal, and **unlabelled history is indistinguishable from stale truth**. Physical separation makes
the distinction greppable — exclude `docs/archive/` and the noise is gone.

### Current classification

| Area | Files | Status | Notes |
|---|---|---|---|
| `specs/` | 20 | NORMATIVE | `18` is non-normative by declaration |
| `implementation/` | 10 | BINDING | Stack decisions, conventions, TDD discipline, task protocol, active refactor plan |
| `review/` | 148 | RECORD | Dated verdicts and manifests; frozen provenance |
| `observability/` | 2 | GUIDE | |
| `orleans-engine/` | 25 | PLANNED | Future variant; **not** superseded, so not archived |
| root `*.md` | 13 | GUIDE | `README`, `production-readiness`, `project-technical-overview`, this map, engine guides |
| `archive/` | 171 | HISTORICAL | `implementation-phases/` 109, `architecture/` 24, `requirements/` 13, `plans/` 8, `research/` 7, `durable/` 5, `reviews/` 1 |

Archived on 2026-07-31, all with `git mv` so `git log --follow` still works:

| Moved | Why |
|---|---|
| `architecture/` → `archive/architecture/` | Its own README declared the directory "not the first-release approval baseline"; `specs/README.md` provenance names 12 of its files as superseded; the rest are March–April 2026 and cite removed types |
| `requirements/` → `archive/requirements/` | Self-declared "Historical Requirements Tree" |
| `plans/` → `archive/plans/` | README declared the directory historical |
| `research/` → `archive/research/` | Prior-art studies, 2026-03 |
| `reviews/` → `archive/reviews/` | One file from 2026-03-17; name collided with `review/` |
| `durable/` → `archive/durable/` | All five files "Saved on 2026-03-16" |
| `implementation/phases/` → `archive/implementation-phases/` | 109 files its own README declares historical; includes task docs for removed/deferred `WaitLong`, `RunExternalJob`, and Saga. The OpenSpec task graphs are the active checklists. |

Still to verify: `durable-driver-audit.md` and `durable-driver-status.md` at the docs root are dated
status documents (2026-07-13/14) predating Sections 4–7; confirm they are current or archive them.

---

## 4. Where the gates failed

Both 2026-07-31 failures were the same defect: **a synchronization step that runs once, at a fixed
point, and silently ignores anything approved later or not named in its own inputs.**

| Gate | Scope | Missed |
|---|---|---|
| `10.14` canonical sync | "every approved **delta**" | 3 capabilities that had no delta |
| `x.0` amendment gates | applied **before** each section starts | an amendment approved after every gate closed |

The structural fix is in `CLAUDE.md`: a sync gate must enumerate its **targets** (every capability,
every mapped file in §2), never only its own inputs.
