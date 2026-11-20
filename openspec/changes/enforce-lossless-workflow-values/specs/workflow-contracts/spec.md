## MODIFIED Requirements

### Requirement: Generated workflow metadata preserves the fixed codec
The selected ephemeral native path SHALL obtain application value contracts from one System.Text.Json source-generated metadata context registered through WorkflowJsonMetadata while retaining the fixed `orcacore-json-v1` options, bytes, fingerprints, product-owned converters, collection ordering, detachment, size limits and existing unsupported-shape rules. Generated mode on .NET 10 SHALL additionally reject a requested declared value closure containing any reachable JSON member for which JsonPropertyInfo.AssociatedParameter.IsMemberInitializer is true and JsonIgnoreAttribute.Condition is present and not Never. This declared-shape restriction SHALL apply independently of the submitted value or actual initializer and SHALL cover roots, nested/derived values and collection contracts. Classification SHALL use configured metadata/member attributes without constructing models, running getters, inspecting initializer IL or executing sample deserialization; unprovable classification after trimming/AOT SHALL fail qualification. Known definition closure SHALL fail Build/TryBuild with SFE-TYPE-002 and the existing exception/validation shapes; direct value/event boundaries SHALL throw NotSupportedException before consumption/mutation. Diagnostics SHALL name the type/member, Generated-mode omission restriction and remedy. An unused excluded type in an otherwise-valid context SHALL NOT by itself reject context registration. This is the only new mode-specific admission exception to rejection parity; unconfigured legacy Reflection mode SHALL apply the common value-level representation check, and common admitted values SHALL retain exact byte/value parity. Metadata acquisition SHALL NOT permit replacement serializers, application converters, arbitrary resolvers, private roots or altered context options. The library SHALL supply its own primitive/strong-value/failure contracts and SHALL use one authoritative process registration across both codec copies. Supported native values SHALL have a finite closed, source-generatable metadata graph subject to that restriction; missing or inaccessible members SHALL NOT be silently dropped. No registered metadata context SHALL enter a structural or input fingerprint.

#### Scenario: The same value crosses JIT and native boundaries
- **WHEN** a value, collection, strong value or failure tree in the common admitted shape set is encoded and decoded through legacy JIT and generated-metadata JIT, full trimming and Native AOT
- **THEN** exact encoded bytes, fingerprints, detached values and rejection rules agree under the fixed codec

#### Scenario: Public converter accessibility preserves the strong-string codec
- **WHEN** the approved catalogue amendment is implemented and any of EventName, WorkflowOutcomeName, AuthoredBranchId, ResourcePoolName, TransientPoolName, StartIdempotencyKey, StopConfirmationId, ResourcePoolOperationId, ResourceGovernancePartitionId, StepOperationId or LeaseProtectionToken crosses an ordinary external generated root, nested DTO or supported collection contract
- **THEN** its existing product attribution and closed factory encode the same quoted string and decode with the same Create/Parse validation as legacy JIT, preserve equality and input/definition fingerprints, and introduce no new codec hook or shape admission; CanConvert rejects types outside that exact table and direct CreateConverter rejects an unsupported type with the existing NotSupportedException

#### Scenario: Application context requests custom serialization
- **WHEN** registration or metadata validation encounters nondefault options, an application converter, custom resolver chain or a serialization-only context
- **THEN** it rejects the configuration or affected type without installing conflicting metadata, replacing codec behavior, admitting work or committing

#### Scenario: An omitted init-only member would change the detached value
- **WHEN** InitState has `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Count { get; init; } = 42` and a caller submits Count=0 or Count=7, either at the root or nested in a declared value closure
- **THEN** legacy Reflection mode rejects the unstable omitted zero and admits the stable present seven while Generated mode rejects both values by declared shape with SFE-TYPE-002 at definition validation or NotSupportedException at a direct boundary, without user-step execution, admission, key binding, event consumption or commit

#### Scenario: The original value state uses a member initializer without omission
- **WHEN** the original readonly record struct ValueState(int Number), an otherwise-supported ordinary settable DTO, or a genuine constructor-bound record passes the restriction and all existing codec checks
- **THEN** an initializer flag alone SHALL NOT cause rejection. An ordinary mutable or constructor-bound control SHALL accept each byte-stable value and reject a value whose omitted representation decodes and re-encodes differently

#### Scenario: Default runtime options conceal baked enum or resolver behavior
- **WHEN** a generated context sets UseStringEnumConverter=true with an empty Options.Converters list, or the review's handwritten JsonSerializerContext presents default options but renames Value to changed
- **THEN** Register rejects that context with NotSupportedException naming the unsupported policy/provenance, installs nothing, preserves retry before first use and causes no workflow effects; default options or generic constraints alone SHALL NOT be treated as conformance

## ADDED Requirements

### Requirement: Workflow values preserve their fixed-codec representation
Every accepted workflow input, state, result, output, event payload, event start input and DAG value SHALL preserve its serialized representation under its declared type and fixed metadata. Encoding the reconstructed value SHALL produce exactly the submitted bytes, including property and collection order. The first accepted representation SHALL supply both the stored bytes and any fingerprint. Application equality SHALL NOT participate. For submitted values that fail this check the codec SHALL throw `NotSupportedException` before engine-owned admission, start-key binding, event consumption or state commit. Existing declared-shape restrictions, null rules and metadata diagnostics SHALL still apply. Malformed committed framing or syntax SHALL retain its integrity classification. A well-formed DAG run input incompatible with a locally installed type SHALL retain its existing contract-mismatch outcome.

#### Scenario: A private setter or constructor loses a submitted value
- **WHEN** a submitted decoded value would re-encode differently under its declared type
- **THEN** the boundary rejects it before persisting a value or binding a start key, including on replay of an existing key

#### Scenario: An immutable value can reconstruct its bytes
- **WHEN** a constructor-bound, nested collection, polymorphic or nullable value satisfies the declared shape rules and re-encodes exactly
- **THEN** it uses the original fixed-codec bytes and an independent detached object whenever the boundary requires detachment
