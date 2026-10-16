# Proposed fingerprint storage-boundary amendment

Date: 2026-10-14. Review base: e634737420a490acceffae56c338ac9a20051b48.
Status: PROPOSED. This addendum needs its own independent approval and checkpoint
before the next Task 2.1 source slice. It is not product implementation, a new
friend, a canonical amendment, or completion of Task 2.1.

## 1. Authority, precedence and preserved history

The RC-3 contract was approved at checkpoint
e6802a348b0180d5eaad3be99b32e3204c5c25a1 (tree
d367ced646461fb78c8c85dea5bf1fb142e640f7), with direct-child evidence
578593efd3709a5e9a14582d572eb4275324de3e and the RC-3 APPROVE verdict
b7c9d54dcfdbcc8fd3ec2ef99c43fb313e9b400a038b6da7f13b969b2feead4c.
Its four-block canonical transition was approved at 1770b2b, evidenced at
18bf865 and activated at 89e3b18. The 2026-10-05 transition addendum remains binding.

The approved RC-3 contract, API catalog, standalone signatures, rejected packets,
verdicts and transition addendum are retained byte-exact. This proposed addendum
supersedes ONLY the two storage-record fingerprint parameter/property types,
their six API keys, and the corresponding reconstruction responsibility. Every
other RC-3 lifecycle, lease, identity, framing, scheduler, schema, certification
and package-boundary obligation remains in force. The new dated API catalog and
standalone signatures are replacements for future implementation comparison,
not edits to the historical RC-3 packet or currently shipped public baselines.

## 2. The implementation conflict and existing precedent

RC-3 puts DagRunRecordCodecV1 and the DAG records in Runtime.Protocol.
DagRunBindingV1.Fingerprint is a DefinitionFingerprint and
DagRunValueV1.Fingerprint is a PayloadFingerprint. Both OrcaCore types have
internal-only constructors, no public parse/factory or JSON rehydration path,
and Runtime.Protocol, providers and Durable.Hosting are not OrcaCore friends.
The signature-only RC-3 proof declared these types but never reconstructed them.
It therefore did not prove the decoder or any provider load could be written.

The existing workflow path stores fingerprint strings in WorkflowEvent,
WorkflowCommand and provider records. Engine.Durable, an existing OrcaCore
friend, adapts persisted text to application values in DurableContractAdapter
(source file DurableApplicationContractFactory.cs). Follow that division, not
a new permission. Definition and payload hashing algorithms and existing
workflow compatibility are unchanged.

## 3. Exact proposed declarations and API delta

Namespace OrcaCore.Abstractions.Durable.Dag, assembly OrcaCore.Runtime.Protocol:

```csharp
public sealed record DagRunValueV1(
    bool IsPresent, string ContractKey, string CodecFormat,
    byte[] Bytes, string Fingerprint);
public sealed record DagRunBindingV1(
    DefinitionId DefinitionId, DefinitionVersion DefinitionVersion,
    string Fingerprint);
```

BEFORE, the last parameters were respectively PayloadFingerprint Fingerprint
and DefinitionFingerprint Fingerprint. AFTER, both are non-null string.
Each positional constructor, init property and generated Deconstruct parameter
changes: exactly six old proposed keys are replaced by six new keys. No other
member, parameter name, nullability, type, enum or record shape changes.

The replacement full catalog preserves the original pre-RC-3 comparison base:
7 OrcaCore + 193 Runtime.Protocol + 165 Provider.Abstractions = 365 additive keys.
The seven OrcaCore keys already shipped at c93411e; they are not seven additional
changes in this amendment. Of the 365 proposed keys, 359 remain byte-identical
to RC-3 and six are replaced. No existing shipped API key is removed. Protocol's
new complete extraction SHA-256 is
703536ec79dd945894ec58ac9593a90cf3b81dca13c5013e12f503e18a223a50.
OrcaCore and Provider.Abstractions extraction digests are unchanged.

There is no direct provider-port signature change: append, checkpoint, tail,
discovery and mutation signatures still carry the same named enclosing records.
Their nested binding/value fingerprint representation changes transitively.
The internal child receipt's PayloadFingerprint, authored definition references,
and application snapshots remain typed; they are not storage DTO constructors.

## 4. Exact string validation, not weaker binding

For these TWO DAG record fields only, a fingerprint is exactly 64 uppercase ASCII
hexadecimal characters (0-9, A-F), non-null and non-empty, with no whitespace,
prefix, trimming, padding or case conversion. Lowercase/mixed-case, non-hex and
63/65-character values fail closed. DefinitionFingerprint.ComputeCanonicalHash
and durable payload hashing already emit this uppercase SHA-256 text. Existing
workflow provisional values such as pending-definition are neither changed nor
admitted as a DAG binding fingerprint.

