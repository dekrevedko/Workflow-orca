# Issue #13 — whole AOT change review, 2026-10-28

**Verdict: HOLD the whole change for the two confirmed P2 findings.** No additional
confirmed regression was found outside those findings in this expanded review.
This is a separate adversarial self-review, not an external reviewer approval.
It does not authorize fixes, commits, pushes, PR publication, merging or release.

## Scope: the complete AOT change, not just publication preparation

The implementation was reviewed from pre-AOT master
`3b7f98aa0748a4d9c3f54d4604e566ed61f21bad` through
`8566986850dc377f5a69e165dd58936be50ad722`. That range changes **348 paths**:
42 product source files, 26 tests/guards/baselines, 8 native-harness/workflow paths,
16 documentation paths and 256 OpenSpec/planning/evidence paths. The separate
30-path publication target over HEAD `1ad2a5946b2874fd709571376a67799f5c7f9cd1`
was included too. Product-file diffs and relevant callers were read individually;
large evidence sets were checked through their manifests, validators, hashes and
scenario records rather than described as line-by-line source review.

This review includes shared Abstractions/Core behavior and its JIT users in Durable,
DAG and hosting. It grants no new native support to those engines. It reviews the
actual AOT source and harness, not only the two previously reported codec defects.

The frozen publication tree remains
`3596bfa07556e6da81f64633d0e65bf3734d1c61`, with content SHA256
`6ba5c0ba2576471039e72991f3698764b8f5ad189757d62375f079f8f46014ae`.
No original source, requirement, document, baseline or frozen artifact was edited.
This report and the preceding extended-review verdict are excluded review files.

## Findings retained against the whole change

**F1 — P2, Generated-mode compatibility/contract gap.**
`src/OrcaCore.Abstractions/Serialization/FixedWorkflowValueCodec.cs:104` includes
setter-only descriptors that the legacy serialized-member walk omits. The two
write-only-member DTO controls work on pre-AOT master/current legacy mode but fail
Build/TryBuild in Generated mode with SFE-TYPE-002. The approved contract describes
the omittable synthetic initializer rule as the sole additional admission exception;
this extra rejection is unapproved. Resolve parity or obtain an explicit contract
amendment, with positive regression coverage. It is not an unconfigured-JIT break.

**F2 — P2, existing bug carried into native qualification.**
The same codec's runtime walk at lines 366–369 skips included fields and properties
with both accessors internal. Accepted workflows lose unapproved derived members
(Extra=99 becomes a base Item without Extra); a custom collection in an included
field also bypasses the runtime allowlist. Ordinary public-property controls reject
both hazards. The old master reproduces it, so it is not a new JIT regression, but
AC-570/NS-08 and the stated fixed-codec rejection contract are not satisfied.
Fix/reconcile the boundary without breaking existing getter-count semantics and
extend the native negatives before claiming full conformance.

Exact public workflow repros, old-master classification and raw observations are
in [the earlier detailed verdict](task-3-1-extended-review-verdict-2026-10-28.md)
and its retained local receipts. That verdict's nonblocking P3 also remains:
`docs/specs/12-acceptance-criteria.md:778` still calls the now-qualified assertions
“mandatory future” assertions. No further confirmed P1/P2 finding emerged here.

## What was checked across the implementation

| Area | Review and outcome |
|---|---|
| Public API and package boundary | WorkflowJsonMetadata/Register, existing converter accessibility and throttle Interfaces annotation match the three approved catalogue layers. No extra option, serializer hook, reset, package, dependency or friend was added. |
| Process-wide metadata and bootstrap | Both codec copies share one registry. Reservation/publication/first-use sealing, same-context idempotence, reentrancy/failure retry, construction outside locks and process lifetime were traced. No provider/scope/disposable ownership was introduced. Native installation uses the static Core bootstrap; legacy definition-first behavior remains. |
| Fixed codec and diagnostics | Generated resolution does not fall back to reflection. SFE-TYPE-003 is distinct from unsupported SFE-TYPE-002. The F1 initializer exception and customization negatives remain. DictionaryEntry enumeration is safe behind the exact Dictionary runtime allowlist. The two open findings concern member boundaries, not the dictionary replacement. |
| Typed delegate/step dispatch | Root/branch bindings, value-state replacement, typed snapshots, wait records and public callback wrappers were traced. Delegate identity is retained; ConditionalWeakTable does not permanently root application closures. Type caches contain closed operations, not service providers. Existing exception envelopes remain. |
| Fingerprints and compilation | The contract-validation extraction preserves type discovery, with early generated snapshot closure checks. Snapshot metadata is excluded from established fingerprint inputs. Each typed partitioner identity matches its former reflected type/property representation. Context type does not enter fingerprints. |
| Parallel/ForEach/merge behavior | Typed result/outcome/item lists replace dynamic generic construction. Existing schema, duplicate, ordering, limit and terminal checks remain. Typed item snapshots preserve detachment and the pre-body 256 KiB check. Nullable and value-type outcomes run through the public consumers. |
| Failures and projections | Direct constructors preserve validated values. The approved failure-corpus repair retains authored cause ordering/provenance rather than reducing a failed scope to one leaf. WorkflowFailure and its copied/read-only Causes are immutable; the stored projection introduces no mutable business-state alias. |
| Facade, lifecycle and resources | Typed per-registration visitors replace method/property lookup. Preflight, start-key binding, admission/acceptance/abort gates, query/output handling and internal event routing retain their owners. Retention, Closed/Stopping/Completed, physical joins and DI disposal rules were checked against callers and their existing tests. |
| Other engines and compatibility | Shared Core/codec changes were included in the review. Durable/hosting/DAG tests and real provider/integration lanes remain green for their existing JIT paths. Generated/native use by other engines is not newly promised. |
| Requirements, docs and baselines | Four canonical spec owners, NF-034/AC-563–574, document 17/API declaration, API/compile/package baselines, source-risk classifications, approved amendments and release/profile wording were reconciled. Structural checks agree; F1/F2 are the semantic exceptions that those checks missed. |
| Harness, native evidence and CI | Original journeys are unchanged; added cases retain explicit assertions, process isolation, warning decisions, package identities and native feature checks. The workflow change removes only the temporary push trigger, leaving jobs/execution flags unchanged. No test-runner migration or blanket AOT badge appears. |

