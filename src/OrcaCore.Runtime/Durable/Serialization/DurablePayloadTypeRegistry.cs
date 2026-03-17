using System.Collections.Concurrent;
using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Serialization;

public sealed class DurablePayloadTypeRegistry : IDurablePayloadTypeResolver
{
    private readonly ConcurrentDictionary<Type, string> _typeToKey = new();
    private readonly ConcurrentDictionary<string, Type> _keyToType = new(StringComparer.Ordinal);

    public static DurablePayloadTypeRegistry Default { get; } = CreateDefault();

    public DurablePayloadTypeRegistry Register<T>(string key) => Register(typeof(T), key);

    public DurablePayloadTypeRegistry Register(Type type, string key)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (_typeToKey.TryGetValue(type, out var existingKey) && !string.Equals(existingKey, key, StringComparison.Ordinal))
            throw new InvalidOperationException($"Payload type '{type.FullName}' is already registered with key '{existingKey}'.");

        if (_keyToType.TryGetValue(key, out var existingType) && existingType != type)
            throw new InvalidOperationException($"Payload type key '{key}' is already registered for '{existingType.FullName}'.");

        _typeToKey[type] = key;
        _keyToType[key] = type;
        return this;
    }

    public bool TryGetTypeKey(Type type, out string key) => _typeToKey.TryGetValue(type, out key!);

    public bool TryResolveType(string key, out Type type) => _keyToType.TryGetValue(key, out type!);

    private static DurablePayloadTypeRegistry CreateDefault()
    {
        return new DurablePayloadTypeRegistry()
            .Register<string>("string")
            .Register<bool>("bool")
            .Register<byte>("byte")
            .Register<sbyte>("sbyte")
            .Register<short>("int16")
            .Register<ushort>("uint16")
            .Register<int>("int32")
            .Register<uint>("uint32")
            .Register<long>("int64")
            .Register<ulong>("uint64")
            .Register<float>("single")
            .Register<double>("double")
            .Register<decimal>("decimal")
            .Register<Guid>("guid")
            .Register<DateTime>("datetime")
            .Register<DateTimeOffset>("datetimeoffset")
            .Register<JsonElement>("json");
    }
}
