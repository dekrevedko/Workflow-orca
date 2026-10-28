using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

#if ORCACORE_CORE_CODEC_COPY
namespace OrcaCore.Core.Internal;
#else
namespace OrcaCore.Internal;
#endif

/// <summary>
/// Implements the one fixed codec used for workflow values in every engine.
/// </summary>
internal static class FixedWorkflowValueCodec
{
    private static readonly ConcurrentDictionary<Type, bool> DeclaredTypeSupport = new();
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    internal const string Format = "orcacore-json-v1";

    internal const int MaxEncodedValueBytes = 256 * 1024;

    internal static bool IsSupportedDeclaredType(Type declaredType)
    {
        if (!IsSupportedDeclaredShape(declaredType)) return false;
        try { _ = global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.ResolveContract(declaredType); }
        catch (global::OrcaCore.Internal.WorkflowJsonContractException) { return false; }
        return true;
    }

    internal static bool IsSupportedDeclaredShape(Type declaredType)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        if (global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.Seal() is { } generated)
            return DeclaredTypeSupport.GetOrAdd(declaredType, type => IsSupportedGeneratedShape(type, generated, []));
        if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
            throw new InvalidOperationException("Reflection workflow metadata is unavailable.");
        return DeclaredTypeSupport.GetOrAdd(declaredType, static type => IsSupportedDeclaredType(type, []));
    }

    internal static byte[] Serialize(object? value, Type declaredType)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        EnsureSupportedDeclaredType(declaredType);
        ValidateGraph(value, declaredType, new HashSet<object>(ReferenceEqualityComparer.Instance));
        var options = global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.Seal();
        if (options is not null) return JsonSerializer.SerializeToUtf8Bytes(value, options.GetTypeInfo(declaredType));
        if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
            throw new InvalidOperationException("Reflection workflow metadata is unavailable.");
        return JsonSerializer.SerializeToUtf8Bytes(value, declaredType, SerializerOptions);
    }

    internal static object? Deserialize(ReadOnlySpan<byte> payload, Type declaredType)
    {
        ArgumentNullException.ThrowIfNull(declaredType);
        EnsureSupportedDeclaredType(declaredType);
        var options = global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.Seal();
        if (options is not null) return JsonSerializer.Deserialize(payload, options.GetTypeInfo(declaredType));
        if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
            throw new InvalidOperationException("Reflection workflow metadata is unavailable.");
        return JsonSerializer.Deserialize(payload, declaredType, SerializerOptions);
    }

    private static void EnsureSupportedDeclaredType(Type declaredType)
    {
        if (!IsSupportedDeclaredShape(declaredType))
        {
            throw new NotSupportedException(
                $"Declared payload type '{declaredType.FullName}' uses serialization customization " +
                $"that is not supported by {Format}.");
        }
        _ = global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.ResolveContract(declaredType);
    }

    private static bool IsSupportedGeneratedShape(Type type, JsonSerializerOptions options, HashSet<Type> active)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsPointer || type.IsByRef || type.IsByRefLike || type.ContainsGenericParameters ||
            typeof(Delegate).IsAssignableFrom(type)) return false;
        if (!active.Add(type)) return true;
        try
        {
            if (!HasOnlyProductOwnedConverter(type, type)) return false;
            if (TryGetDictionaryTypes(type, out var key, out var value))
                return key == typeof(string) && IsSupportedGeneratedShape(value, options, active);
            if (TryGetSequenceElementType(type, out var element))
                return IsSupportedGeneratedShape(element, options, active);
            if (IsCollectionLike(type)) return false;
            if (IsLeafType(type) || type == typeof(global::OrcaCore.WorkflowFailure) ||
                type == typeof(global::OrcaCore.FailureOccurrence) || type == typeof(global::OrcaCore.AuthoredLocation)) return true;
            System.Text.Json.Serialization.Metadata.JsonTypeInfo info;
            try { info = options.GetTypeInfo(type); }
            catch (NotSupportedException) { return true; } // ResolveContract supplies SFE-TYPE-003; never discover missing members.
            if (info.PolymorphismOptions is { } polymorphism)
                foreach (var derived in polymorphism.DerivedTypes)
                    if (!IsSupportedGeneratedShape(derived.DerivedType, options, active)) return false;
            foreach (var property in info.Properties)
            {
                if (property.Get is null && property.Set is null && property.AssociatedParameter is null) continue;
                if (property.AttributeProvider?.GetCustomAttributes(typeof(JsonConverterAttribute), inherit: false).Length > 0 &&
                    type.Assembly != typeof(global::OrcaCore.DefinitionId).Assembly) return false;
                if (!IsSupportedGeneratedShape(property.PropertyType, options, active)) return false;
            }
            return true;
        }
        finally { active.Remove(type); }
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Unconfigured legacy JIT declared-shape discovery only.")]
    private static bool IsSupportedDeclaredType(Type declaredType, HashSet<Type> activeTypes)
    {
        declaredType = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (declaredType == typeof(global::OrcaCore.WorkflowFailure) ||
            declaredType == typeof(global::OrcaCore.FailureOccurrence) ||
            declaredType == typeof(global::OrcaCore.AuthoredLocation))
        {
            return true;
        }

        if (declaredType.IsPointer ||
            declaredType.IsByRef ||
            declaredType.IsByRefLike ||
            declaredType.ContainsGenericParameters ||
            typeof(Delegate).IsAssignableFrom(declaredType))
        {
            return false;
        }

        if (!activeTypes.Add(declaredType))
        {
            return true;
        }

        try
        {
            if (!HasOnlyProductOwnedConverter(declaredType, declaredType))
            {
                return false;
            }

            if (TryGetDictionaryTypes(declaredType, out var keyType, out var valueType))
            {
                return keyType == typeof(string) &&
                       IsSupportedDeclaredType(valueType, activeTypes);
            }

            if (TryGetSequenceElementType(declaredType, out var elementType))
            {
                return IsSupportedDeclaredType(elementType, activeTypes);
            }

            if (IsCollectionLike(declaredType))
            {
                return false;
            }

            if (IsLeafType(declaredType))
            {
                return true;
            }

            foreach (var derivedType in declaredType
                         .GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
                         .Select(attribute => attribute.DerivedType))
            {
                if (!IsSupportedDeclaredType(derivedType, activeTypes))
                {
                    return false;
                }
            }

            foreach (var property in declaredType.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!IsSerializedProperty(property))
                {
                    continue;
                }

                if (!HasOnlyProductOwnedConverter(property, declaredType) ||
                    !IsSupportedDeclaredType(property.PropertyType, activeTypes))
                {
                    return false;
                }
            }

            foreach (var field in declaredType.GetFields(
                         BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.GetCustomAttribute<JsonIncludeAttribute>(inherit: false) is null ||
                    field.GetCustomAttribute<JsonIgnoreAttribute>(inherit: false)?.Condition ==
                    JsonIgnoreCondition.Always)
                {
                    continue;
                }

                if (!HasOnlyProductOwnedConverter(field, declaredType) ||
                    !IsSupportedDeclaredType(field.FieldType, activeTypes))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            activeTypes.Remove(declaredType);
        }
    }

    private static bool HasOnlyProductOwnedConverter(MemberInfo member, Type owningType)
    {
        var converter = member.GetCustomAttribute<JsonConverterAttribute>(inherit: false);
        if (converter is null)
        {
            return true;
        }

        var productAssembly = typeof(global::OrcaCore.DefinitionId).Assembly;
        return owningType.Assembly == productAssembly &&
               converter.ConverterType?.Assembly == productAssembly;
    }

    private static bool IsSerializedProperty(PropertyInfo property)
    {
        if (property.GetIndexParameters().Length != 0 ||
            property.GetCustomAttribute<JsonIgnoreAttribute>(inherit: false)?.Condition ==
            JsonIgnoreCondition.Always)
        {
            return false;
        }

        return property.GetMethod?.IsPublic == true ||
               property.GetCustomAttribute<JsonIncludeAttribute>(inherit: false) is not null;
    }

    private static void ValidateGraph(
        object? value,
        Type declaredType,
        HashSet<object> activeReferences)
    {
        if (value is null)
        {
            return;
        }

        declaredType = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        var runtimeType = value.GetType();
        if (value is global::OrcaCore.WorkflowFailure failure)
        {
            ValidateFailure(failure, activeReferences);
            return;
        }

        if (value is global::OrcaCore.AuthoredLocation)
        {
            return;
        }

        if (value is global::OrcaCore.FailureOccurrence occurrence)
        {
            if (occurrence is not (
                    global::OrcaCore.FailureOccurrence.Root or
                    global::OrcaCore.FailureOccurrence.Branch or
                    global::OrcaCore.FailureOccurrence.Item))
            {
                throw UnsupportedPolymorphism(runtimeType, declaredType);
            }

            return;
        }

        if (TryGetDictionaryTypes(declaredType, out var keyType, out var valueType))
        {
            if (!IsSupportedDictionaryRuntimeType(runtimeType, keyType, valueType))
            {
                throw UnsupportedCollection(runtimeType, declaredType);
            }

            var trackedDictionary = !runtimeType.IsValueType;
            if (trackedDictionary && !activeReferences.Add(value))
            {
                throw new JsonException($"Cyclic payload graph is not supported by {Format}.");
            }

            try
            {
                foreach (DictionaryEntry entry in (IDictionary)value)
                {
                    ValidateGraph(entry.Key, keyType, activeReferences);
                    ValidateGraph(entry.Value, valueType, activeReferences);
                }
            }
            finally
            {
                if (trackedDictionary)
                {
                    activeReferences.Remove(value);
                }
            }

            return;
        }

        if (TryGetSequenceElementType(declaredType, out var elementType))
        {
            if (!IsSupportedSequenceRuntimeType(runtimeType, declaredType, elementType))
            {
                throw UnsupportedCollection(runtimeType, declaredType);
            }

            var trackedCollection = !runtimeType.IsValueType;
            if (trackedCollection && !activeReferences.Add(value))
            {
                throw new JsonException($"Cyclic payload graph is not supported by {Format}.");
            }

            try
            {
                foreach (var item in (IEnumerable)value)
                {
                    ValidateGraph(item, elementType, activeReferences);
                }
            }
            finally
            {
                if (trackedCollection)
                {
                    activeReferences.Remove(value);
                }
            }

            return;
        }

        var generated = global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.Seal();
        if (runtimeType != declaredType)
        {
            var approved = generated is not null
                ? generated.GetTypeInfo(declaredType).PolymorphismOptions?.DerivedTypes.Any(derived => derived.DerivedType == runtimeType) == true
                : declaredType.GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false).Any(attribute => attribute.DerivedType == runtimeType);
            if (!approved) throw UnsupportedPolymorphism(runtimeType, declaredType);
        }

        if (IsLeafType(runtimeType))
        {
            return;
        }

        var tracked = !runtimeType.IsValueType;
        if (tracked && !activeReferences.Add(value))
        {
            throw new JsonException($"Cyclic payload graph is not supported by {Format}.");
        }

        try
        {
            if (generated is not null)
            {
                foreach (var property in generated.GetTypeInfo(runtimeType).Properties)
                    if (property.Get is not null && property.AttributeProvider is PropertyInfo member &&
                        (member.GetMethod?.IsPublic == true || member.SetMethod?.IsPublic == true))
                        ValidateGraph(property.Get(value), property.PropertyType, activeReferences);
            }
            else
            {
                if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
                    throw new InvalidOperationException("Reflection workflow metadata is unavailable.");
                ValidateLegacyMembers(value, runtimeType, activeReferences);
            }
        }
        finally
        {
            if (tracked)
            {
                activeReferences.Remove(value);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Unconfigured legacy JIT graph member discovery only.")]
    private static void ValidateLegacyMembers(object value, Type runtimeType, HashSet<object> activeReferences)
    {
            foreach (var property in runtimeType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.GetMethod is null ||
                    property.GetIndexParameters().Length != 0 ||
                    property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition ==
                    JsonIgnoreCondition.Always)
                {
                    continue;
                }

                ValidateGraph(property.GetValue(value), property.PropertyType, activeReferences);
            }
    }

    private static bool TryGetDictionaryTypes(
        Type declaredType,
        out Type keyType,
        out Type valueType)
    {
        if (declaredType.IsGenericType &&
            declaredType.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(Dictionary<,>) ||
             definition == typeof(IDictionary<,>) ||
             definition == typeof(IReadOnlyDictionary<,>)))
        {
            var arguments = declaredType.GetGenericArguments();
            keyType = arguments[0];
            valueType = arguments[1];
            return true;
        }

        keyType = null!;
        valueType = null!;
        return false;
    }

    private static bool TryGetSequenceElementType(Type declaredType, out Type elementType)
    {
        if (declaredType.IsArray && declaredType.GetArrayRank() == 1)
        {
            elementType = declaredType.GetElementType()!;
            return true;
        }

        if (declaredType.IsGenericType &&
            declaredType.GetGenericTypeDefinition() is var definition &&
            (definition == typeof(List<>) ||
             definition == typeof(IList<>) ||
             definition == typeof(IReadOnlyList<>)))
        {
            elementType = declaredType.GetGenericArguments()[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    private static bool IsSupportedDictionaryRuntimeType(
        Type runtimeType,
        Type keyType,
        Type valueType) =>
        runtimeType.IsGenericType &&
        runtimeType.GetGenericTypeDefinition() == typeof(Dictionary<,>) &&
        runtimeType.GetGenericArguments().SequenceEqual([keyType, valueType]);

    private static bool IsSupportedSequenceRuntimeType(
        Type runtimeType,
        Type declaredType,
        Type elementType)
    {
        if (declaredType.IsArray)
        {
            return runtimeType == declaredType;
        }

        if (runtimeType.IsArray)
        {
            return runtimeType.GetArrayRank() == 1 &&
                   runtimeType.GetElementType() == elementType;
        }

        return runtimeType.IsGenericType &&
               runtimeType.GetGenericTypeDefinition() == typeof(List<>) &&
               runtimeType.GetGenericArguments()[0] == elementType;
    }

    private static bool IsCollectionLike(Type type) =>
        type != typeof(string) && typeof(IEnumerable).IsAssignableFrom(type);

    private static bool IsLeafType(Type type)
    {
        return type.IsPrimitive ||
               type.IsEnum ||
               type == typeof(string) ||
               type == typeof(decimal) ||
               type == typeof(DateTime) ||
               type == typeof(DateTimeOffset) ||
               type == typeof(TimeSpan) ||
               type == typeof(Guid) ||
               type == typeof(Uri);
    }

    private static NotSupportedException UnsupportedPolymorphism(Type runtimeType, Type declaredType)
    {
        return new NotSupportedException(
            $"Unapproved polymorphic payload '{runtimeType.FullName}' for declared type " +
            $"'{declaredType.FullName}' is not supported by {Format}.");
    }

    private static NotSupportedException UnsupportedCollection(Type runtimeType, Type declaredType)
    {
        return new NotSupportedException(
            $"Collection payload '{runtimeType.FullName}' for declared type '{declaredType.FullName}' " +
            $"is outside the fixed sequence/map allowlist for {Format}.");
    }

    private static void ValidateFailure(
        global::OrcaCore.WorkflowFailure failure,
        HashSet<object> activeReferences)
    {
        if (!activeReferences.Add(failure))
        {
            throw new JsonException($"Cyclic payload graph is not supported by {Format}.");
        }

        try
        {
            ValidateGraph(
                failure.AuthoredLocation,
                typeof(global::OrcaCore.AuthoredLocation),
                activeReferences);
            ValidateGraph(
                failure.Occurrence,
                typeof(global::OrcaCore.FailureOccurrence),
                activeReferences);
            foreach (var cause in failure.Causes)
            {
                ValidateFailure(cause, activeReferences);
            }
        }
        finally
        {
            activeReferences.Remove(failure);
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new AuthoredLocationJsonConverter());
        options.Converters.Add(new FailureOccurrenceJsonConverter());
        options.Converters.Add(new WorkflowFailureJsonConverter());
        return options;
    }
}
