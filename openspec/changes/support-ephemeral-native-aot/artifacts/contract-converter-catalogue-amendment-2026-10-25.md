# Issue #13 minimal converter catalogue amendment — 2026-10-25

Status: candidate for explicit approval. Preparation and a separate self-review
do not authorize implementation. Task 2 stays uncommitted, task 2.2 stays open,
and the product converter remains internal until the user approves this exact
amendment. This record supplements the approved R1 contract; it does not replace
its immutable verdict, consumer protocol, owner routing or historical evidence.

## Reason and exact scope

The ordinary external Metadata context for a DTO containing EventName fails with
SYSLIB1220 and SYSLIB1030 under SDK 10.0.301. Its JsonConverterAttribute names the
internal StrongStringValueJsonConverterFactory. A separate minimal consumer that
never calls WorkflowJsonMetadata reproduces the errors. Resolving metadata at
runtime cannot fix this compile failure. The task-2.2 review records it as F1.
This is an OrcaCore-attributable supported-path diagnostic, not permission to
suppress a warning, omit the DTO root or use a private bridge.

Expose the existing sealed product factory in its current assembly/package
OrcaCore and namespace. The exact additional catalogue, in declaration notation,
is:

```csharp
namespace OrcaCore.Abstractions.Ids;

public sealed class StrongStringValueJsonConverterFactory
    : System.Text.Json.Serialization.JsonConverterFactory
{
    public StrongStringValueJsonConverterFactory();
    public override bool CanConvert(System.Type typeToConvert);
    public override System.Text.Json.Serialization.JsonConverter CreateConverter(
        System.Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options);
}
```

Together with the unchanged OrcaCore.WorkflowJsonMetadata static class and its
Register<TContext>() method, this is the entire additive public catalogue: two
types and four declared members (the metadata method, factory constructor and
two overrides). Inherited framework members are not new OrcaCore declarations.
The factory stays sealed, with its existing base type and nonnullable parameter/
return signatures. No new attribute, overload, generic parameter, interface,
explicit constructor body or other public member is authorized.

The later product diff for this factory SHALL consist solely of changing
`internal sealed class` to `public sealed class`. Its existing implicit
parameterless constructor consequently becomes public; the already-public
overrides become reachable. Do not rewrite their bodies or expose the private
StringValueJsonConverter<T> implementation. Keep every existing public strong
type's JsonConverterAttribute byte-identical. The current factory and strong
type files are both byte-equal to HEAD 10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd:

- StrongStringValueJsonConverterFactory.cs SHA-256:
  `13f5085f2f7bdc3d82e4e7bf38b9857b6aabd100f02a9ae58d58c07a2d14ca4c`.
- StrongValues.cs SHA-256:
  `d078dea988e033b6d6ce8b81e5a405c916e303f52b19b6ddf7655a769eff39c1`.

## Closed behavior to preserve

The exact table remains:

| Public type (namespace OrcaCore) | Existing string parser |
|---|---|
| EventName | Create |
| WorkflowOutcomeName | Create |
| AuthoredBranchId | Create |
| ResourcePoolName | Create |
| TransientPoolName | Create |
| StartIdempotencyKey | Create |
| StopConfirmationId | Create |
| ResourcePoolOperationId | Create |
| ResourceGovernancePartitionId | Create |
| StepOperationId | Parse |
| LeaseProtectionToken | Parse |

CanConvert SHALL recognize exactly these eleven CLR types. An unsupported
non-null type such as System.String SHALL return false; direct CreateConverter
for it SHALL keep the existing NotSupportedException. Each returned converter
SHALL retain the existing Read/GetString/parser and WriteStringValue/Value
behavior, including escaping, validation and the existing reference-value null
handling. This amendment adds no null-handling policy, dictionary-key support,
new supported shape, new converter registration or mutable configuration.

The framework generator may construct the attributed factory from an external
assembly. The consumer does not install it in context settings. Constructing or
using it directly SHALL NOT configure/seal metadata, capture/dispose a provider,
admit work, run workflow code or affect host ownership. Product converter
priority, fixed orcacore-json-v1 bytes/options/limits/detachment, legacy JIT
behavior and input/definition fingerprints remain unchanged. No context identity
or factory instance becomes part of a fingerprint.

Explicit application converter settings remain prohibited, including a context
Converters entry naming this product factory. Register SHALL reject that
configuration with NotSupportedException identifying the context and unsupported
policy, publish nothing, leave configuration retryable before first use and run
no workflow work. The existing product type attribute is not an application
converter option. A public sealed factory provides no replacement-codec hook.
Other R1 configuration negatives, process seal/race semantics, F1 declared-shape
rule, diagnostics and DI ownership remain unchanged.

## Supplemental acceptance cases

These are additional subcases of existing NS groups, not replacements for any
R1 case or AOT-01–16 assertion. After explicit approval, task 2.2 adds local
behavior-first public-consumer controls; task 2.4 carries them into every one of
the six native cells and the separate unconfigured legacy JIT control.

