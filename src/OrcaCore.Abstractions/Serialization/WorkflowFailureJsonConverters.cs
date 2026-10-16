using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

#if ORCACORE_CORE_CODEC_COPY
namespace OrcaCore.Core.Internal;
#else
namespace OrcaCore.Internal;
#endif

internal sealed class AuthoredLocationJsonConverter : JsonConverter<AuthoredLocation>
{
    public override AuthoredLocation Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("A failure authored location must be a non-empty string.");
        }

        try
        {
            return (AuthoredLocation)typeof(AuthoredLocation)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single()
                .Invoke([value]);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is ArgumentException inner)
        {
            throw new JsonException("The failure authored location is not canonical.", inner);
        }
    }

    public override void Write(
        Utf8JsonWriter writer,
        AuthoredLocation value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

internal sealed class FailureOccurrenceJsonConverter : JsonConverter<FailureOccurrence>
{
    public override FailureOccurrence Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        RequireObject(root);
        var version = RequiredInt(root, "version");
        if (version != 1)
        {
            throw new JsonException($"Unsupported failure-occurrence version '{version}'.");
        }

        var kind = RequiredString(root, "kind");
        return kind switch
        {
            "root" when HasOnly(root, "version", "kind") =>
                CreateRootOccurrence(),
            "branch" when HasOnly(root, "version", "kind", "branchId") =>
                CreateBranchOccurrence(RequiredString(root, "branchId")),
            "item" when HasOnly(root, "version", "kind", "index") =>
                CreateItem(root),
            _ => throw new JsonException(
                $"Unknown or malformed failure-occurrence discriminator '{kind}'.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        FailureOccurrence value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("version", 1);
        switch (value)
        {
            case FailureOccurrence.Root:
                writer.WriteString("kind", "root");
                break;
            case FailureOccurrence.Branch branch:
                writer.WriteString("kind", "branch");
                writer.WriteString("branchId", branch.BranchId.Value);
                break;
            case FailureOccurrence.Item item:
                writer.WriteString("kind", "item");
                writer.WriteNumber("index", item.Index);
                break;
            default:
                throw new JsonException(
                    $"Unsupported failure occurrence '{value.GetType().FullName}'.");
        }

        writer.WriteEndObject();
    }

    private static FailureOccurrence CreateItem(JsonElement root)
    {
        var index = RequiredInt(root, "index");
        if (index < 0)
        {
            throw new JsonException("A failure item occurrence index cannot be negative.");
        }

        return (FailureOccurrence)typeof(FailureOccurrence.Item)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters() is
                [{ ParameterType: var parameterType }] &&
                parameterType == typeof(int))
            .Invoke([index]);
    }

    private static FailureOccurrence CreateRootOccurrence() =>
        (FailureOccurrence)typeof(FailureOccurrence.Root)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters().Length == 0)
            .Invoke([]);

    private static FailureOccurrence CreateBranchOccurrence(string branchId) =>
        (FailureOccurrence)typeof(FailureOccurrence.Branch)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(constructor => constructor.GetParameters() is
                [{ ParameterType: var parameterType }] &&
                parameterType == typeof(AuthoredBranchId))
            .Invoke([AuthoredBranchId.Create(branchId)]);

    private static void RequireObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A failure occurrence must be an object.");
        }
    }

    private static bool HasOnly(JsonElement element, params string[] names)
    {
        var allowed = names.ToHashSet(StringComparer.Ordinal);
        return element.EnumerateObject().All(property => allowed.Contains(property.Name)) &&
               element.EnumerateObject().Count() == allowed.Count;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new JsonException($"Failure occurrence property '{name}' is required.");
        }

        return property.GetString()!;
    }

    private static int RequiredInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value))
        {
            throw new JsonException($"Failure occurrence property '{name}' must be an integer.");
        }

        return value;
    }
}

internal sealed class WorkflowFailureJsonConverter : JsonConverter<WorkflowFailure>
{
    public override WorkflowFailure Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Count() != 5)
        {
            throw new JsonException("A workflow failure must be the exact v1 object shape.");
        }

        var code = RequiredString(root, "code");
        var message = RequiredString(root, "message");
        var location = root.TryGetProperty("authoredLocation", out var locationElement)
            ? JsonSerializer.Deserialize<AuthoredLocation>(locationElement.GetRawText(), options)
            : null;
        var occurrence = root.TryGetProperty("occurrence", out var occurrenceElement)
            ? JsonSerializer.Deserialize<FailureOccurrence>(occurrenceElement.GetRawText(), options)
            : null;
        if (!root.TryGetProperty("causes", out var causesElement) ||
            causesElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Workflow failure property 'causes' is required.");
        }

        var causes = causesElement.EnumerateArray()
            .Select(element => JsonSerializer.Deserialize<WorkflowFailure>(
                element.GetRawText(),
                options) ?? throw new JsonException("A workflow failure cause cannot be null."))
            .ToArray();
        if (location is null || occurrence is null)
        {
            throw new JsonException("Workflow failure provenance is required.");
        }

        return Create(code, message, location, occurrence, causes);
    }

    public override void Write(
        Utf8JsonWriter writer,
        WorkflowFailure value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("code", value.Code);
        writer.WriteString("message", value.Message);
        writer.WritePropertyName("authoredLocation");
        JsonSerializer.Serialize(writer, value.AuthoredLocation, options);
        writer.WritePropertyName("occurrence");
        JsonSerializer.Serialize(writer, value.Occurrence, options);
        writer.WritePropertyName("causes");
        writer.WriteStartArray();
        foreach (var cause in value.Causes)
        {
            JsonSerializer.Serialize(writer, cause, options);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static WorkflowFailure Create(
        string code,
        string message,
        AuthoredLocation location,
        FailureOccurrence occurrence,
        IReadOnlyList<WorkflowFailure> causes) =>
        (WorkflowFailure)typeof(WorkflowFailure)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single()
            .Invoke([code, message, location, occurrence, causes]);

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new JsonException($"Workflow failure property '{name}' is required.");
        }

        return property.GetString()!;
    }
}
