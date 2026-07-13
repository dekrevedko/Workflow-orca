namespace OrcaCore.Abstractions.Serialization;

public interface IPayloadSchemaResolver
{
    bool TryGetTypeKey(Type type, out string key);

    bool TryResolveType(string key, out Type type);

    bool TryGetSchemaId(Type type, out string schemaId);

    bool TryResolveContentType(Type type, out string contentType);
}
