# Issue #13 R1 — F1 and P3 disposition (2026-10-23)

Rejected target: c0fe638a0143421f6470bd207e65966aa0d1698a, at unchanged base
3b7f98aa0748a4d9c3f54d4604e566ed61f21bad. The rejection verdict is preserved at
`contract-independent-review-verdict-2026-10-23.md`, raw SHA-256
`e555153f62a2b1bacaf73eb30a7f8d1df1228c6e15eba9116230917fd8785602`. This is a new contract
target, not approval or product implementation. All dated rejected artifacts are
preserved; active proposal/design/specs/tasks and mechanical pins are revised.

## F1 decision: explicit Generated-mode admission restriction

On .NET 10, reject a requested value closure with a JSON member whose configured
JsonPropertyInfo.AssociatedParameter.IsMemberInitializer is true AND whose
JsonIgnoreAttribute.Condition exists and is not Never. Reject the declared shape
for both omitted and present submitted values. Roots/nested/derived/collection
contracts obey the same rule. Do not reject unused excluded context roots.
Known Build/TryBuild uses SFE-TYPE-002 and existing exception/validation shapes;
direct boundaries use NotSupportedException, before effects/consumption. Name
the root, containing type/member, policy and admitted remedy. SFE-TYPE-003 remains
missing metadata, not this policy.

This deliberately narrows Generated-mode admission. It does not alter
orcacore-json-v1 bytes/options/fingerprints or unconfigured legacy JIT. Parity is
required on common admitted shapes; this is the sole added mode-specific
rejection exception. The original readonly record struct ValueState(int Number)
remains admitted because it has no omission policy. Mutable and genuine
constructor-bound controls stay admitted. No blanket init/record ban, changed
golden value or .NET 11 dependency is introduced. The public API is unchanged.

Classification uses metadata/member attributes, without model construction,
getters, initializer IL inspection or sample deserialization. The framework-only
R1 probe compiled with SDK 10.0.402/runtime 10.0.13 confirms the following facts
before constructing an application model; native classifier preservation must
still be proven in task 2, with no private roots or silent fallback.

| Shape | IsMemberInitializer | JsonIgnore | Excluded by F1 |
|---|---|---|---|
| InitState.Count | true | WhenWritingDefault | yes, even for present 7 |
| Original-shaped readonly positional value state | true | absent | no |
| MutableState.Count | absent | WhenWritingDefault | no |
| Genuine constructor-bound record Count | false | WhenWritingDefault | no |
| Always-ignored member with no associated parameter | absent | Always | no |

The underlying counterexample is retained: reflection and generated metadata both
encode submitted zero as `{}`, but return 42 and 0 respectively. The present-7
framework control returns 7 through both. The future OrcaCore test must reject
the affected type for both values; no implementation pass is claimed now.

The initializer flag and parameter association are public .NET 10 metadata:
[IsMemberInitializer](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.metadata.jsonparameterinfo.ismemberinitializer?view=net-10.0),
[AssociatedParameter](https://learn.microsoft.com/en-us/dotnet/api/system.text.json.serialization.metadata.jsonpropertyinfo.associatedparameter?view=net-10.0).

## P3 disposition: explicit configuration negatives

NS-02a requires rejection of Metadata context UseStringEnumConverter=true even
with default engine options and an empty Options.Converters list. NS-02b uses
the review's exact handwritten context that exposes default options but renames
Value to changed. Both must raise NotSupportedException naming the context/rule
before publication, install nothing, preserve retry and do no workflow work.
NS-02c verifies valid retry, original numeric enum/member encoding and no reliance
on generic constraints/options alone. Validate configuration/provenance and the
actual metadata; deliberately forged-code authentication is not in scope.

NS-06a–f cover the exact F1 root/nested omitted/present negatives, original value
state and mutable/constructor-bound positives, metadata-only classification and
unused-excluded-root control. NS-08 cross-references all those negative cases.
The twelve group IDs and AC-563–574 mapping remain; all subcases are mandatory
on both hosts in every Generated-mode flavor. Original sixteen scenario
assertions and six native cells remain mandatory, with no Unsupported completion.

## R1 artifact routing and gate

The R1 consumer protocol and owner-routing files supersede their rejected dated
versions. The original exact public API catalogue remains current: one type and
one method, no new package/friend/dependency/option/annotation. R1 provenance is
recomputed from the 199-row master baseline, retaining seven pending additions,
and catalogs the rejected current provenance artifact as an immutable predecessor.
The rejected verdict is included in the R1 target. The R1 approval verdict will
be excluded until its separately authorized evidence-only child.

Only contract/process files change. Source, canonical/numbered docs, baselines,
runtime tests/workflows and old approval records remain unchanged. Nothing is
committed, pushed, published or implemented. Green lanes support re-review only.
