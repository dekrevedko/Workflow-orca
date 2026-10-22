# Static source assessment, not publish evidence

Base `7895262a21f19741c0b5b7fbad30907bccff2cbb`; [issue #3](https://github.com/dekrevedko/Workflow-orca/issues/3).
Master, origin/master and GitHub agree. PR #8 is a two-parent merge and issue #2
is closed. No AOT/trim publish, consumer implementation or support claim occurs
in this contract target.

Selected source/package closure: OrcaCore.Engine.Ephemeral references OrcaCore and
OrcaCore.Core. Core references OrcaCore and links the fixed-codec/failure-converter
source into its own assembly. Its empty public API does not remove its runtime
dependency. Hosting/DI dependencies are separately pinned by the package projects.
Runtime.Protocol and durable providers are not in this selected closure.

The exact token sweep recorded 89 source lines across 15 files.
Every row has its base line/hash, risk category and assembly context. This search
is an auditable candidate inventory, not an exhaustive IL reachability analysis,
observed warning list, proof that each path fails, or an AOT compatibility test.
Future publish diagnostics, including dependency/native-tool findings, are the
authoritative warning inventory.

| Candidate family | Lines |
|---|---:|
| AttributeMetadata | 8 |
| ConstructorMetadata | 16 |
| ExpressionCompilation | 3 |
| JsonRuntimeTypeMetadata | 8 |
| MemberMetadata | 18 |
| RuntimeActivation | 9 |
| RuntimeGenericConstruction | 16 |
| RuntimeInvocationMetadata | 11 |

Key paths read individually:

- Facade registration/start and catalog dispatch inspect runtime state types and
  close methods dynamically. Queries and event handling also use the fixed codec;
  no public ephemeral event-ingress API is invented for the consumer.
- StructuredInvocationCache closes typed step methods, inspects private context
  constructors and builds expression invokers/snapshot factories. Expression
  interpreter/fallback behavior requires measurement; Compile alone is not a
  published failure classification.
- ScopeMergeAdapter, the ForEach adapter and StepResultWaitAccessor close runtime
  generic result/partition/wait shapes. Value-type fixtures are mandatory alongside
  reference types because their native generic closure can differ.
- FixedWorkflowValueCodec validates declared/runtime graphs by metadata inspection
  and calls JSON overloads with runtime Type and its own options. Strong-value
  converters have a finite product-owned dispatch table; they must not be confused
  with an app-defined replacement converter.
- The same linked codec source is compiled into OrcaCore and Core. Diagnostic
  occurrences must retain emitting assembly identity instead of deduplicating
  solely by physical filename.

Preserved normative constraints: doc 17 fixed codec, staged typed/named-step and
detachment contracts; PR-016; NF-001/010/012/030/032/040; AC-024 and composition/
ownership acceptance; quality-and-verification and developer-facing-surface.
No existing line in either normative tree changes here. New proposed owners are
NF-033 and AC-555–562 under the later task 2.1 transition.

Sweep expression: `MakeGeneric|GetMethod|GetPropert|GetField|GetConstructor|GetCustomAttribute|Activator|\.Compile\(|DynamicInvoke|JsonSerializer\.` over the three source trees, C# files only.
The TSV preserves every exact match and can be rerun against the pinned base.
