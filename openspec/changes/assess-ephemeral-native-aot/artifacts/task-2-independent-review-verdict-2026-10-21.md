# Issue #3 task 2 native assessment — independent review verdict

**Date:** 2026-10-21
**Target:** `codex/issue-3-ephemeral-aot`, base `dbd829e`, 64 entries (24 modified, 40 added), tree
`98d9668adbfec19dd022f61babddd240d7f1cbe8`, porcelain SHA-256 `fbb906a3…`. The tree reproduced
through a private index, and nothing drifted during review.

## Verdict: APPROVE

I approve the assessment and its interpretation.

| Matrix cell | Result |
|---|---|
| JIT, both hosts | `AssessedCompatible` (16/16) |
| Trimmed JIT, Native AOT, SDK defaults | `AssessedUnsupported` |
| Trimmed JIT, Native AOT, JSON reflection explicitly enabled | `AssessedUnsupported` |

No support claim is made beyond the JIT cells. There are no P1 or P2 findings.

**Authorized now:**
1. Stage exactly the 64 manifest paths, excluding this verdict. Confirm tree `98d9668a…` with parent
   `dbd829e`, then commit.
2. Add a verdict-only child commit.
3. Make **one** publication commit, without another independent review. It contains only:
   - the approval binding: approval paths set to this verdict and its commits; 2.4 ticked;
   - the guide and README wording changed from "pending independent review" to the approved result;
   - P3-1 and the PR text.

   Record OpenSpec strict and Infrastructure results in the commit message. Anything beyond this,
   especially product source, returns for review.
4. Push and open the PR to master.
   - CI must be green.
   - Merge it with a merge commit.
   - Merging and closing issue #3 remain the owner's decision. The assessment may legitimately close
     the issue with "unsupported", as the issue itself allows.

## What I verified

**Chain.**
- `eea51c9` has parent `7895262` and the approved tree `16c371ba…`.
- `dbd829e` adds only the contract verdict.

**Native evidence.**
- Workflow run [39721105482](https://github.com/dekrevedko/Workflow-orca/actions/runs/39721105482)
  completed successfully on the pushed temporary branch at `b92c5cc`. Its jobs were one candidate feed,
  native `windows-2025`, native `ubuntu-24.04`, and a same-source/package consistency job.
- `b92c5cc` descends from `dbd829e`.
- The candidate tree differs from it only in evidence, docs and planning files. The harness, consumer
  and workflow bytes are identical.
- No `src/`, `Directory.Packages.props` or `global.json` change exists anywhere in the target.

**Verifier.** I ran the author's `task-2-verify-evidence-2026-10-21.py` myself, and it passed:
- both native records, the harness and all 863 raw archive entries match;
- all 680 discovery warning rows and 300 runtime findings keep their exact identities and individual
  dispositions.

**F1 handling.** This is exactly the recommended option.
- **Default trimmed/AOT cells** record the true first failure. Registration rejects the definition
  with `WF-DEFINITION-INVALID` / `SFE-TYPE-002`, because `DefinitionCompilerOptions.cs:58` turns
  missing default JSON metadata into a schema rejection. The record keeps this as observed rather
  than relabelling it.
- **The switch-enabled extra cells** advance and expose the next real blockers:
  - in trimmed builds, `FacadeValueFactory.Construct` / `PayloadFingerprint`'s nonpublic-constructor
    lookup;
  - in AOT builds, `RegisterCore`'s runtime property lookup and ValueState/`Int32[]` metadata.

  The switch is recorded as a LocalRequirement candidate that is insufficient, so the cells stay
  `AssessedUnsupported` rather than conditional.
- The site is now in the static inventory (90 risks).

**Accounting and measurements.**
- There are 268 warning identities / 316 occurrences plus 120 runtime findings, each with its own
  disposition.
- JIT is measured with 3 warm-ups plus n=20 per host. The OS-specific memory metrics are named
  separately, the `GC.GetTotalMemory` value is labelled an estimate, and no cross-OS ranking is
  made.
- Unsupported cells report their measurements as unavailable, with the cause.

**Canonical and guards.**
- All four blocks appear exactly once in `openspec/specs/quality-and-verification/spec.md` and are
  byte-equal to the delta. The sync is append-only, with zero removed lines.
- The DAG guard and the RepositoryGuard AC-555–562 rows are exact compositions or non-credit
  mappings, and the other edits are mechanical pins.
- NF-033 and AC-555–562 are present in the numbered docs.

**Lanes.** I did not re-run them. The author's full lanes (OpenSpec 24/24, Release 0/0,
Infrastructure 259/259, units 969/969, coverage 79.10%) are on this exact tree. The target's only
executable changes are guard pins and the off-CI assessment workflow. The PR's CI re-runs them.

## P3 (optional)

- **P3-1 — Workflow trigger.** `.github/workflows/ephemeral-native-assessment.yml` still has a
  `push` trigger on the temporary `codex/issue-3-native-evidence` branch. Keep `workflow_dispatch` and
  drop that trigger in the publication commit. Once the PR merges, the owner may delete the remote
  temporary branches: `codex/issue-3-native-evidence`, `codex/issue-2-ephemeral-shutdown` and
  `issue/1-ephemeral-retention`.
- **P3-2 — Product follow-ups (owner decision, separate issues).**
  - The registration path reports disabled reflection JSON as a generic schema error; it should
    diagnose the cause.
  - Any AOT/trimming remediation needs its own amendment: a codec type-info strategy, and removal of
    the nonpublic-constructor and runtime property lookups.