| Case | Required proof |
|---|---|
| NS-02d | A real generated context explicitly listing this factory as a converter is rejected by Register before publication/seal/work. A normal default generated context then registers successfully before first use. Ordinary attributed product types do not trigger this rejection. The legacy control checks unchanged default attributed behavior; it does not call Register. |
| NS-06g | Ordinary external Metadata and Default contexts, public packages only, compile without SYSLIB1220/SYSLIB1030 or any other unclassified supported-path warning. Cover all eleven table entries as roots, nested DTO members and supported collection elements; retain the original EventName-containing golden DTO. Round-trip exact quoted-string bytes and parsed values, including the existing escaping/null/invalid-string corpus, and compare input/definition fingerprints with legacy JIT. Include the fixed literal `catalogue-value`, encoded as the JSON string `"catalogue-value"`; do not derive every golden expectation from the new converter itself. Default and Metadata generation modes both compile and execute their corresponding positive closures in each native cell. |
| NS-06h | Through only the public factory API, assert its sealed type, accessible parameterless constructor, eleven supported CanConvert results, false for System.String and the unchanged NotSupportedException from direct CreateConverter for System.String. Direct construction/conversion before metadata setup has no seal/registration side effect: subsequent valid metadata registration succeeds in a fresh process. Preserve positive conversion behavior for all eleven table entries in the same process. |

Trimmed/AOT cells continue to assert JSON reflection is disabled. No suppression,
application converter/resolver workaround, private root, private bridge,
reflection switch, root omission or new Unsupported result satisfies these
cases. Classify all newly reached generator/trim/AOT warnings individually. The
eleven-type factory table does not extend the native promise to durable/DAG/
providers, all public OrcaCore paths or twelve packages. This is a catalogue
correction, not evidence that any native cell now passes.

## Owner transitions after approval

This amendment's pending planning edits change only the current proposal,
design, tasks and three delta files; requirement identities/counts stay at
2/2/1/2. The structured-fiber-execution delta is unchanged. Canonical text and
the previously reviewed task-2.1 transition remain at the R1 catalogue until
explicit amendment approval. The resulting temporary delta/canonical difference
is intentional and is not an approved canonical/source target.

After explicit approval, task 2.2 owns these transitions within the same
uncommitted task-2 target:

| Owner | Required transition |
|---|---|
| Current planning and this amendment | Record explicit approval and exact amendment/verdict identity without rewriting any earlier dated verdict/protocol. Do not invent a commit binding while task 2 is uncommitted. |
| openspec/specs/developer-facing-surface, workflow-contracts and quality-and-verification | Apply exactly the corresponding amended delta blocks to the R1 additions; retain every prior canonical line outside those blocks and unchanged structured-fiber-execution text. |
| docs/specs/17-public-authoring-contract.cs; 17-selected-mode-capability-matrix.md | Add the exact factory type/constructor/two overrides alongside the unchanged metadata API; state the product-attribution purpose and keep existing retention/shutdown entries. |
| docs/specs/03, 10, 11, 12, 13; docs/normative-source-map.md | Reconcile catalogue references and AC-564/568 supplemental NS-02d/NS-06g–h coverage within AC-563–574, ownership/status and fixed-codec boundaries. No new acceptance IDs or native credit from planning alone. |
| README.md; docs/ephemeral-engine-developer-guide.md; docs/packages.md | Explain normal generated contexts and the existing product attribution; never instruct adding the factory to context options. Retain pending implementation/native status and old released-version facts. Inspect all other existing R1 routed owners for conflicting one-type/one-method catalogue wording. |
| src/OrcaCore.Abstractions/Ids/StrongStringValueJsonConverterFactory.cs | Only the specified accessibility token change after approval; retain the source hash check with that sole substitution. StrongValues.cs and the converter bodies stay byte-identical. |
| PublicApi/v1/OrcaCore.api.txt; Fixtures/v1-public-contract.json; CompileFixtures/ExactAuthoring; PackageFixtures/PrimaryPackage; metadata subprocess/native consumer | Capture the exact compiled factory type/constructor/two overrides alongside the real source; no baseline-only advance. Compile and execute the ordinary external strong-value DTO, all supplemental controls and the existing failure/size corpus without weakened assertions. |
| NativeSupportTransitionGuards and inherited exact source/API guards; registry; current provenance/inventory owners | Bind the explicit amendment approval, validate the exact R1-plus-amendment composition, retain unchanged predecessor artifacts and recompute current counts/hashes/pins from the approved predecessor baseline. Do not drop the exact-delta check or add a broad source exemption. Source/native/whole-target approval remains unset until the corresponding reviewed gate. |

R1 owner routing's one-type/one-method restriction is superseded only by this
exact catalogue and only after approval. All other routing remains in force.
Task 2.1's checked box records the original R1 transition, not completion of this
later correction. Task 2.2 and all later boxes stay open.

## Review and implementation boundary

Review this planning-only amendment against the already approved R1 contract and
the actual unchanged source. Do not mistake OpenSpec validation or a favorable
self-review for user approval, executable proof or permission to commit task 2.
The pending task-2.2 F2 nested-failure corpus remains unresolved; this amendment
does not waive it, weaken any assertion or authorize task 2.3. Once the amendment
is approved, resume task 2.2, run matching API/compile/package tests and ReSharper,
and independently review a green slice before advancing. The coherent task-2
checkpoint and native support claim still require the later whole-target gate.
