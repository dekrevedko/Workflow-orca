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
| [`openspec/specs/`](../openspec/specs) | **WHAT** — capability specs in `SHALL`/scenario form | **Derived artifact** | Delta in `openspec/changes/<id>/specs/` → approval → canonical sync; archive when the whole change is complete |

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

Verified 2026-08-01. The table names each capability's **primary owner**, not an exhaustive
occurrence allowlist. When changing either side, check the listed owners and then run a reverse
occurrence sweep across every active numbered spec and matrix/compile-shaped contract. Every hit
must be classified as an owning rule, a cross-capability dependency, or an acceptance reference;
an unclassified hit blocks synchronization.

| OpenSpec capability | Primary `docs/specs` owner | Req. prefix |
|---|---|---|
| `workflow-authoring` | `04`, `08`, `17-matrix`, `17-public-authoring-contract.cs` | `CR`, `CP` |
| `workflow-contracts` | `03`, `04`, `17-public-authoring-contract.cs` | `CR` |
| `state-driven-runtime` | `04` | `CR` |
| `structured-fiber-execution` | `04`, `08` | `CR`, `CP` |
| `event-routing-and-waits` | `05`; current cross-capability occurrences also exist in `01`, `03`, `06`, `09`, `10`, `12`, `13`, `14`, `16`, and `17-matrix` | `EV` |
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
| **Deferred** | `WhenFirst`, Saga, public `RunExternalJob`, public `RunChild`/`RunChildren`, nested `Parallel`/`While`/`ForEach`, durable lambda steps, definition-wide retry, management retry, pause/resume/archive/purge, authored `Cancel` | **Must stay recorded** with rationale and re-entry criteria |
| **Removed** | `WaitLong`, author `Yield` | **Must disappear** — no alias, tombstone, or placeholder |

So a deferred capability is documented *as deferred*; do not erase its mention. A removed one is
erased. Getting this backwards in either direction is a defect. Durable workflow-authored
`Publish` and definition-targeted event fanout are current Section 7B capabilities and therefore
do not belong in this deferred registry.

**The future-capability registry lives at
[`specs/13-phasing-and-open-questions.md`](specs/13-phasing-and-open-questions.md)
§13.4 "Future-capability registry".** Its deferred-capability table records every future promise and
the questions a reviewed re-entry amendment must close; its separate removed-concepts subsection
keeps retired names searchable without presenting them as future work. Canonical OpenSpec and its
active owning deltas cite this exact path and section name. Harmonization task `6.6` owns that shared
name and cross-reference; reshape task `9.6` owns the registry's final membership.

### Known asymmetries

- **`OB` (observability, file `15`) has no dedicated OpenSpec capability.** It is partially covered
  by `management-and-querying` and `repository-foundation`. Either create the capability or record
  the split deliberately; today it is neither.
- **`JS` (file `14`) is a driving scenario, not a capability.** It intentionally has no counterpart.
- **`13-phasing-and-open-questions.md` and `18-semantic-appendix.md` carry no requirement IDs.**
  `18` is explicitly non-normative.
- **`17-public-authoring-contract.cs` is the compile/reflection guard baseline** (reshape proposal
  item 34). The 2026-07-28 amendment did not amend it, but the later approved Section 7B change did;
  the current companion and exact API baselines include the shipped durable ingress and publish
  surface.

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
| `implementation/` | 9 | BINDING | Stack decisions, conventions, TDD discipline, task protocol, active refactor plan |
| `review/` | 147 | RECORD | Dated verdicts and manifests; frozen provenance |
| `observability/` | 2 | GUIDE | |
| `orleans-engine/` | 1 | PLANNED | Clean future-hosting boundary; implementation requires a new approved change |
| root `*.md` | 11 | GUIDE | `README`, `production-readiness`, `project-technical-overview`, this map, current engine guides |
| `archive/` | 199 | HISTORICAL | Root `README` 1, `implementation-phases/` 109, `architecture/` 24, `requirements/` 13, `plans/` 39, `research/` 7, `durable/` 5, `reviews/` 1 |

The directory migrations below were archived on 2026-07-31 with `git mv` so
`git log --follow` still works. On 2026-08-01 the historical Phase-0 kickoff, pre-v1 ephemeral
guide/diagrams, dated durable driver status/audit, superseded end-to-end plan, and the superseded
25-file Orleans plan were also moved into `archive/plans/` while current ephemeral guides, a
gate-oriented end-to-end plan, and a clean Orleans boundary note were authored at their active
paths. The archive's own `README.md` is an index created in place, not a moved historical file.

| Moved | Why |
|---|---|
| `architecture/` → `archive/architecture/` | Its own README declared the directory "not the first-release approval baseline"; `specs/README.md` provenance names 12 of its files as superseded; the rest are March–April 2026 and cite removed types |
| `requirements/` → `archive/requirements/` | Self-declared "Historical Requirements Tree" |
| `plans/` → `archive/plans/` | README declared the directory historical |
| `research/` → `archive/research/` | Prior-art studies, 2026-03 |
| `reviews/` → `archive/reviews/` | One file from 2026-03-17; name collided with `review/` |
| `durable/` → `archive/durable/` | All five files "Saved on 2026-03-16" |
| `implementation/phases/` → `archive/implementation-phases/` | 109 files its own README declares historical; includes task docs for removed/deferred `WaitLong`, `RunExternalJob`, and Saga. The OpenSpec task graphs are the active checklists. |

The dated durable driver audit/status, superseded pre-v1 ephemeral guide/diagrams, and superseded
Orleans task plan now live under `archive/plans/`. Their active-path replacements, where present,
describe only the selected v1 contract.

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
