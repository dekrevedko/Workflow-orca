using System.Reflection;
using System.CodeDom.Compiler;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrcaCore.Internal;

// This state lives only in OrcaCore. The Core-linked codec uses this same owner.
internal static class WorkflowJsonMetadataRegistry
{
    private static readonly object Gate = new();
    private static JsonSerializerContext? context;
    private static JsonSerializerOptions? options;
    private static bool sealedMode;
    private static Type? pendingContext;
    private static readonly AsyncLocal<ConstructionScope?> ConstructingContext = new();
    private sealed class ConstructionScope { internal bool Active = true; }

    [System.Diagnostics.CodeAnalysis.FeatureGuard(typeof(System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute))]
    public static bool DynamicCodeEnabled => System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported;

    internal static bool LegacyReflectionEnabled =>
        DynamicCodeEnabled && JsonSerializer.IsReflectionEnabledByDefault && Seal() is null;

    internal static void Register<TContext>() where TContext : JsonSerializerContext, new()
    {
        var requested = typeof(TContext);
        lock (Gate)
        {
            if (ConstructingContext.Value is { Active: true })
                throw Conflict(requested, "reentrant context registration is not supported");
            while (true)
            {
                if (context?.GetType() == requested) return;
                if (context is not null || sealedMode)
                    throw Conflict(requested, "registration conflicts with the current configuration");
                if (pendingContext is null)
                {
                    pendingContext = requested;
                    ConstructingContext.Value = new ConstructionScope();
                    break;
                }
                if (pendingContext != requested)
                    throw Conflict(requested, "a different context registration is in progress");
                Monitor.Wait(Gate);
            }
        }
        try
        {
            // Never run application construction or metadata/configuration code under Gate.
            if (requested.GetCustomAttribute<GeneratedCodeAttribute>(inherit: false)?.Tool != "System.Text.Json.SourceGeneration")
                throw new NotSupportedException($"Workflow JSON metadata context '{requested.FullName}' has unsupported provenance; use the ordinary System.Text.Json source generator.");
            var candidate = new TContext();
            if (requested.GetCustomAttribute<JsonSourceGenerationOptionsAttribute>(inherit: false)?.UseStringEnumConverter == true)
                throw new NotSupportedException($"Workflow JSON metadata context '{requested.FullName}' uses unsupported UseStringEnumConverter policy.");
            ValidateConfiguration(requested, candidate);
            var candidateOptions = new JsonSerializerOptions { TypeInfoResolver = new FixedMetadataResolver(candidate) };
            candidateOptions.Converters.Add(new AuthoredLocationJsonConverter());
            candidateOptions.Converters.Add(new FailureOccurrenceJsonConverter());
            candidateOptions.Converters.Add(new WorkflowFailureJsonConverter());
            candidateOptions.MakeReadOnly();
            lock (Gate)
            {
                if (sealedMode) throw Conflict(requested, "registration lost the first-use race");
                context = candidate;
                options = candidateOptions;
            }
        }
        catch (NotSupportedException exception)
        {
            lock (Gate) throw new NotSupportedException(Describe(requested, exception.Message), exception);
        }
        catch (Exception exception)
        {
            lock (Gate) throw new InvalidOperationException(Describe(requested, "context construction/configuration failed: " + exception.Message), exception);
        }
        finally
        {
            lock (Gate)
            {
                pendingContext = null;
                ConstructingContext.Value!.Active = false;
                ConstructingContext.Value = null;
                Monitor.PulseAll(Gate);
            }
        }
    }

    private static InvalidOperationException Conflict(Type requested, string reason) => new(Describe(requested, reason));

    private static string Describe(Type requested, string reason) =>
        $"Workflow JSON metadata requested context '{requested.FullName}': {reason}; current context '{context?.GetType().FullName ?? "Reflection (unconfigured)"}', pending context '{pendingContext?.FullName ?? "none"}', sealed={sealedMode}.";

