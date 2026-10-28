# Issue #13 task 2.5 — independent whole-target self-review (2026-10-27)

Verdict: APPROVE the task-2 canonical/implementation/native-evidence target for
external approval. No blocking findings. This is a separate self-review, not an
external reviewer or direct user source/native approval. Keep task 2 uncommitted.
Task 2.6 and every publication action remain gated.

Reviewed base: 10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd.
Input tree: 30777062d26e7a8542eaffbdfd08bbd9fbe29652.
Validation record: task-2-5-validation-record-2026-10-27.json,
SHA256 c26c101f802fc6f39fecbfb17c88f26b978e94e1a6e5337304c0a8df0df50c0b.
The final self-inclusive checkpoint manifest and freeze request identify the
complete target including this task's evidence and mechanical completion tail.
This self-review is part of the checkpoint manifest because the completion guard
requires it. A later external approval verdict is excluded and becomes the sole
verdict-only child. Preserve this self-review byte-exact once frozen.

## Contract and implementation

The public surface is exactly WorkflowJsonMetadata/Register, the approved existing
sealed converter's accessibility and the approved Interfaces annotation on
StepExecutionThrottle.For<TStep>. No new package, dependency, friend, reset,
serializer hook or package-wide IsAotCompatible/IsTrimmable badge was introduced.
The converter body/eleven-type table and throttle validation body are preserved.

One authoritative registry lives in OrcaCore and is shared by both codec copies.
Reservation, construction outside the lock, atomic publication and first-use seal
have consistent linearization. Same-type registration is a construction-free
no-op; competing types and reentrant construction fail with context/seal detail.
Failed validation publishes nothing and permits retry. The retained registry
owns no service provider/scope/disposal and cannot reset at host stop.

Generated metadata uses fixed product options/converters; schema validation
separates missing metadata (SFE-TYPE-003) from unsupported shape (SFE-TYPE-002).
The declared F1 omission/member-initializer rule is applied without model/getter
execution and does not reject an otherwise-valid context's unused excluded type.
Baked enum/explicit converter/handwritten resolver negatives preserve valid retry.
Golden values, eleven strong types, nested failures and fingerprints retain parity.

Closed adapters carry generic state/result/item/input operations through
bootstrap, registration, steps, waits, snapshots, composition and outputs.
ConditionalWeakTable keys avoid process-static capture of application delegates
or partitioner closures; type-only caches retain no instances/providers. The
legacy reflective fallback is fenced from Generated mode. The bounded metadata
checks and original 90 plus 14 added risk dispositions were checked against the
selected source/guard changes and native diagnostic closure.

The nested-failure repair preserves the asserted failure tree rather than changing
expected values. New typed dispatch does not change acceptance/abort authority,
physical cancellation ownership, callback settlement, retirement pins or shutdown
joins. Existing runtime tests are unchanged apart from the earlier approved
inventory-only RepositoryGuard additions; no weakened assertion or new skip was
introduced by task 2.5. Canonical blocks and the three directly approved amendment
layers remain exact, with all earlier artifacts/verdicts immutable.

## Execution evidence

Rechecked OpenSpec 25/25, warnings-as-errors Release 0/0, Infrastructure 302/302,
all six CI unit projects 969/969, PostgreSQL 103/103, SQL Server 74/74 and active
integration 11/11. All report zero failures/skips. CI requirement selectors pass
60 additional repeated cases. Fifty consecutive unfiltered dotnet test ephemeral
runs, two at a time under concurrent load, pass 352/352 each (17,600 executions).
Benchmark smoke, exact-feed/clean twelve-package consumer and eight publication
identity/negative checks pass without publishing.

ReportGenerator 5.5.11 with the CI assembly filter reports 15,952/20,112 engine
lines, 79.31583134447097%, above 75%. Individual unit/Infrastructure inputs are
retained; provider coverage was not added to this numerator. ReSharper's completed
52-file built inspection reports 0 errors, 583 warnings and 343 notes, all retained
and triaged. Closure warnings were checked against actual ownership transfer and
lease implementation, rather than dismissed only because the lines were old.
No finding requires product changes.

Native run 40629471843 remains genuine Windows Server 2025 and Ubuntu 24.04
execution: each host JIT 76/76, full-trim 51/51 and AOT 51/51. Independent raw
reverification checks source/harness/package identity, logs, all 16 original
journeys, twelve NS groups, thirteen mandatory subcases and every diagnostic
classification. All current source/harness bytes match. Disabled reflection and
AOT dynamic-code facts are recorded per process. No private roots, switches or
suppression replace execution. Live event delivery/resumption and publishing
remain explicitly unqualified under the direct user-approved boundary amendment.

## Limits and completion tail

These local provider lanes used Windows with real Linux Docker databases; CI's
Linux provider process and future PR CI remain uncredited. Existing inactive
provisional/DAG exclusions are not counted as tests. The bounded native recipe
is not twelve-package/RID-wide support or a claim about earlier releases.

The only allowed completion tail is: tick 2.5, bind this exact record and verdict
in the existing registry extension, require both by exact hash/current product
source/artifact/green-lane checks, and keep global source/native approval null
and 2.6/3.x open. Rebuild/re-run the full Infrastructure lane and scoped InspectCode
for that mechanical guard change, then record their actual results in the final
freeze request. No product/harness/baseline edit is covered by this allowance.
Clean owned scratch/build/package caches after compact evidence is preserved;
never delete shared images/caches, unrelated worktrees or bypass a cleanup block.
