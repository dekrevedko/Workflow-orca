namespace OrcaCore.Runtime.Durable.Serialization;

public interface IDurablePayloadTypeResolver
{
    bool TryGetTypeKey(Type type, out string key);

    bool TryResolveType(string key, out Type type);
}