The per-file source audit has 42 distinct entries with file hashes and individual
notes in the local `source-audit.json`. The original 90 risk dispositions and 14 R1
additional entries were compared with the affected paths. In particular, the
intentional public-member-only getter boundary in risk rows 16/17/97 explains why
F2 survived the earlier getter-count repair; those controls must not be weakened.

## Fresh differential and execution evidence

Two disposable worktrees held the current frozen target and the original master.
Both full Release builds and exact-package feeds succeeded with zero warnings/errors.
External consumers used PackageReferences, explicit local feeds, fresh package
caches and empty ancestor-MSBuild boundaries.

| Check | Result |
|---|---:|
| Pre-AOT master: original public journeys | 16/16 |
| Current public JIT preflight, including original legacy/generated journeys | 76/76 cases |
| Original journey outcomes/assertion counts/final phases vs master | Exact match, 16 legacy and 16 generated |
| Additional codec/fingerprint corpus, 13 cases × 3 modes | 39/39, no differences/errors |
| Real PostgreSQL provider tests | 103/103 |
| Real SQL Server provider tests | 74/74 |
| Integration tests | 11/11 |
| Native support-gate negative unit tests | 4/4 |
| Recorded native six-cell matrix, verifier rerun | Pass |

The added corpus compares original-master legacy, current legacy and current
Generated mode. It covers mixed/inherited included fields, property ordering,
extension data, omitted/present constructor-bound and mutable defaults, readonly
getters, KeyValuePair, DateOnly, TimeOnly and nullable record structs. Public input
fingerprints, definition fingerprints and detached results match in all 39 runs.
These positive controls supplement, and do not waive, the negative F1/F2 repros.
The baseline and current JIT checks used .NET 10.0.14.

The local preflight is explicitly **Windows 11 JIT**. Its CLI ends with the expected
“Unqualified native host” rejection after recording all 76 passing cases; it does
not count as another Server 2025/Ubuntu/native-AOT execution. It emitted no warnings.
The six recorded real native cells were reverified, not rerun. All **360 source/build
input hashes** match current files. Of 18 harness inputs, only the workflow differs,
and its exact difference is deletion of the temporary branch trigger.

All four original consumer files are byte-identical to pre-AOT master. Existing
runtime/provider/integration tests were not rewritten or skipped: among those
projects only RepositoryGuardTests gained inventory checks. This limits the chance
that green compatibility results came from changing their expectations.

The same-day preceding review's full unit **969/969**, Infrastructure **302/302**
and OpenSpec **25/25** results are reused because their code/frozen inputs are
unchanged. The built ReSharper 2026.2.3.1 run already covers all 42 changed product
files plus 10 guards: **0 errors, 583 warnings, 343 notes**, with full SARIF and
triage retained; its normalized diagnostics match the earlier 52-file baseline.
It was not rerun or represented as a second clean inspection. The prior 50-run
stress/aggregate-coverage results are carried, not repeated in this audit.

## Preservation and cleanup

Local evidence is at
`.artifacts/publishing-validation/issue-13-whole-aot-review-2026-10-28/`:
`scope.json`, `source-audit.json`, differential comparisons, provider TRX/logs,
preflight records, package hashes, byte-verified copies and cleanup/end receipts.
About 0.9 MB of compact raw evidence was retained; no binaries/packages/caches were
copied into review artifacts. Both owned worktrees, `E:/oc13full28` and `E:/oc13b28`,
were removed after verifying their markers, HEADs, preserved evidence and absence
of foreign edits, reclaiming about 1.55 GiB of scratch/build/consumer/cache files.
The provider fixtures disposed their containers; shared images/caches were left
alone. Earlier blocked cleanup paths were not retried or bypassed.

The original frozen 30-path target, prior verdicts, branch/HEAD and empty index
remain intact. The next work is a focused, reviewed F1/F2 fix or contract
reconciliation with regression/native coverage. The whole AOT change remains held
for that work; passing existing matrices alone does not override the findings.