    private static void ValidateConfiguration(Type type, JsonSerializerContext candidate)
    {
        var mode = type.GetCustomAttribute<JsonSourceGenerationOptionsAttribute>(inherit: false)?.GenerationMode;
        if (mode == JsonSourceGenerationMode.Serialization ||
            type.GetCustomAttributes<JsonSerializableAttribute>(inherit: false)
                .Any(root => root.GenerationMode == JsonSourceGenerationMode.Serialization))
            throw new NotSupportedException($"Workflow JSON metadata context '{type.FullName}' must generate Metadata for reading and writing.");
        var value = candidate.Options;
        var defaults = new JsonSerializerOptions();
        // A parameterless generated context can expose default runtime Options while its
        // generated resolver has baked a different attribute policy. Check both sources.
        var expected = new Dictionary<string, object?>
        {
            ["AllowOutOfOrderMetadataProperties"] = defaults.AllowOutOfOrderMetadataProperties,
            ["AllowTrailingCommas"] = defaults.AllowTrailingCommas,
            ["AllowDuplicateProperties"] = defaults.AllowDuplicateProperties,
            ["DefaultBufferSize"] = defaults.DefaultBufferSize,
            ["DefaultIgnoreCondition"] = (int)defaults.DefaultIgnoreCondition,
            ["DictionaryKeyPolicy"] = 0, ["PropertyNamingPolicy"] = 0,
            ["IgnoreReadOnlyFields"] = defaults.IgnoreReadOnlyFields,
            ["IgnoreReadOnlyProperties"] = defaults.IgnoreReadOnlyProperties,
            ["IncludeFields"] = defaults.IncludeFields, ["MaxDepth"] = defaults.MaxDepth,
            ["NumberHandling"] = (int)defaults.NumberHandling,
            ["PreferredObjectCreationHandling"] = (int)defaults.PreferredObjectCreationHandling,
            ["PropertyNameCaseInsensitive"] = defaults.PropertyNameCaseInsensitive,
            ["ReadCommentHandling"] = (int)defaults.ReadCommentHandling,
            ["RespectNullableAnnotations"] = defaults.RespectNullableAnnotations,
            ["RespectRequiredConstructorParameters"] = defaults.RespectRequiredConstructorParameters,
            ["UnknownTypeHandling"] = (int)defaults.UnknownTypeHandling,
            ["UnmappedMemberHandling"] = (int)defaults.UnmappedMemberHandling,
            ["WriteIndented"] = defaults.WriteIndented,
            ["IndentCharacter"] = defaults.IndentCharacter, ["IndentSize"] = defaults.IndentSize,
            ["NewLine"] = defaults.NewLine, ["UseStringEnumConverter"] = false
        };
        var configuration = type.GetCustomAttributesData().SingleOrDefault(attribute =>
            attribute.AttributeType == typeof(JsonSourceGenerationOptionsAttribute));
        if (configuration is not null)
        {
            if (configuration.ConstructorArguments.Any(argument => !Equals(argument.Value, 0)))
                throw new NotSupportedException($"Workflow JSON metadata context '{type.FullName}' must use default generated options.");
            foreach (var argument in configuration.NamedArguments)
            {
                if (argument.MemberName == "GenerationMode") continue;
                if (argument.MemberName == "Converters" && (argument.TypedValue.Value is null ||
                    argument.TypedValue.Value is IReadOnlyCollection<CustomAttributeTypedArgument> { Count: 0 })) continue;
                if (!expected.TryGetValue(argument.MemberName, out var expectedValue) ||
                    !Equals(argument.TypedValue.Value, expectedValue))
                    throw new NotSupportedException($"Workflow JSON metadata context '{type.FullName}' has unsupported generated options policy '{argument.MemberName}'.");
            }
        }
        if (value.Converters.Count != 0 || !ReferenceEquals(value.TypeInfoResolver, candidate) ||
            value.TypeInfoResolverChain.Count != 1 ||
            value.AllowOutOfOrderMetadataProperties != defaults.AllowOutOfOrderMetadataProperties ||
            value.AllowTrailingCommas != defaults.AllowTrailingCommas ||
            value.AllowDuplicateProperties != defaults.AllowDuplicateProperties ||
            value.DefaultBufferSize != defaults.DefaultBufferSize ||
            value.DefaultIgnoreCondition != defaults.DefaultIgnoreCondition ||
            value.DictionaryKeyPolicy is not null || value.Encoder is not null ||
            value.IgnoreReadOnlyFields != defaults.IgnoreReadOnlyFields ||
            value.IgnoreReadOnlyProperties != defaults.IgnoreReadOnlyProperties ||
            value.IncludeFields != defaults.IncludeFields || value.MaxDepth != defaults.MaxDepth ||
            value.NumberHandling != defaults.NumberHandling ||
            value.PreferredObjectCreationHandling != defaults.PreferredObjectCreationHandling ||
            value.PropertyNameCaseInsensitive != defaults.PropertyNameCaseInsensitive ||
            value.PropertyNamingPolicy is not null || value.ReadCommentHandling != defaults.ReadCommentHandling ||
            value.ReferenceHandler is not null ||
            value.RespectNullableAnnotations != defaults.RespectNullableAnnotations ||
            value.RespectRequiredConstructorParameters != defaults.RespectRequiredConstructorParameters ||
            value.UnknownTypeHandling != defaults.UnknownTypeHandling ||
            value.UnmappedMemberHandling != defaults.UnmappedMemberHandling ||
            value.WriteIndented != defaults.WriteIndented ||
            value.IndentCharacter != defaults.IndentCharacter || value.IndentSize != defaults.IndentSize ||
            value.NewLine != defaults.NewLine)
            throw new NotSupportedException($"Workflow JSON metadata context '{type.FullName}' has unsupported options/resolver/converters; use default generated metadata options.");
    }

    private sealed class FixedMetadataResolver(JsonSerializerContext application) : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions serializerOptions)
        {
            var info = ((IJsonTypeInfoResolver)WorkflowJsonProductContext.Default).GetTypeInfo(type, serializerOptions) ??
                ((IJsonTypeInfoResolver)application).GetTypeInfo(type, serializerOptions);
            if (info is not null) FixedWorkflowValueCodec.ValidateSerializedMembers(info);
            return info;
        }
    }

    internal static JsonTypeInfo? ResolveContract(Type root)
    {
        var configured = Seal();
        return configured is null ? null : WorkflowJsonTypeContracts.Validate(root, context!.GetType(), configured);
    }

    internal static JsonSerializerOptions? Seal()
    {
        lock (Gate) { sealedMode = true; Monitor.PulseAll(Gate); return options; }
    }
}