This does NOT change lowercase operation fingerprints, checkpoint SHA-256,
type-shape hashes, artifact hashes, child-identity recipes or contract keys.
Those retain their separately approved RC-3 formats. Never apply one casing rule
to every field named fingerprint/hash.

Every future DAG DTO constructor and every port/decoder revalidates the two
fields; init/with and caller-owned buffers cannot bypass validation at a storage
boundary. Existing defensive copying/whole-record validation remains required.
Malformed protocol JSON is rejected before returning a record (JsonException);
malformed persisted checkpoint/provider rows must not return partial snapshots
or create an application fingerprint to conceal corruption.

Shape is NOT authenticity. Providers/codec preserve text and exact bytes but
cannot certify an installed compiler definition. Recovery resolves the exact
definition identity/version, compares its installed typed fingerprint.Value
ORDINALLY against the stored text, and rejects a binding mismatch before mapping,
admission or child start under the existing integrity rules. Value fingerprint
text must equal uppercase SHA-256 of the EXACT stored fixed-codec bytes before
the runtime accepts a value. A different well-formed digest is still invalid.
Present-null remains the four bytes null, with their actual digest, not absence.
An unknown definition, mismatched digest or corrupt protocol is not an ordinary
mapper/decode failure and must not be relabeled DAG_INPUT_MAPPING_INVALID.
No change to slot retention, uncertain-child reconciliation or failure policy
is authorized by this representation amendment.

## 5. Complete reconstruction-path inventory

| Path | Proposed representation and owner |
|---|---|
| Authoring graph creation | Dag owns authored DefinitionFingerprint; read its public Value into DagRunBindingV1. No text-to-typed constructor in Hosting. |
| Record decoder | Runtime.Protocol constructs nested graph/binding/value records from validated strings; never reconstructs an OrcaCore fingerprint object. The eight-kind framing/property allowlists remain unchanged. |
| Created record and checkpoint decoding | Nested graph, run input, every node input/output and binding carry the same strings. Checkpoint framing and hash verification remain mandatory. |
| InMemory restart/copy/materialization | Copy/validate the same raw text/bytes at all record, checkpoint and node-index boundaries. Host-local state cannot become authority. |
| PostgreSQL/SQL Server loads | First-create binding_payload, node/value/checkpoint/event/receipt rows reconstruct strings through the same protocol/DTO checks. No provider gets a typed factory, serializer hook or new project edge. |
| LoadDagRunCheckpointAsync/LoadDagRunTailAsync | These existing proposed port names return the same checkpoint/record shapes, now string-based at every nested fingerprint. |
| GetDagRunByStartKeyAsync/ListRecoverableDagRunsAsync | Discovery's DagRunBindingV1 contains fingerprint text, not an unreachable typed constructor. |
| Append/operation replay/collision/reopen | Compare complete binding/value coordinates and exact string/byte hashes; do not normalize and silently accept a divergent replay. |
| Durable.Hosting read/create/recover/prepare/join/projection | Reads text-based provider records. Uses existing Engine.Durable access for verified runtime value construction; it does not invoke OrcaCore internals itself. |
| Public DAG snapshot/child receipt/conflict output | Typed values are runtime-created at the existing Engine.Durable boundary after binding/byte checks, then passed through existing internal edges. Storage DTO changes do not turn raw caller text into trusted public values. |

Only existing friends construct the strong values. Dag already constructs authored
definition fingerprints through its guarded authoring seam. Engine.Durable owns
verified persisted/runtime projections, including payload fingerprints, reached
by Durable.Hosting through the existing Engine.Durable -> Durable.Hosting edge.
Implementing the narrowly scoped internal adaptation inside that existing owner
is a future reviewed source task, not a new public factory, exported API, friend
or Dag/Hosting codec permission. No new Hosting-to-engine interface or exact
runtime member allowlist is silently introduced by this addendum.

## 6. Rejected alternatives

- OrcaCore -> Runtime.Protocol friendship: contradicts the approved nine-friend
  boundary and grants broad internal access to a public protocol package.
- A public Parse/factory, JSON converter or JsonConstructor on either opaque
  application value: permits application-created compiler/runtime fingerprints,
  grows OrcaCore's API, and fixes only one of several reconstruction paths.
- Reflection, UnsafeAccessor, expression compilation or dynamic construction:
  forbidden bypasses, not package-boundary solutions.
- Move the codec to Core/Engine or add a resolver callback to Deserialize:
  changes the approved package ownership or exact signatures unnecessarily.
- A cache of existing typed objects: loses restart/provider independence and
  cannot implement the byte-only Deserialize signature for a cold load.

