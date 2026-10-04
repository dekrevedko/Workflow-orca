# Independent review — DAG runtime-view source Tasks 2.1–2.4

Date: 2026-10-04
Change: `admit-dag-hosting-runtime-view`
Base activation: `13fe5b996e4758e383ea6ac68d86bdaf10b940b8`
Manifest: [developer-facing-interface-section-08-task-8-3-runtime-view-source-dirty-manifest-2026-10-04.txt](developer-facing-interface-section-08-task-8-3-runtime-view-source-dirty-manifest-2026-10-04.txt)

## Exact authority and prior chain

Review this one uncommitted source target. Atomic canonical checkpoint
`a0da21ba9597e3864a3d4134120fbb0138417bd7` has the independently approved
tree `572d86f6801e36a0611227386939e4dcbb1ab885` and parent
`64d97c644475f6dfbe183540bf546c7ab48eab46`. Its only-child evidence commit
`6c8bcd02bf6c747800c17dbc27afce53d24efbc1` adds the immutable APPROVE:
9,749 B / `cce8c5195f559f1624a002ef4870c32ccdb445163aec9f552dcbfc5245de7e35`.
The base above checks Task 1.4 only. Evidence Infrastructure and activation
Infrastructure both passed 240/240; no status documentation was edited in activation.

Tasks 2.1–2.3 are implemented candidates. Task 2.4 stays open until independent
source review/checkpoint and is deliberately unpinned for checkbox-only activation.
Tasks 3.1/3.2 stay open and pinned, and the registry stays ApprovedPending.
APPROVE authorizes exactly this source checkpoint, not Complete promotion, archival,
codec/child-start access, reshape 8.4/8.5, or executable DAG runtime completion.
No forward bridge implementation starts while this source review is pending.

## Self-inclusive freeze and reproducible records

The manifest includes itself, this request, both admission/freeze fixtures,
the source-validation artifact, retained SARIF and every semantic file.
Raw git order is `git status --porcelain=v1 --untracked-files=all --no-renames`.
Tracked paths must also occur in `git diff --name-only HEAD`; include every
untracked file. Reject content-identical status phantoms. The main index is empty.

- paths: 33 (25 modified, 8 new)
- manifest bytes: 2344
- manifest SHA-256: `6d92fc3d518644f6c3f767302a027c09767a28064e442402ffae7a213e5583e0`
- semantic rows: 29
- semantic bytes: 3909
- semantic SHA-256: `1c7afb55ac2faab32c9eceb801dc8aa84bf03aca19f39b65a15bfaef923179c1`

Semantic recipe: omit all `docs/review/` paths and exactly
`tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json`
and `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json`.
Render each other path's raw bytes as
`path<TAB>raw byte length<TAB>lowercase SHA-256`, ordinal-sort complete rows,
LF-join with one terminal LF, UTF-8 without BOM. Do not normalize file bytes.
This ordinal recipe is intentionally not the old source request's raw-order recipe.

The active record includes all manifest paths except the exact self-referential
review-manifest fixture. Render `XY<TAB>path<TAB>raw byte length<TAB>lowercase SHA-256`,
ordinal-sort, LF-join with one final LF, hash UTF-8. Its exact size/hash is pinned
after the request and catalog are final. The active anchor and staged disposable
tree are in the handoff, outside these self-inclusive bytes, avoiding circular hashes.
Compare every staged blob to raw working bytes. Do not stage/commit main during review.

## Claims to challenge

1. The ninth friend is exactly `OrcaCore.Dag -> OrcaCore.Dag.Hosting`. No new
   test grant, package reference, public API, public implementation metadata,
   OrcaCore authoring allowlist widening or Core/codec runtime access is present.
   Canonical/delta specs and all historical packet bytes are unchanged.
2. Production Hosting consumes exactly the three reviewed internal type families
   and fifteen decoded signatures. Inspect TypeRefs independently of MemberRefs:
   field/method signatures, generic arguments, custom attributes, inheritance,
   interfaces, typeof and is references must not bypass the policy. Constructors,
   fields and extra overloads are rejected. Permanent compiled probes individually
   exercise eight forbidden types and four forbidden members; public-only is green.
   Removing either type scanning or member policy must turn its self-test red.
