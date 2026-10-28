# Issue #13 task 2.3 completion evidence

HEAD remains 10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd on
codex/issue-13-native-aot-trimming. Task 2 remains uncommitted. The approved
127-path predecessor tree ceef1c99341a39a70c77f13f530ca6ecc3d244f8 matched its
content/porcelain hashes before work. The approved proposal and held verdict,
all previous review artifacts, and the original 90-site inventory remain exact.

## Approved change and controls

The only product delta from that tree is
DynamicallyAccessedMembers(Interfaces) on the existing TStep parameter of
StepExecutionThrottle.For. The body is byte-identical. The new metadata guard
was red before application because the attribute was absent; it passes after
application. A structurally valid capacity-body mutation is also caught and
restored. No source/runtime assertion is weakened.

The public-package tracer now exercises an inherited IStep implementation
through the exact derived-step throttle, a generic helper propagating Interfaces,
and a successful named-step output. Existing invalid-type and capacity-first
exception/parameter controls stay intact. These controls pass in JIT, full trim
and actual local Native AOT, with SDK-default disabled JSON reflection in the
latter two profiles. IL2090 is absent from source analysis and both package links.

The compiled API capture changes exactly one line in OrcaCore.api.txt, adding
DynamicallyAccessedMembersAttribute(8192); the other eleven baselines are
unchanged. Its current SHA-256 is
4a2e0af3420a8126f1a943a1510f8009b5da9371276a3a98e1e03092da432d3a.
The older DAG API guard keeps the exact current/source hashes, normalizes only
that full approved line, and requires the prior 7de9a018… metadata API hash and
its original six additive keys. No general API or DAG-source exemption is added.
The matrix owns the throttle declaration. The authoring companion does not
declare this hosting value type and stays unchanged; the compile/package tracer
exercises the real API and annotated forwarding signature.

The binding records direct user approval separately from source/native approval.
Earlier converter approval remains immutable. Three existing requirement blocks
are reconciled exactly between deltas and canonical specs. All 199 predecessor
rows remain exact; the seven issue-13 rows are recomputed from their blocks.
Provenance is 206 rows: 195 synchronized, 8 approved supersessions, 3 outside and
0 pending. The 25 predecessor provenance artifacts remain byte-exact. Canonical
content digest is a6b35398005ee13b265ec9e82becf44dac68d5c677a188c1984e7514831bbab3.
Only owned live documentation/accounting rows and exact source pins change.

## Validation

- OpenSpec strict: 25/25; Release warnings as errors: 0 warnings / 0 errors.
- Infrastructure: 302/302, with CI's engine-only coverage include.
- Six CI unit projects with coverage: 969/969; earlier runtime tests unchanged.
- PackageReference-only cases: 38/38; exact local product archives/DLLs checked
  in the fresh primary cache. Native/trim SDK tools may reuse the shared NuGet
  cache read-only; product packages remain the verified primary copies.
- Full trim and Native AOT: six selected package probes pass in each on this
  Windows 11 machine. They include inherited/invalid/generic throttle controls,
  typed dispatch, ownership in both policies, golden bytes/fingerprints, failure
  and size boundaries, getter parity and early missing-array admission.
- ReSharper 2026.2.3.1: completed valid main solution, explicit consumer graph
  and exact API-guard inspections, build enabled, Release/net10.0, Any CPU.
  Zero errors; warnings/notes retained and triaged separately.
- Current diagnostic record: 26 source-AOT + 8 package-AOT + 4 package-trim = 38
  individually classified records. The four old IL2090 observations are closed.
  Remaining records are private legacy compiler-only calls fenced out of the
  supported path or deliberate handwritten-resolver JIT counterexample setup.
  No suppression, reflection switch, private root or compatibility badge added.

The first full lane found an old API pin still expecting the unannotated line;
its exact reviewed transition now passes. A temporary provenance tuple typo was
fixed before validation; it is not behavior-red evidence. No direct-runner
substitution or runner migration occurred.

Task 2.3 is checked only after green validation and the separate source/catalogue
review. Tasks 2.4 onward remain open. Source/native/whole-target approval fields
remain null. No real Windows Server/Ubuntu matrix, live SQL integration, support
claim, commit, push, PR or issue change is credited. Test accounting is 366 files /
1,511 declarations, active 212 / 823; all 866 retired rows remain unchanged and
uncredited. Compact logs, reports, package identities, TRX and coverage summaries
remain under .artifacts/publishing-validation/issue-13-task-2-3-r1/.
