# Independent review: DAG fingerprint storage-boundary contract amendment

Date: 2026-10-14. Base/parent e634737420a490acceffae56c338ac9a20051b48; tree 1ba8709b299d1c7b36b8f5f3175e37c156af96af.
Main stays UNCOMMITTED, with an empty index. This is a contract/API refinement,
not the next Task 2.1 product-source slice.

## Exact requested decision

Independently review the [dated addendum](../../openspec/changes/admit-durable-dag-run-progression/artifacts/task-2-1-fingerprint-contract-addendum-2026-10-14.md), replacement full API catalog, standalone signatures and scoped rehydration example, the appended proposal/design/task notes, and persistent review pins/registry. The self-inclusive [raw Git-order manifest](developer-facing-interface-section-08-task-8-4-dag-run-fingerprint-contract-amendment-dirty-manifest-2026-10-14.txt) names all fourteen commit-real paths: eight modified, six new. Six changed proposed API keys do NOT mean six checkpoint files.

This asks approval of replacing the two DAG storage-record fingerprint field types with non-null canonical strings, including every reconstruction path. No new friend, public application factory/Parse/JSON constructor/converter, reflection, UnsafeAccessor, expression bridge, runtime codec permission, package edge or product source is authorized. Current nine friends and fourteen shipped API baselines are byte-unchanged. Task 2.1 and every later task, reshape 8.4/8.5 and Complete remain open. The three round-100 findings stay reserved for the next source target; none is claimed fixed here.

## Why this amendment is necessary

The approved RC-3 byte-only DagRunRecordCodecV1.Deserialize lives in Runtime.Protocol, but its DagRunBindingV1 and DagRunValueV1 require DefinitionFingerprint/PayloadFingerprint objects. Those OrcaCore values have internal-only constructors and no supported public JSON rehydration. Runtime.Protocol, providers and Durable.Hosting are not friends. A signature-only proof never called those constructors, so it concealed the decode/load implementation conflict. Existing workflow protocol/provider records already store fingerprint strings and Engine.Durable adapts runtime values through its existing friend boundary.

The addendum inventories authoring-to-record projection, nested Created/checkpoint/record decode, InMemory copies, both relational providers' row materialization, checkpoint/tail/start-key/discovery ports, append/reopen/replay, Hosting bridge recovery and typed application snapshots/receipts. Fixing only the record decoder is expressly insufficient. No direct provider-port signature changes: their enclosing record names remain the same, with nested representation refined. Typed public/application contracts and the internal typed child receipt remain typed.

## Exact API and format evidence

DagRunBindingV1.Fingerprint changes DefinitionFingerprint -> string;
DagRunValueV1.Fingerprint changes PayloadFingerprint -> string. Each constructor,
init property and generated Deconstruct parameter changes, with non-nullability
preserved: six old proposed keys replaced by six new keys, other 359 unchanged.
The replacement catalog retains the pre-RC-3 baseline convention:

| Assembly | Original comparison baseline | Proposed full extraction | Added / removed |
|---|---|---|---|
| OrcaCore | ef13f660beecb09c88c0d22b09abae7b69332158d8fcea4b7da69ad5e0a32678 | a8bb583860fd8744b018273911fe65b1c899926d70e501efb773ccfbf5588f3e | 7 / 0 |
| Runtime.Protocol | ed710c565b5e39186b1569949f9a8eb4eea201f215fd9b97cb5961c5c85be5d1 | 703536ec79dd945894ec58ac9593a90cf3b81dca13c5013e12f503e18a223a50 | 193 / 0 |
| Provider.Abstractions | d9549da7045c7bfc7ef2e56d56e5c6f12d422ccd10107bfe0edcfa06d057820e | 225f3481c5cb3f8dcdbe98cad7629d80e9c472e97f262c516cdbcb4bbb28a579 | 165 / 0 |

Total 365; the seven OrcaCore keys already shipped at c93411e, not new work here.
The separate metadata prototype compiles all three owning assemblies and compares
type/line keys from the existing exact API formatter, including generated members.
No shipped public baseline is refreshed. The formatter's FormatAssembly and its
formatting helpers match current guard source exactly. Catalog extraction is
signature evidence, NOT storage or runtime-behavior implementation.

For these TWO fields only, require exactly 64 uppercase ASCII hex digits, reject
null/empty/whitespace/prefixes/wrong length/lowercase/mixed-case/non-hex without
normalization. Existing lowercase operation/checkpoint/type-shape hashes keep
their own rules. Shape does not establish authenticity: compare installed exact
definition identity/version/fingerprint ordinally and hash exact stored payload
bytes before constructing any trusted typed projection. Present-null bytes and
their real hash stay present. Corruption/binding faults are not mapper failures.
Typed construction stays inside existing Dag/Engine.Durable friends; Hosting
reaches Engine.Durable through the existing edge. No ad-hoc new seam is approved.

### Reproducible standalone proof

Compile the two dated C# artifacts together in a fresh ignored executable project,
TargetFramework net10.0, Nullable/ImplicitUsings enabled, AssemblyName exactly
OrcaCore.Runtime.Protocol and EnableDefaultCompileItems=false. Reference ONLY
src/OrcaCore.Abstractions/bin/Release/net10.0/OrcaCore.dll by HintPath; no project
reference, changed OrcaCore assembly, test friend, copied constructor, converter
hook or System.Reflection/UnsafeAccessor is needed. Place the project inside
this repository or copy its global.json/build properties first.