3. Typed mapper wrappers and copied plan descriptors preserve public authoring
   behavior/fingerprints and never invoke a mapper during Build. Exact reference
   identity, direct-dependency restrictions, eager declared-output validation,
   read-only snapshots and caller-map isolation precede mapper invocation.
   Present reference/Nullable<T> null is valid; missing output, wrong declared
   type and non-nullable null are invalid. Ordinary mapper exceptions, including
   mapper-thrown OCE, become DAG_INPUT_MAPPING_INVALID. Integrity exceptions are
   not normalized. The source-pinned real-adapter harness executes exactly 34
   behavior assertions, with no test friend or reflection bridge. It supplies
   already-decoded detached values: it does not test codec/materialization, input
   fingerprint/commit, child start/join or registry/coordinator wiring.
4. Both latest non-blocking notes are fixed in this reviewed source target:
   current doc status names approved atomic checkpoint a0da21ba and the compiled
   candidate awaiting independent source approval; dated Decision 22 entries are
   untouched with a new 2026-10-04 entry appended. Schema 8's renamed historical
   contract catalog distinguishes rejected/superseded from approved/governing.
   Both entries and files stay byte-exact, permanently source-pinned. All 22
   Task 7.3 rows/digest, numbered status pins and Task 8.0 map hash are refreshed.
5. Provenance is unchanged at 180 rows / 47,347 B /
   `40d4d8c0b9144ea087d7b36d33df35d4736942ae615f50126f7ba136b20da8c1`:
   173 synchronized, four superseded predecessors, three bootstrap-only, zero
   pending. Three new guard Facts explain crosswalk +1 file/+3 declarations:
   physical 340/1,405, active 192/717. Only Dag and Dag.Hosting package-source
   hashes change; public baseline files do not. The actual atomic approval,
   single-parent added verdict and exact tree are checked before source tasks.

## Verification and isolated controls

The source-validation artifact documents scope, mapping ownership, the exact
assertion catalog, prior chain, both note closures and inspection triage, and is
independently hash-pinned in guard source. No future bridge claim is implied.

Debug/Release non-incremental warn-as-error builds: 0 warnings/errors. Core 350,
Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, ProviderCertification 96;
real PostgreSQL 101, SQL Server 72, Integration 11. Infrastructure 243/243,
fourteen documented ExpectedRed failures separately. Focused metadata/behavior
3/3 in both configurations; strict OpenSpec 20/20. Twelve fresh packages;
eight green consumers, dag-hosting the one existing expected red. Green compile
fixtures and public baseline/consumer guards pass. Final frozen and committed
rehearsal results are provided in the handoff.

JetBrains InspectCode 2026.2.3.1, scoped Dag/Dag.Hosting/new guard, Release/net10.0:
59 findings, 41 warnings, 18 notes, zero errors. The retained SARIF and artifact
give the exact categories and triage; do not confuse these with compiler warnings.
An unused retained field was removed; no automatic cleanup or broad refactor ran.

| Control | Isolated mutation | Result |
|---|---|---|
| S1 | Suppress internal TypeRef collection with compiling predicate | RED, emitted sentinel absent |
| S2 | Neutralize exact member policy | RED, forbidden-member self-test |
| S3 | Let mapper OCE escape | RED, linked adapter behavior |
| S4 | Reject present reference/nullable null | RED, exact null case |
| S5 | Hosting typeof of internal DagValueTypes | RED, actual production TypeRef set |
| S6 | Hosting reads DagNodeRef.PlanToken | RED, actual production member set |

All controls ran in a verified disposable worktree and restored source bytes.
S1's first constant-false mutation was a CS0162 compile failure, not evidence;
the corrected compiling predicate hit the intended assertion. One overlapping
main build hit DLL locks while an Infrastructure run was active; sequential
non-incremental rebuilds then passed. Neither issue changed product/frozen bytes.
Permanent probes copy and compare the repo SDK pin before creating projects.

## Reviewer and checkpoint sequence

Independently verify actual compiled references, mapping behavior, null/exception
policy, unchanged canonical/history/public surface, all refreshed records and
approval-chain authority. ReSharper InspectCode is available for your source review.
Write a new immutable verdict without editing this frozen target. The verdict is
an extra path for the later evidence commit, not a manifest path for this target.

On APPROVE only: checkpoint exactly the manifest, then make its only-child
evidence commit adding/cataloguing the byte-exact verdict and clearing the freeze;
activation checks Task 2.4 only. Tasks 3.1/3.2/reshape 8.4–8.5 stay separately gated.
No checkpoint is authorized by the author's green validation or rehearsal alone.
