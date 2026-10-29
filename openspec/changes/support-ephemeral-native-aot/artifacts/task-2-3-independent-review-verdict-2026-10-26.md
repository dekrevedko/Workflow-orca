# Issue #13 task 2.3 independent review verdict

Verdict: HOLD task 2.3 completion for F1. The private typed-dispatch implementation
is green and the minimal catalogue amendment is recommended for approval. Do not
apply the annotation, tick 2.3, advance to 2.4, commit or publish before the
required authorization. This is a separate self-review, not external review or
user approval.

Frozen target: 127 paths, candidate tree `ceef1c99341a39a70c77f13f530ca6ecc3d244f8`, parent
`10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd`; content `56d38b00ab738071ee7820cfadc23103ccfd5cbfb75fbee8a6d54658e810d298` and porcelain
`9f0b810c5673822f906a7a58c33507961d801f1f6b563f5332d84df758316c23`. All target blobs match their working bytes and nothing
drifted during review. This verdict and the request are excluded from that tree.
The author index is empty. Review used the diff from task 2.2 tree
`2e276cbeddf908ff41d75c762582fec80e6737b8` and read the frozen registration/start/event,
invocation, snapshot/merge/codec, declaration admission, ownership fixtures and
source-guard changes separately from the implementation pass.

## F1 — P2: the public throttle boundary still lacks interface metadata

`src/OrcaCore.Abstractions/Hosting/StructuredExecutionHostOptions.cs:70` calls
GetInterfaces on the unconstrained public TStep in StepExecutionThrottle.For.
IL2090 is reproduced by source compilation, source Native AOT analysis, the
PackageReference-only Native AOT link and the full-trim link. These are four
observations of one boundary, not four independent defects. A successful concrete
execution does not establish its required generic metadata contract.

The approved design explicitly requires another reviewed catalogue amendment
before any public generic-parameter annotation or baseline change. The public
method and all API baselines remain unchanged here. The proposed
[amendment](contract-throttle-metadata-catalogue-amendment-2026-10-26.md) adds only
DynamicallyAccessedMembers(Interfaces) to this existing TStep. It retains capacity
validation before type validation, the existing exceptions/parameter names,
exact-step identity, DI and physical throttle ownership. No new constraint,
interface, overload, dependency, runtime option, descriptor or suppression is
needed. I found no further issue in the proposed amendment; explicit user
approval is still required before applying it. After approval, reconcile its
catalogue owners/baseline, add inherited-step controls and close this diagnostic,
then revalidate and review task 2.3 before advancing.

## Implementation findings resolved before the freeze

1. The first metadata graph rewrite called a nonpublic JsonInclude getter an
   extra time. Its new legacy control passed while Generated mode failed the
   getter-count assertion. The frozen descriptor visibility check restores the
   existing graph boundary; both modes, trim and Native AOT now pass.
2. Missing internal item-array metadata previously left TryBuild successful.
   The new control now gets SFE-TYPE-003 before Init/item projection. The extra
   snapshot validation is metadata-only and does not change schema/fingerprint
   inputs. Existing golden bytes/fingerprints remain equal.

The typed visitor passes closed state across an existing friend boundary and
captures engine/definition only per registration. Acceptance/abort, publication,
locks, idempotency, routing and lifetime pins stay in their original owners.
Typed Func/snapshot/result/item bindings avoid selected dynamic generic closure,
expression IL and property/constructor discovery. Weak callback/partitioner keys
do not create process-lifetime DI captures. Original exception envelopes,
ordered nullable/outcome handling and fixed snapshot limits are retained.

No earlier runtime test or immutable task-2.2 artifact drifted. Canonical specs,
approved exports/baselines, strong-value converter body and earlier registry
approvals are unchanged. The exact source allowlist is explicit; no other engine
or broad package compatibility exemption was added. The 866 retired rows remain
unchanged and receive no passing credit. All 90 original risk candidates, 14
supplemental sites and 42 diagnostic records have individual dispositions.

## Checked evidence and limits

- OpenSpec strict 25/25; Release warnings as errors 0/0.
- Frozen target's full Infrastructure lane: 301/301 with CI's engine-only
  coverage include. Six CI unit projects: 969/969 with coverage and unchanged
  original runtime assertions.
- PackageReference-only consumer: 38/38 process cases; exact local-feed product
  archives and primary-cache DLLs are byte-bound. Six selected probes pass in
  each of local full trim and actual Native AOT, with JSON reflection disabled
  by default and native dynamic code false.
- ReSharper 2026.2.3.1: completed valid solution and explicit consumer-graph
  inspections, build enabled, Any CPU; 0 errors in both. The 581/343 and 28/42
  warning/note counts are retained and triaged, not described as a clean report.
- The concurrent ReSharper/test build failure and unfiltered coverage/DAG guard
  failure were harness setup errors, excluded from green evidence and corrected
  in the final serialized run. No guard was weakened.

Native diagnostic publication collected warnings with TreatWarningsAsErrors=false.
The selected throttle diagnostic is unresolved. Handwritten-resolver framework
counterexamples are JIT negative setup, not supported native scenarios. These
Windows 11 development probes do not qualify Windows Server 2025, Ubuntu 24.04,
all native NS-09–11 callbacks or the six-cell task-2.4 consumer matrix. No SQL live
integration, runner migration, support claim, checkpoint, push or issue edit is
made. Cleanup retains source/verdicts/compact receipts and removes only owned
validation/build/restore/inspection output.

Cleanup outcome: automatic approval review rejected the scoped removal command
with only “blocked by policy”; the command did not execute. E:/oc13t23 remains
registered, and the eight author bin/obj directories remain. The exact resolved
ownership/size inventory and blocked result are retained in the compact receipts.
No alternate deletion method was attempted; all frozen source and evidence remain
intact. This cleanup status adds no product finding or permission to advance.
