# Native discovery diagnostic self-review — 2026-10-27

Verdict: APPROVE the 24 individually listed non-product counterexample diagnostic
records for the final validation rerun. This is a separate self-review, not
external or whole-target source approval. No general warning category is waived.

Native discovery run 40628433270 at c863d1a87268173bb1a4314b052589fb72459bda
executed all six real host/profile cells: each JIT 76/76, each full trim 51/51,
each AOT 51/51. All publishes and assertions succeeded, host checks qualified,
source was clean, all twelve package hashes matched, selected DLL/archive bytes
matched, and source/tree/code-harness/golden/strong-value identities agreed.
Both legacy JIT controls agree. Every group and all thirteen mandatory subcases
passed. Four job conclusions were failure solely at the unclassified-warning gate.

The adjacent TSV lists every record: four IL2026 rows per trimmed cell and eight
IL2026/IL3050 rows per AOT cell, 24 records/20 distinct normalized identities.
Each occurs once. None comes from OrcaCore or the positive journey. All are the
deliberate handwritten-context framework counterexample in the metadata consumer.

Reconstruction of the exact materialized Program.cs matches both hosts' input
hash. Line 349 calls CreateJsonTypeInfo<Payload>; line 351 creates the known int
property named changed. The remaining source/link/AOT records name those same
calls inside HandmadeContext.GetTypeInfo. The framework warns because these APIs
can require arbitrary metadata/generic code. This counterexample uses only the
closed Payload/int graph, direct construction/get/set delegates and int metadata
rooted by the generated DefaultContext. All four actual reflection-disabled
native artifacts reproduce changed:7, then the engine rejects that handwritten
resolver before publication/seal and successfully retries the normal generated
Value:7 context. The fixed product codec never accepts or invokes the resolver.
That is an exact bounded safety proof, not support for arbitrary custom resolvers.

Every JSON decision binds its own identity/count/code, cause, normalized message,
source hash/lines, discovery run and executable witness. No suppression, rooting,
private bridge or product code change was made. Applying only these reviewed
decisions to in-memory copies of the raw discovery records passes all six gates;
raw reports/logs remain unchanged. New/stale/changed warnings must still fail.

Decision file SHA256: 2ac6cd0d4198cb9b2391c54abad20d346eaf3e9e1642114638aa132c6faf4234.
The source and consumer/harness code must remain byte-identical for the rerun.
Repacking at a new validation commit can change informational-version/source-link
metadata and package bytes; the final run must therefore bind its own one exact
feed across all six cells. No binary identity across the two runs is asserted.

Only the authorized validation branch may advance. The issue branch remains
uncommitted at 10aec46; source/native whole-target approval remains null, 2.4 is
not yet checked, and no PR/issue/release action is authorized by this review.
