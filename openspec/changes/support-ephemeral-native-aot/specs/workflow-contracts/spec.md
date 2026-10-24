## ADDED Requirements

### Requirement: Generated workflow metadata preserves the fixed codec
The selected ephemeral native path SHALL obtain application value contracts from one System.Text.Json source-generated metadata context registered through WorkflowJsonMetadata while retaining the fixed `orcacore-json-v1` options, bytes, fingerprints, product-owned converters, collection ordering, detachment, size limits and existing unsupported-shape rules. Generated mode on .NET 10 SHALL additionally reject a requested declared value closure containing any reachable JSON member for which JsonPropertyInfo.AssociatedParameter.IsMemberInitializer is true and JsonIgnoreAttribute.Condition is present and not Never. This declared-shape restriction SHALL apply independently of the submitted value or actual initializer and SHALL cover roots, nested/derived values and collection contracts. Classification SHALL use configured metadata/member attributes without constructing models, running getters, inspecting initializer IL or executing sample deserialization; unprovable classification after trimming/AOT SHALL fail qualification. Known definition closure SHALL fail Build/TryBuild with SFE-TYPE-002 and the existing exception/validation shapes; direct value/event boundaries SHALL throw NotSupportedException before consumption/mutation. Diagnostics SHALL name the type/member, Generated-mode omission restriction and remedy. An unused excluded type in an otherwise-valid context SHALL NOT by itself reject context registration. This is the only new mode-specific admission exception to rejection parity; unconfigured legacy Reflection mode SHALL retain existing acceptance/round trips, and common admitted shapes SHALL retain exact byte/value parity. Metadata acquisition SHALL NOT permit replacement serializers, application converters, arbitrary resolvers, private roots or altered context options. The library SHALL supply its own primitive/strong-value/failure contracts and SHALL use one authoritative process registration across both codec copies. Supported native values SHALL have a finite closed, source-generatable metadata graph subject to that restriction; missing or inaccessible members SHALL NOT be silently dropped. No registered metadata context SHALL enter a structural or input fingerprint.

#### Scenario: The same value crosses JIT and native boundaries
- **WHEN** a value, collection, strong value or failure tree in the common admitted shape set is encoded and decoded through legacy JIT and generated-metadata JIT, full trimming and Native AOT
- **THEN** exact encoded bytes, fingerprints, detached values and rejection rules agree under the fixed codec

#### Scenario: Application context requests custom serialization
- **WHEN** registration or metadata validation encounters nondefault options, an application converter, custom resolver chain or a serialization-only context
- **THEN** it rejects the configuration or affected type without installing conflicting metadata, replacing codec behavior, admitting work or committing

#### Scenario: An omitted init-only member would change the detached value
- **WHEN** InitState has `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int Count { get; init; } = 42` and a caller submits Count=0 or Count=7, either at the root or nested in a declared value closure
- **THEN** legacy Reflection mode retains the existing omitted-to-42 and present-to-7 round trips while Generated mode rejects both values by declared shape with SFE-TYPE-002 at definition validation or NotSupportedException at a direct boundary, without user-step execution, admission, key binding, event consumption or commit

#### Scenario: The original value state uses a member initializer without omission
- **WHEN** the original readonly record struct ValueState(int Number), an otherwise-supported ordinary settable DTO, or a genuine constructor-bound record passes the restriction and all existing codec checks
- **THEN** it remains admitted and its omitted/present round trips agree with the unchanged legacy behavior; an initializer flag alone SHALL NOT cause rejection

#### Scenario: Default runtime options conceal baked enum or resolver behavior
- **WHEN** a generated context sets UseStringEnumConverter=true with an empty Options.Converters list, or the review's handwritten JsonSerializerContext presents default options but renames Value to changed
- **THEN** Register rejects that context with NotSupportedException naming the unsupported policy/provenance, installs nothing, preserves retry before first use and causes no workflow effects; default options or generic constraints alone SHALL NOT be treated as conformance

### Requirement: Missing generated metadata is diagnosed before effects
Generated mode SHALL NOT fall back to reflection. Known definition value closure with absent metadata SHALL fail Build/TryBuild with diagnostic `SFE-TYPE-003` (CodecMetadataMissing), identifying the declared type, missing transitive type when different, registered context and source-generation remedy. Build SHALL preserve WorkflowDefinitionException and TryBuild SHALL preserve its validation shape. `SFE-TYPE-002` SHALL remain the unsupported-shape diagnostic. A direct event/value materialization boundary SHALL throw NotSupportedException with equivalent metadata detail before consuming input or mutating an instance. Dynamic values selected by user delegates SHALL be checked at their value boundary before commit; no inspection of delegate bodies SHALL be required.

#### Scenario: A state member type is absent from generated metadata
- **WHEN** a definition's declared root resolves but a required transitive contract does not
- **THEN** validation reports SFE-TYPE-003 for both relevant types/context and no start, user step, key binding or commit occurs

#### Scenario: An undeclared event value arrives after admission
- **WHEN** an accepted instance receives a value whose runtime closure has missing metadata or an unapproved subtype
- **THEN** it rejects before consuming the event or changing instance state and existing waits remain usable
