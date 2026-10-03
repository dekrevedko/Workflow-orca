## MODIFIED Requirements

### Requirement: Dependency direction remains one-way
The allowed direct OrcaCore package references SHALL be exhaustive: `OrcaCore.Core -> OrcaCore`; `OrcaCore.Engine.Ephemeral -> OrcaCore + OrcaCore.Core`; `OrcaCore.Runtime.Protocol -> OrcaCore`; `OrcaCore.Provider.Abstractions -> OrcaCore + OrcaCore.Runtime.Protocol`; `OrcaCore.Engine.Durable -> OrcaCore + OrcaCore.Core + OrcaCore.Runtime.Protocol + OrcaCore.Provider.Abstractions`; `OrcaCore.Durable.Hosting -> OrcaCore + OrcaCore.Engine.Durable + OrcaCore.Provider.Abstractions`; each provider adapter -> `OrcaCore + OrcaCore.Runtime.Protocol + OrcaCore.Provider.Abstractions`; `OrcaCore.Dag -> OrcaCore`; and `OrcaCore.Dag.Hosting -> OrcaCore.Dag + OrcaCore.Durable.Hosting`. Product friends SHALL be exactly `OrcaCore -> OrcaCore.Core`, `OrcaCore -> OrcaCore.Engine.Ephemeral`, `OrcaCore -> OrcaCore.Engine.Durable`, `OrcaCore -> OrcaCore.Dag`, `OrcaCore.Core -> OrcaCore.Engine.Ephemeral`, `OrcaCore.Core -> OrcaCore.Engine.Durable`, `OrcaCore.Engine.Durable -> OrcaCore.Durable.Hosting`, `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting`, and `OrcaCore.Dag -> OrcaCore.Dag.Hosting`. The four application-owned grants provide compile-checked internal authoring/runtime contracts without creating reverse package references; the `OrcaCore -> OrcaCore.Dag` authoring grant is limited by compiled-metadata evidence to the exact authoring constructors and canonical hash operation named in the `developer-facing-surface` requirement. Owning white-box test friends SHALL be exactly `OrcaCore.Core -> OrcaCore.Core.Tests`, `OrcaCore.Engine.Ephemeral -> OrcaCore.Engine.Ephemeral.Tests`, `OrcaCore.Engine.Durable -> OrcaCore.Engine.Durable.Tests`, `OrcaCore.Durable.Hosting -> OrcaCore.Hosting.Tests`, `OrcaCore.Providers.PostgreSql -> OrcaCore.Providers.PostgreSql.Tests`, and `OrcaCore.Providers.SqlServer -> OrcaCore.Providers.SqlServer.Tests`; the sole cross-package test friend SHALL remain `OrcaCore.Engine.Durable -> OrcaCore.ProviderCertification` for deterministic resource-governance barrier certification. `OrcaCore.Dag.Hosting` SHALL be the only DAG-to-durable product bridge and SHALL consume one named versioned internal child-start/join contract through its exact friend edge, never a public child API. No package SHALL reference an engine implementation except the owning hosting package, no OrcaCore package SHALL reference a companion integration, and acceptance, behavior-scenario, compile-fixture, integration, provider-certification (apart from the named barrier edge), and other test assemblies SHALL receive no friend access. Architecture checks SHALL reject every other edge or friend assembly.

The `OrcaCore.Dag -> OrcaCore.Dag.Hosting` runtime-view product friend SHALL be limited by exact type-and-member metadata evidence to `DagRuntimeView<TRunInput>`, `DagRuntimeNodeDescriptor`, `DagMappedInputResult`, and only the fifteen internal method/getter signatures enumerated below. It SHALL NOT authorize their constructors, raw delegates, mutable drafts, or any additional internal type/member/overload. It SHALL NOT add a direct package reference, a test friend, a Core-to-DAG edge, or codec access to Dag. `OrcaCore.Dag.Hosting -> OrcaCore.Dag + OrcaCore.Durable.Hosting` SHALL remain its complete direct OrcaCore dependency set. The existing `OrcaCore.Durable.Hosting -> OrcaCore.Dag.Hosting` durable bridge SHALL decode committed dependency outputs before evaluation and own fixed-codec normalization, committed bytes and child-start/join; no child-start access SHALL cross the `OrcaCore -> OrcaCore.Dag` authoring friend. Dag.Hosting-level behavior SHALL verify the runtime view without additional assembly friendship.

