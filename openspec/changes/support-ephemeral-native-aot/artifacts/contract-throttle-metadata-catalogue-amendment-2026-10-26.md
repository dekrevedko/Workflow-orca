# Issue #13 minimal throttle metadata catalogue amendment

Status: proposed; no public annotation or API baseline change is applied.

The task 2.3 audit found a public bounded-reflection boundary absent from the
old 90-site inventory: `StepExecutionThrottle.For<TStep>` calls
`typeof(TStep).GetInterfaces()` to enforce the existing IStep<TState> rule.
Native publish reports IL2090 because the generic parameter has no interface
metadata requirement. A passing concrete run does not close that analyzer proof.

Proposed catalogue addition, and only addition:

```csharp
namespace OrcaCore.Hosting;
public sealed class StepExecutionThrottle
{
    public static StepExecutionThrottle For<
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.Interfaces)]
        TStep>(int maxConcurrency);
}
```

This annotates the existing generic parameter. It adds no type, overload,
constraint, member, runtime option, package dependency, public bridge, rooting
descriptor or warning suppression. It does not change the implementation body:
capacity is validated first, then the exact closed step's implemented interfaces;
invalid types retain ArgumentException with parameter TStep, invalid capacity
retains ArgumentOutOfRangeException with parameter maxConcurrency, and exact
step identity plus all throttle ownership/physical-slot rules remain unchanged.

Why this boundary needs metadata: the API intentionally accepts TStep without
an IStep<TState> constraint because it has no TState parameter. Its validation
must still reject non-step and open/unrelated types; testing assignability to
one arbitrary IStep<object> would change semantics. New marker interfaces or
public constraints would be a larger catalogue change. A private forwarding
method cannot satisfy a metadata requirement that originates at public TStep.

After explicit approval only: synchronize the exact catalogue owners, apply
only this generic-parameter attribute, capture the matching API baseline and
source pins, exercise valid named steps (including inherited IStep contracts)
and invalid types/capacities in JIT, full trim and Native AOT, and require IL2090
at this boundary to disappear without suppressions. Keep the existing ownership,
timeout, cancellation and host-throttle tests unchanged. Re-run ReSharper,
required green lanes and a separate self-review before task 2.3 is completed.
Task 2.4 native-host qualification remains separate; this amendment makes no
support claim and is not implementation approval.
