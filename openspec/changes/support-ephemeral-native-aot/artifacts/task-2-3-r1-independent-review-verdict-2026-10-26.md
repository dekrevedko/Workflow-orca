# Issue #13 task 2.3 R1 independent review verdict

Verdict: APPROVE task 2.3, no open findings. This is a separate self-review,
not external review or approval of the whole task-2/native target. Keep task 2
uncommitted. Task 2.4 and all later gates remain open.

Frozen 140-path tree `baf010a2c9a3ca019959fabf0d6e6672fc7a7ebe`, parent `10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd`;
content `d1177db5a7dec07f8e577daff2b956bbd8d3e1f83c501fa10eec705379c6e667` and porcelain `093a339fe2dad201aeb15535b1244e5d773d44c868b130a16b03636f05a58d2d`.
The author index is empty; disposable staged tree and all raw blobs match, with
clean staged whitespace and no drift during review. Request and verdict are
excluded from the tree. Review compared against approved predecessor
`ceef1c99341a39a70c77f13f530ca6ecc3d244f8` and the earlier task-2.2 source baseline.

## F1 resolved

The only product delta from the approved predecessor is
DynamicallyAccessedMembers(Interfaces) on TStep of StepExecutionThrottle.For.
The method body is byte-identical, with positive capacity checked before type
validation. The new guard was red for the absent annotation before application,
then green; a changed-capacity-body mutation is independently rejected and
restored. No suppression or other public annotation is applied.

The public-package tracer retains existing controls and adds inherited IStep,
exact derived-step identity/capacity, an annotated generic forwarder and real
named-step output. Invalid type and capacity exception/parameter ordering remain
unchanged. JIT, full trim and actual local Native AOT execute these assertions.
IL2090 is absent from source compilation/native analysis and both package links.
The previously held finding is closed, not reclassified away.

## Catalogue, source and process review

The compiled API changes exactly one line, adding attribute value 8192; eleven
other baselines and the authoring companion stay unchanged. Document 17 owns
this hosting-value declaration. The DAG API guard still pins the complete
current source/API hashes and normalizes only the exact approved full line to
reproduce the old metadata API hash and its original six additions. No generic
API, DAG source or package-compatibility exception is granted.

The binding records direct user approval of the immutable amendment and the
held review, preserving the earlier converter approval. All source/native/
whole-target approval fields remain null. Three existing issue-13 blocks are
changed and match their canonical deltas exactly; all other block identities,
199 predecessor rows, removal records and directory inventories remain exact.
Provenance: 206 rows, 195 synchronized / 8 superseded / 3 outside / 0 pending,
with 25 unchanged predecessor artifacts. Exact current hashes and owned live
documentation rows are reconciled, not used to rewrite historical verdicts.
The new binding was normalized to LF after the staged rehearsal caught CRLF;
only its byte pins changed, not approval data or the API.

The previously reviewed private typed paths are unchanged in R1. Earlier runtime
tests and all 866 retired rows remain unchanged. One new active metadata guard
brings physical accounting to 366 files / 1,511 declarations, active 212 / 823.
Source allowlists remain exact named paths. No dependency, friend grant, runtime
option, reflection switch, private root or broad compatibility badge was added.

## Verification

- Strict OpenSpec 25/25; Release warnings as errors 0 warnings / 0 errors.
- The frozen staged target passes full Infrastructure 302/302 with CI's
  engine-only coverage include; six CI unit projects pass 969/969 with coverage.
- Public PackageReference-only consumer passes 38/38 cases; product archives
  and fresh-primary-cache DLLs are byte-bound. Six selected probes pass in each
  of full trim and Native AOT, with SDK-default JSON reflection false.
- ReSharper 2026.2.3.1 completed valid solution/consumer and changed-guard
  inspections with build enabled, Any CPU and no errors. Warnings/notes are
  retained/triaged; static analysis is not represented as an empty report.
- The 38 individual current diagnostic records are fenced legacy compiler
  calls or deliberate handwritten-resolver JIT counterexample setup. None is
  the selected throttle finding; no native credit is given to the counterexample.

Task 2.3 alone is checked; 2.4 onward remain unchecked. Windows 11 development
probes do not supply native Windows Server 2025/Ubuntu 24.04 qualification or
complete NS-01–12/AOT-01–16 execution on six hosts/cells. No live SQL integration,
support claim, commit, push, PR, issue edit or runner migration is credited.
Cleanup status is retained separately with the compact receipts; source,
immutable decisions and this verdict remain preserved.