The exhaustive proposed signature contract SHALL be exactly the following, with no additional type or member access:

```csharp
// On the existing public WorkflowDagPlan<TRunInput>:
internal DagRuntimeView<TRunInput> GetRuntimeView();

internal sealed class DagRuntimeView<TRunInput>
{
    internal IReadOnlyList<DagRuntimeNodeDescriptor> Nodes { get; }
    internal DagMappedInputResult EvaluateMapping(
        DagNodeRef node,
        TRunInput immutableRunInput,
        IReadOnlyDictionary<DagNodeRef, object?> successfulDirectDependencyOutputs);
}

internal sealed class DagRuntimeNodeDescriptor
{
    internal DagNodeRef Reference { get; }
    internal int AuthoredOrdinal { get; }
    internal DefinitionId ChildDefinitionId { get; }
    internal DefinitionVersion ChildDefinitionVersion { get; }
    internal DefinitionFingerprint ChildFingerprint { get; }
    internal Type InputType { get; }
    internal Type? OutputType { get; }
    internal IReadOnlyList<DagNodeRef> Dependencies { get; }
}

internal sealed class DagMappedInputResult
{
    internal bool IsValid { get; }
    internal object? Input { get; }
    internal Type? InputType { get; }
    internal string? FailureCode { get; }
}
```

Mapper exceptions derived from `Exception`, including mapper-thrown `OperationCanceledException`, SHALL become `DAG_INPUT_MAPPING_INVALID`, except `OutOfMemoryException`, `StackOverflowException`, and `AccessViolationException`, which SHALL NOT be converted. Host/run cancellation observed outside mapper evaluation SHALL retain the runtime cancellation outcome. Before invoking the evaluator, an ordinary declared-type output decode/materialization failure SHALL fail the node as `DAG_INPUT_MAPPING_INVALID`, without invoking its mapper, committing input, or starting a child; protocol-integrity or storage failures SHALL remain runtime failures and SHALL NOT be disguised as mapper failures.

#### Scenario: New project reference is added
- **WHEN** a contributor wires a new project dependency
- **THEN** verification rejects any direct edge absent from the exhaustive manifest, including reverse application, protocol-to-provider, provider-to-engine, core-to-advanced, DAG reverse, and companion-integration edges

#### Scenario: Provider author persists protocol facts
- **WHEN** a provider fixture references provider abstractions and their declared runtime-protocol dependency
- **THEN** it implements persistence ports without referencing either engine implementation

#### Scenario: Companion scheduler is in the solution
- **WHEN** scheduler code references Kubernetes or AWS SDK packages
- **THEN** those dependencies remain entirely in outward-dependent companion projects and are absent from OrcaCore package closures

#### Scenario: DAG host starts durable children
- **WHEN** `OrcaCore.Dag.Hosting` coordinates a DAG node child
- **THEN** it uses the single versioned friend bridge from `OrcaCore.Durable.Hosting` and no application, provider, or additional friend package gains child-management access

#### Scenario: DAG package graph and friend metadata are inspected
- **WHEN** compiled product metadata and packed dependencies are compared with the exact package/friend manifest
- **THEN** `OrcaCore.Dag -> OrcaCore` remains the sole DAG authoring dependency, the existing `OrcaCore -> OrcaCore.Dag` authoring friend retains its six-signature allowlist, the separately named `OrcaCore.Dag -> OrcaCore.Dag.Hosting` runtime-view friend is the only additional proposed product grant, and the DAG authoring assembly references no unapproved internal application or child-runtime member

#### Scenario: Host consumes internal DAG runtime structure
- **WHEN** the runtime adapter evaluates a ready node from a validated DAG plan
- **THEN** only the exact Dag-to-Dag.Hosting runtime-view types and members are referenced, all existing direct package edges stay unchanged, and the existing durable bridge owns codec and child-start work