## 7. Normative crosswalk and why no canonical sync is needed

The two changed CLR field types are specified only in the RC-3 artifact/API
catalog. A reverse sweep of docs/specs and openspec/specs finds neither
DagRunValueV1 nor DagRunBindingV1. Both active delta files likewise do not name
their fingerprint fields or require typed strong values at persistence boundaries.
Canonical typed STREAM identity and typed lease/port DTO obligations do not
require typed fingerprint payload members. General fingerprint identity, exact
bytes, no-new-friend and compiler/runtime-only construction all remain satisfied.

| Source | Disposition |
|---|---|
| durable-persistence-and-outbox: three registered headings | Unchanged. Strings still persist exact graph/input bindings, bytes, checksums and replay coordinates through the same typed ports. |
| durable-runtime: Durable DAG progression is runtime owned | Unchanged. Authored fingerprint and committed-byte semantics, mapper policy and package edges do not change. |
| docs 06/16 | Excluded: durable records, restart/fencing/atomicity do not prescribe these CLR fingerprint fields. |
| doc 10 | Excluded: exact provider ownership and fingerprint conflict behavior remain; plain text follows existing workflow ports. |
| docs 03/04/08/12 | Excluded: domain opaque values, workflow semantics, DAG execution/acceptance and concurrency obligations are unchanged. |
| doc 17, including compile-shaped companion | Excluded: compiler/runtime-only constructors and typed public/application snapshots remain exactly as written; neither storage record is declared there. |
| doc 11 | Excluded: no dependency, nonfunctional or provider-family obligation changes. |
| Canonical repository-foundation / developer-facing-surface | Excluded: no friend, public factory, reflection bridge or project-reference change. |
| Binding guides / CLAUDE.md / Task 8.0 map | Excluded: no package, source-ownership or task re-sequencing change. The known stale CLAUDE status sentence is reserved for the next source slice as requested. |

Therefore this target changes NO canonical file, delta, numbered spec, guide,
Task 7.3 row/digest or Task 8.0 map. Canonical full-file content, preambles,
184-row requirement record and superseded provenance catalog remain byte-exact.
This is an explicit contract/API refinement inside the existing post-gate run
amendment, not a fifth requirement owner or silent normative preference.

## 8. Proofs and future source exits

The dated standalone signatures change exactly the two parameter types. They
compile together with the dated reconstruction example as assembly
OrcaCore.Runtime.Protocol, referencing ONLY shipped OrcaCore.dll; OrcaCore's
unchanged friend list grants that assembly no internals. The example reconstructs
both string records across three simulated boundaries, exercises exact formatting
and committed-byte checking (21 negative cases), and its forbidden-constructor
compile mode must fail CS1729 for both strong values. It uses no private-member
access, converter hook, new friend or fake application fingerprint class.

This is a scoped construction/format proof, NOT the eight-kind production codec,
provider storage, real recovery, full checkpoint/reducer validation, DG-P credit,
or proof of the lease protocol. The metadata prototype separately compiles all
three proposed owning assemblies and captures exports using the existing exact
API formatter; compiler-generated record members are included. Product src and
the fourteen current public baselines remain untouched.

Future source review must prove each reconstruction row, exact two-field format
rejections at every boundary, a well-formed wrong definition digest, byte/hash
mismatch, restart with empty process caches, unchanged typed public snapshots,
and no access bypass. DG-P01/04/13/18 remain the relevant source/certification
exits; this contract proof does not satisfy them. The next Task 2.1 source target
also owns the three round-100 carry-forwards: lock task 1.4 complete, prove early
rejection before storage, and correct the stale CLAUDE.md sentence.

## 9. Registry and checkpoint protocol

The post-gate registry's dedicated fingerprint amendment record binds this
addendum, replacement catalog, signatures and reconstruction proof by exact hashes
to the real RC-3 checkpoint/evidence/verdict. Existing Proposed/ApprovedPending
successor history and four supersessions are unchanged. This new record remains
Proposed with null approval while review is pending; semantic sync eligibility
does NOT authorize it. Its source pins survive clearing the temporary freeze.

After independent APPROVE: checkpoint exactly the reviewed target; its only
direct-child evidence commit adds/catalogs the byte-exact final APPROVE verdict
and clears activeFreeze while retaining the history pointer. NO activation or
task checkbox change is required for this partial-contract refinement. That
evidence state must stay green. The NEXT reviewed source target must bind the
amendment checkpoint/tree, single-parent evidence commit, actual verdict addition,
unique final APPROVE line and exact artifacts, following the transition-addendum
precedent. A proposed/null or mismatched approval cannot authorize implementation.
Task 2.1 and all later tasks, reshape 8.4/8.5 and Complete remain open.
