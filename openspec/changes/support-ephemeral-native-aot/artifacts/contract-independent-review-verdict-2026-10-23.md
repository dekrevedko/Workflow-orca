# Issue #13 frozen contract review — 2026-10-23

## Verdict: REJECT

One P2 contract finding needs resolution before checkpointing or source implementation.
No P1 finding. The public API shape, process-wide ownership choice, static-bootstrap
approach, native matrix and mechanical provenance changes are otherwise sound in
substance. This review pass did not alter any of the twenty frozen files.

## Target and independent checks

- Base/HEAD: `3b7f98aa0748a4d9c3f54d4604e566ed61f21bad`.
- Branch: `codex/issue-13-native-aot-trimming`.
- Frozen candidate tree: `c0fe638a0143421f6470bd207e65966aa0d1698a`.
- Manifest: `artifacts/contract-manifest-2026-10-23.txt`, twenty paths.
- Content SHA-256: `6e3da60d91cc50b3497d0c65c5a99aa10697d99148b866f414217c61922050d0`.
- Porcelain SHA-256 before this excluded verdict: `19d1015d802ed5c03d7705e733654be74f59312c141b1a7e5bb810a6006c913a`.

I independently checked every raw file hash against the freeze, each staged-tree
blob against its worktree input, the complete candidate-to-parent path set and
whitespace, both status hashes and an empty author index. I recomputed 206
provenance rows/54,112 bytes, all seven pending additions and the unchanged 199-row
base subset, current artifact hash and all 21 unchanged predecessor artifacts.
OpenSpec strict was re-run: **25/25**.

I inspected the source and canonical/numbered owners for the codec, bootstrap,
internal friend grants and original assessment. Product source remains unchanged.
I did not re-run the full Release build or Infrastructure lane for this text-only
review. The author's retained exact-target TRX says **259/259**, and its build
receipt says **0 warnings/0 errors**; these remain author results, not independent
re-runs. No new native OS/AOT qualification is claimed here.

## F1 — P2: the .NET 10 metadata strategy changes an allowed round trip

Locations: `design.md:27–29`, `design.md:57–59`,
`specs/workflow-contracts/spec.md:4–8`, and the protocol's NS-06 row at line 43.

The contract admits public source-generatable DTOs with the existing fixed-codec
shape/rejection rules and promises identical detached values in legacy JIT and
Generated mode. Ordinary .NET 10 metadata generation already breaks that promise
for an init-only property whose initializer is nondefault and whose default value
is omitted:

```csharp
public sealed class InitState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int Count { get; init; } = 42;
}
// Submit new InitState { Count = 0 }.
```

Using SDK 10.0.402/runtime 10.0.13 and a normal Metadata-mode generated context,
I requested generated type metadata against fresh default engine-style options:

| Path | Encoded JSON | Detached Count |
|---|---|---:|
| Reflection / current codec defaults | `{}` | 42 |
| .NET source-generated metadata | `{}` | 0 |

The generated constructor is `new InitState() { Count = (int)args[0] }`; when the
member is absent, its default argument overwrites the initializer. The existing
codec accepts this shape: it rejects JsonIgnore.Always properties from its graph,
but accepts WhenWritingDefault and a public int init property. No application
converter, context option, inaccessible member or missing metadata causes this.
Thus checking bytes/fingerprints alone misses a business-state difference.
Microsoft documents preservation of omitted init-only initializers as a
[.NET 11 source-generation change](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-11/libraries#source-generation-and-contract-metadata);
this issue explicitly targets .NET 10.

**Required contract fix:** choose and specify how this family of shapes is handled
before approving the strategy. Preserve the existing round-trip semantics with a
feasible static mechanism, or explicitly narrow the native/generated support
boundary with a deterministic early rejection and reconcile the parity/rejection
promises. Add this exact omitted-init case and a present-value control to the
required protocol. Do not silently change the fixed codec, update the golden
expectation to zero, or rely on a future .NET runtime. No source fix is authorized
by this review.

## Non-blocking note — P3: test baked metadata behavior, not options alone

The isolated probe also found that a generated context with
`UseStringEnumConverter=true` emits `"Ready"` where reflection emits `1`, even
with fresh default engine options and `context.Options.Converters.Count == 0`.
A handwritten JsonSerializerContext can likewise satisfy the generic constraints
and return renamed properties while presenting default options. These are
configuration-validation controls, not another demonstrated product bug: the
contract already requires rejection of custom contexts/behavior. Make both cases
explicit negatives in NS-02/08 so implementation cannot satisfy that rule by
checking only the options/converter list. Microsoft's
[source-generation guide](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation#blanket-policy)
documents the enum policy.

## Evidence, cleanup and authority

Compact probe source, generated InitState constructor, successful probe output,
OpenSpec log and independent hash/provenance checks are under ignored
`.artifacts/publishing-validation/native-support-contract/independent-review/`.
The first probe attempt failed because its harness omitted TypeInfoResolver; the
corrected successful probe and that initial log are both retained. It is a
framework experiment, not OrcaCore implementation or a failed product test.

The disposable E:/oc13r-json project and its bin/obj output are removed after
receipts are retained; cleanup.json records the observed result. This verdict is
excluded from the approved-request twenty-path manifest. Its addition changes the
working status only by this new file; the frozen content/tree stays unchanged.
HEAD and index remain at the uncommitted contract base. Nothing is pushed or
changed on GitHub. Resolve F1 in a new frozen contract revision and re-review that
diff; do not checkpoint this target or begin source implementation on this verdict.
