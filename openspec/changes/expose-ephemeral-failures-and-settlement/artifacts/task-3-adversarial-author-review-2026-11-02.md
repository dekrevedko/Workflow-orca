# Issue 16 task 3 adversarial author review — 2026-11-02

Verdict: **HOLD for qualification evidence**. No remaining local P1/P2 product
finding was identified. This is a fresh adversarial pass by the implementing
Codex session, not an external independent approval. No source checkpoint,
publication, push or issue change is authorized by this report.

Reviewed against the approved contract and canonical transition, the exact two
public members, FS-01–20, numbered ownership/shutdown rules, old native boundaries,
both failure codecs, durable stored records, current tests, package consumers,
baselines and historical provenance guards. The real canonical chain is
ecd188a (tree ae97c26e) → verdict-only child 5960f0b. All old approval and
provenance artifacts remain unchanged; source approval stays null.

## Blocking evidence gap

Task 3.4 requires actual successful Windows Server 2025 and Ubuntu 24.04 jobs for
JIT, full trimming and Native AOT, plus their aggregate and exact package/source
identities. None has been run for this candidate. The owner explicitly prohibited
pushes; the earlier issue 13 temporary-branch authorization is not reused.
The 84-case Windows 11 JIT preflight is useful package evidence but deliberately
fails native qualification. Old issue 13 native results cannot qualify new source.
Keep all task 3 boxes open and the candidate uncommitted until this gate and
independent/owner review are complete.

## Findings fixed during the author pass

1. **Cancellation compatibility:** bypassing the legacy invocation wrapper changed
   unrelated branch/item OperationCanceledException into an unaccepted start.
   Four behavior assertions reproduced it. The typed business boundary now captures
   that exception while retaining governed cancellation. Root abort behavior has
   its own unchanged-classification controls.
2. **Owner-graph retention:** handle delegates captured the full runtime binding
   and registry after retirement/disposal. A weak-marker assertion reproduced it.
   Handles now retain identity, a passive task and a shared authority carrier;
   the carrier releases the strong registry at retirement or DI disposal. Active
   handles retain real authority. Four weak tests and token-first controls pass.
3. **Terminal election race:** selecting the original before committing Failed
   allowed a rejected late terminal attempt to clear the elected reference.
   Selection, error and terminal fields now change under the same snapshot gate;
   recovery-map operations share that gate. Cancellation, cleanup and user code
   stay outside it. Existing terminal-winner, timeout and late-work controls pass.
   This finding was code-proven; no deterministic red race reproduction is claimed.
4. **Shutdown stress assertion:** the old zero-budget test could observe Completed
   after a cooperative body legitimately exited. One of twenty loaded runs caught
   it. An explicit physical cancellation-cleanup gate preserves all old assertions
   and adds a pending-body check. The final 405-test suite passes twenty of twenty
   paired dotnet test runs.

The first mutation pass exposed an inadequate recovery weak test: it checked only
after terminal cleanup. The assertion now runs while Waiting, and the relevant
mutation is killed. Twenty-two focused semantic mutations are killed with exact
source restoration and no survivor, timeout, compile error, uncovered selection
or harness error in the final campaign. This is scoped manual mutation coverage,
not a whole-repository Stryker score. Earlier invalid attempts remain in evidence.

## Consistency and qualification limits

- Live originals exist only in the ephemeral runtime association; public values,
  Core records and provider records contain only detached text. ExceptionType is
  not resolved into Type and is not business identity.
- Null-type failure bytes remain legacy. Typed public failures use the exact
  version-2 shape; mixed graphs and strict malformed readers are tested. The
  engine-produced durable stored failure bytes remain byte-equal to the base.
- Receipt completion is an identity-bound terminal/zero-owner seal. It excludes
  query pins, retirement cleanup success and arbitrary unjoined application work.
  Cancellation affects the observer; retirement and owner disposal do not require
  lookup, DI, polling or a host lease on the completed path.
- Only the approved getter and inherited nonvirtual method enter the public API.
  No public constructor, option, serializer hook, friend edge or package dependency
  was added. Historical guards retain exact previous layers after verifying the
  current source authorization, rather than broadly exempting source.
- Canonical/numbered text and old release claims are unchanged. Pending guide
  wording is retained because this source/native target has no approval or release.
  The new protocol defaults fail closed; historical protocols require explicit
  historical validation and cannot qualify this source.
- ReSharper has zero errors; retained warnings and setup failures are explained
  in the adjacent triage and raw reports. Local provider execution uses containers;
  it is not a native Ubuntu or hosted CI claim.
