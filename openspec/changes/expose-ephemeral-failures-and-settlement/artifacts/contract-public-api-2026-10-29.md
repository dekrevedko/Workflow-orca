# Issue #16 exact proposed public additions

Base: 0a8e375. PackageId/assembly and namespace: OrcaCore. Exactly two members,
inside existing types; these fragments are not a standalone compilation unit.

```csharp
namespace OrcaCore;

public sealed class WorkflowFailure
{
    public string? ExceptionType { get; }
}

public class WorkflowInstanceHandle
{
    public ValueTask WaitForSettlementAsync(
        CancellationToken cancellationToken = default);
}
```

WorkflowInstanceHandle<TOutput> inherits the method; no redeclaration/override.
Both existing types keep all previous members/accessibility. No new public type,
setter, constructor, factory, extension, overload or registration option.
WorkflowOutputUnavailableException gains no public member: its existing inherited
Exception.InnerException carries the original ephemeral business exception.
Noncancelled durable settlement calls throw System.NotSupportedException with no I/O.
Pre-cancelled tokens throw local OperationCanceledException first.

The user selected: 'Propose an explicit ExceptionType property (recommended)'.
The follow-up 'May be better is to use InnerException?' was answered explicitly:
InnerException carries the original object, while ExceptionType is only detached
text for snapshots/serialization. Both purposes are kept separate; this is design
direction, not source/canonical or checkpoint approval.

Runtime/public API/compile/package baselines and the numbered declaration companion
remain byte-exact in this contract. The companion changes in the approved
canonical transition; executable baselines change with actual source later.
