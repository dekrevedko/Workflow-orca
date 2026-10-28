# Issue #13 task 2.2 independent review — 2026-10-25

## Verdict: REJECT / incomplete source slice

This is a separate self-review pass, not an external human approval. Do not
advance to task 2.3 or checkpoint this state. Task 2 remains uncommitted and task
2.2 stays unchecked. The metadata implementation and current matching baselines
are retained; resolving the exact-catalogue conflict requires another reviewed
contract amendment, not an unreviewed public export.

## Reviewed state

Base/HEAD `10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd`, branch
codex/issue-13-native-aot-trimming. The 64-path partial snapshot (excluding this
verdict) has tree `ec1d2cadbaa73efda377d9ad716e700bd85073fa` and content SHA-256
`5bcaed5c6396aa05d1bc6f78366090904d894e6df9932af6c2e3427c249ea0d7`. Hash rule and exact path list are retained with the local
receipts. All staged rehearsal blobs were byte-equal, whitespace was clean and
the author index stayed empty. This is an incomplete/red snapshot, not a green
review freeze. Earlier task-2.1 artifacts/docs remain unchanged; its current
companion guard pin is intentionally updated with source.

## F1 — P1: ordinary strong-value generation conflicts with the catalogue

A normal Metadata context for CorpusState containing public EventName fails
compilation with SYSLIB1220 and SYSLIB1030. Its JsonConverterAttribute names the
internal StrongStringValueJsonConverterFactory, which the external generator
cannot access. A separate seven-line consumer that never calls the new API
reproduces both errors. Both the factory and its public strong-value attributes
are byte-equal to HEAD, so runtime registration and the later package-cache setup
mistake are not the cause.

The approved protocol requires strong values in an ordinary generated closure,
remediation of OrcaCore-attributable supported-path diagnostics and no blanket
suppression. Its catalogue authorizes only WorkflowJsonMetadata.Register. Runtime
product metadata fixes direct strong-value lookup but cannot repair that external
compile failure. The implementation cannot truthfully satisfy the contract by
omitting that root or switching serializers.

The concrete proposal is
[the converter catalogue amendment](task-2-2-converter-catalogue-amendment-proposal-2026-10-24.md).
It exposes only the existing sealed factory with its exact constructor/overrides,
leaving its closed switch/body and all wire behavior unchanged. No such export
was made. The pending user decision is whether to prepare that amendment for
review or investigate a catalogue-preserving alternative. Design decision 1
explicitly requires a reviewed catalogue amendment for additional public surface.

## F2 — P2: the required failure corpus has no passing proof yet

The new public failure/size corpus stops on its nested-failure assertion. Two
named failing branch steps currently expose a Failed snapshot with
WF-LEGACY-LIFECYCLE and zero Causes, rather than the nested tree the corpus needs.
A fresh unconfigured legacy control produces the same observation. This does
not establish a metadata regression; the harness/underlying existing failure path
must be investigated and the required nested-failure evidence completed. Do not
weaken the assertion or count the size assertion after it as executed. Task 2.3
already owns the downstream failure/dispatch audit, but this review grants no
advance past the still-incomplete task 2.2.

## Implemented and inspected

The exact public API delegates to one authoritative OrcaCore registry used by
both linked codecs. It reserves configuration under a short lock, constructs and
validates outside that lock, publishes atomically, seals on first authoring/type/
codec use and never resets or captures/disposes a provider. Same-type repeats
avoid reconstruction. Pending/conflicting types reject before construction.
Async-flow reentry is explicit; a completed construction marker is deactivated
so later inherited execution contexts can repeat valid registration.

Validation checks generated provenance, baked configuration, actual default
options/resolver state and read/write generation. Both P3 negatives reject before
publication with valid retry. Metadata supplies closed product contracts, no
reflection fallback in Generated mode, root/member diagnostics and the .NET 10
F1 rule over configured metadata without model construction/getters. Known
unsupported shapes keep SFE-TYPE-002 before missing metadata; absent contracts
use SFE-TYPE-003. Static role registration installs Core directly while the legacy
JIT missing-engine path remains available.

The public API capture changes exactly two lines (the approved class and generic
method); no other exported surface, dependency or friend changes. The declaration,
positive compile/package usage, diagnostic codes/descriptors and current hashes
move with source. Existing test assertions were preserved; twelve explicit
pending-native unit dispositions confer no execution/support credit. A compiler
helper was moved intact into its existing partial class to respect the unchanged
1000-line limit, rather than adding a size waiver.

Review controls caught and fixed missing context/seal diagnostics and an
incorrect rejection of an explicitly empty converter array. Constructor causes
remain in the exception chain. The failure wait uses the public output completion
boundary, not an immediate snapshot timing assumption.

## Validation and practical limits

- Strict OpenSpec: 25/25. Canonical/delta/earlier approval artifacts unchanged.
- Root Release warnings-as-errors build: 0 warnings / 0 errors. The standalone
  consumer is intentionally outside this solution build and remains unbuildable.
- Six existing CI unit projects with coverage: 969/969 after the diagnostic
  descriptor, pending-native catalogue dispositions and compiler split were fixed.
  The initial Core run's three maintenance failures are superseded, not counted
  as green. No native or new-corpus acceptance follows from these old controls.
- Actual public API baseline: 1/1. Positive declaration compile: 0/0. Exact
  current primary package consumer compile: 0/0 from a fresh cache with fallback
  folders disabled; restored SHA-512 matches the newly packed feed. The initial
  stale global fallback package error is retained as setup failure, not a product
  finding. All twelve current packages packed; no package was published.
- Before the golden expansion, 23 fresh-process cases passed. The exact current
  whole fixture fails at compilation (26 planned guard cases cannot receive
  behavior credit). A documented review-only variant omitting only the blocked
  CorpusState root runs 24/25 local controls; the failure corpus remains red.
  That variant is not exact-target green evidence and was restored afterward.
- ReSharper InspectCode 2026.2.3.1: zero errors; full scope and final registry/Core
  reinspection were read and triaged in the separate ReSharper review. No blanket
  cleanup or compatibility claim was inferred from exit status.
- Full Infrastructure and native/SQL/integration qualification are not claimed.
  Source-scope/inventory guards still need their exact reviewed reconciliation
  when the coherent implementation target is completed. No guard was broadly
  waived to hide the compiler blocker or grant source approval.

## Next action and cleanup

Resolve F1 through the pending concrete catalogue decision and independent
contract review before changing that public converter boundary. Preserve all
new tests and investigate F2 within the approved source work. Rerun the complete
exact target, ReSharper and independent review before marking task 2.2 complete
or advancing. Tasks 2.6/publication remain gated. Source/native approval stays
null; no commit, push, PR or issue edit occurred.

The disposable E:/oc13t22 checkout owns build, package, restore and ReSharper
caches and is removed after compact receipts are retained. Existing earlier
policy-blocked E:/oc13f1 is not retried or bypassed. Cleanup receipts live under
ignored .artifacts/publishing-validation/issue-13-task-2-2/.
