# Issue #13 task 2.2 converter catalogue conflict — 2026-10-24

Proposal for review only; not an approved contract amendment. No converter was
made public and no earlier contract/delta/canonical text was changed by this record.

## Observed blocker

The ordinary Metadata context for a public DTO containing EventName does not
compile with the repository's warnings-as-errors settings. .NET SDK 10.0.301 /
System.Text.Json source generation reports SYSLIB1220 and SYSLIB1030: EventName's
JsonConverterAttribute names internal StrongStringValueJsonConverterFactory.
The generator cannot reference/construct that type from an external assembly.
The same factory is attributed on eleven public strong string values.

This is an OrcaCore-attributable supported-path diagnostic, so the approved
contract cannot dismiss it as a toolchain warning or suppress the category.
Providing runtime metadata does not repair a compiler failure. The golden input
corpus exposes this independently of the implementation's runtime registration.
Raw compiler output is retained under ignored
.artifacts/publishing-validation/issue-13-task-2-2/golden-controls.log.

The approved catalogue permits only WorkflowJsonMetadata.Register<TContext>().
Design decision 1 requires a reviewed amendment before any extra public type,
member or annotation. Removing JsonConverterAttribute would change existing
external JSON behavior/public attributes; adding an application converter,
replacement resolver or blanket warning suppression is also outside the contract.

## Recommended minimal amendment

Expose the existing sealed product converter factory in its current namespace,
with its existing behavior unchanged. Exact additional catalogue:

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

This requires only the existing class's accessibility change in product code;
its already-present overrides and implicit constructor become public surface.
The final baseline must capture their exact attributes as well. The class stays
sealed and its closed product-type switch stays fixed. It is generator interop,
not an application codec/options hook. Metadata registration still rejects app
converter settings. No new package, dependency, friend, role option/fingerprint,
wire format, converter body, private root or support promise is proposed.

A reviewed amendment must update the current planning/canonical/numbered/API
catalogue owners coherently and retain earlier dated approvals unchanged. Then
resume the same uncommitted task-2 target, compile the ordinary consumer without
these warnings, and prove its strong-value golden bytes and fingerprints.

## Current implementation/evidence disposition

The 23 subprocess cases before this corpus expansion passed, including all nine
R1 subcase groups, process seal/races, ownership and F1 controls. The new golden
fixture is intentionally retained; its compile failure currently cascades into
the whole process suite and is not recorded as 25 behavior failures. Task 2.2 stays
open, source/native approval remains null, and no source checkpoint or next-task
advance is authorized by this proposal. Product source and matching baselines
remain uncommitted. Scoped ReSharper and separate review of this partial slice
are recorded separately; no native compatibility is claimed.
