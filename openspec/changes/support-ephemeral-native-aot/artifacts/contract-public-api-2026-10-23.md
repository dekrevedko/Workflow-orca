# Issue #13 exact public catalogue

Pending independent review. No source declaration or executable baseline is changed.

Assembly `OrcaCore`, package `OrcaCore`, namespace `OrcaCore`:

```csharp
public static class WorkflowJsonMetadata
{
    public static void Register<TContext>()
        where TContext : System.Text.Json.Serialization.JsonSerializerContext, new();
}
```

One new public type and one static void generic method; no constructor, overload,
property, return handle, public interface, generated package, reset or unregister.
No existing public signature, role option/fingerprint, exception/result union,
dependency or friend changes. No public trim/AOT annotation or compatibility badge
is implicitly approved. API/compile/package baselines and declaration companion
change with their real source in the later reviewed target.

The full normative semantics are the two developer-facing-surface additions.
Configuration is process-wide, one context type, atomic and sealed by first use.
Same-type registration is an idempotent no-op with no reconstruction; different
type or registration after a legacy seal throws InvalidOperationException before
mutation. A constructor/validation error installs nothing; its original cause is
preserved and startup can retry before use. Reentrancy fails explicitly. The
context has no DI/provider/scope ownership, no disposal contract and no app
converter/resolver/options hook. Missing contract metadata uses SFE-TYPE-003 at
Build/TryBuild or NotSupportedException at direct codec boundaries. Existing
unsupported-shape errors remain SFE-TYPE-002. No added exception type is needed.

Required native recipe (illustrative future API; not executable current source):

```csharp
var services = new ServiceCollection();
services.AddOrcaCoreEphemeralEngine(hostOptions); // Existing validated host options.
WorkflowJsonMetadata.Register<AppJsonContext>();
// Define workflows only after static Core installation and metadata registration.
// Build the provider, resolve existing public roles, register/start, drain/stop.
```

The application context uses the .NET source generator in Metadata or Default
mode and declares the finite closure. It cannot select a different wire format.
All modules combine their roots into one context before first use; later modules
cannot replace/extend the context. Existing unconfigured JIT applications keep
their current behavior. Existing DI registration and Completed ownership rules
remain authoritative. A runtime options/binder setting cannot replace this call.
If a required type is missing after sealing, correct the generated context and
restart the process; a late registration cannot repair a live sealed registry.