Normal Release -warnaserror builds/runs 0/0 and reconstructs exact text through
three SIMULATED boundaries, rejecting twenty malformed field cases and one
well-formed byte/hash mismatch. Those labels are not actual providers. Building
the same project with -p:DefineConstants=FORBIDDEN_FINGERPRINT_CONSTRUCTORS must
fail with CS1729 for BOTH constructors. Normal mode is then restored and green.
This proves the representation can be reconstructed without friend access, not
that the full eight-kind codec/checkpoint/reducer or durable providers exist.

## Normative scope and preserved history

The addendum's crosswalk records every affected numbered/canonical owner and
explicit exclusions, including doc 11. No canonical or numbered source names
the two storage-record types; general typed streams/ports and opaque public
fingerprint types do not require strong values at these storage DTO fields.
Both active delta files contain no contrary field-type rule. Therefore no
canonical sync, delta edit, fifth owner, Task 7.3 row/digest change or Task 8.0
map change is proposed. No guide or dated decision is rewritten. The known stale
CLAUDE status sentence remains an explicitly deferred source finding.

RC-3 contract f8a1a7f6, catalog 766ad5d8, signatures 8301094b, its three rejected
packets, the RC-3 APPROVE b7c9d54d and the transition addendum remain byte-exact.
The new registry entry binds its real e6802a3/d367ced6/578593e approval chain and
all four new artifacts by source-pinned hashes. The same actual-Git tree,
single-parent/direct-child, verdict addition, ancestry, bytes and unique-final-
APPROVE validator is applied to both the transition and amended-contract chains.
The fingerprint entry itself is Proposed with null approval; existing four
ApprovedPending rows, four supersessions and all prior ownership history remain.

Canonical provenance is unchanged: 184 rows / 48,504 B /
fd6ad0449e706d5ecf3f9dab552a9ad5fc3ef1be95df13304afba91e1d9ca8c8;
173 synchronized, eight superseded, three bootstrap, zero pending. Semantic
approval eligibility is synchronization eligibility, not approval of this new
contract. Fourteen canonical files remain source-pinned at cee7cd37; preambles,
capability inventories and the fourteen-entry superseded catalog are unchanged.

## Validation and controls

Full Debug/Release non-incremental -warnaserror builds 0/0; twelve packages;
Core 354, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, Certification 100;
PostgreSQL 103, SQL Server 74, Integration 11, zero skipped. Infrastructure stays
247 (no Fact/Theory added), expected-red exactly fourteen, total 261; strict
OpenSpec 21/21. Existing package, source provenance and exact API checks pass.
Eight green package fixtures pass, dag-hosting remains the sole expected red.
Final frozen, disposable committed and cleared-freeze evidence states are checked
separately and reported in the handoff. No activation rehearsal is applicable.

Nine mutation controls run only in an owned disposable clone, with root/HEAD
assertions and byte-exact restoration. Each targeted focused guard is RED:

| Control | Intended detector |
|---|---|
| Change addendum validation wording | Persistent artifact hash, even without active freeze |
| Change it and coherently refresh fixture hash | Registry digest terminates in reviewed source |
| Forge Approved/evidence state | Proposed registry pin |
| Restore one typed signature, coherently refresh fixture AND registry source digest | Exact two-parameter substitution comparator |
| Add a seventh API key with coherent fixture/source refresh | Independent six-key count/comparison |
| Edit old RC-3 contract | Immutable predecessor hash |
| Add Runtime.Protocol friend in source | Contract-only source boundary |
| Edit unrelated canonical text | No-sync boundary (existing canonical pin also remains) |
| Cite real activation d59f830 as RC-3 evidence, fixture/source coherent | Actual Git parent check |

Control and restored copies are green. A forbidden-constructor compile error is
separate from a guard failure and is reported as an expected negative proof.
No guard control relies on the temporary active freeze. ReSharper 2026.2.3.1
inspects only the two changed review-guard files after the build, with solution
analysis enabled and no cleanup. Its SARIF counts/scope and all commands/logs are
retained in ignored .codex-run; no exit-code-only clean-report claim is made.
The first disposable-clone safety check stopped because Git resolved R: to its
physical owned path; the assertion was corrected after verifying that path.
It stopped before probes/commits; no main action or data loss occurred.

## Freeze recipes and approval sequence

Base HEAD/tree above; staged paths zero. Manifest is raw git status
--porcelain=v1 --untracked-files=all --no-renames, exact Git order, LF rows and one
final LF. Compare the set to commit-real diff+untracked paths using a COPIED
index; every staged blob must equal its raw file bytes. Fourteen real paths,
not status-cache phantoms. A copied-index tree does not authorize main commit.

Semantic record excludes docs/review/** and ONLY immutable-document-history.json
and review-manifest-provenance.json. Render path<TAB>RAW bytes<TAB>RAW SHA-256,
ordinal-sort full rows, UTF-8 without BOM, LF and one final LF:
10 rows / 1575 B / d2e08b5d32498f548056d466725104bf6ef901f19527596aedaeb31925f944f1.

Active record excludes ONLY review-manifest-provenance.json; render
XY<TAB>path<TAB>RAW bytes<TAB>RAW SHA-256, same ordering/encoding. It includes this
request, so its bytes/digest are in activeFreeze and the final handoff, not here.
Manifest bytes/SHA, NUL porcelain, all-file record and exact simulated tree are
likewise in the handoff, avoiding recursive self-hashes.

Write one immutable dated APPROVE or REJECT verdict for this exact amendment
freeze; do not edit this request/manifest or prior records. Only after APPROVE:
checkpoint exactly these fourteen paths/tree on this base; the only direct child
adds/catalogs that byte-exact verdict and clears activeFreeze while preserving
the history pointer. There is NO activation or task checkbox change. The next
independently reviewed Task 2.1 source target must bind this amendment's actual
checkpoint/tree/evidence/verdict and artifacts before it implements the revised
representation; source is not authorized by this contract request alone.
