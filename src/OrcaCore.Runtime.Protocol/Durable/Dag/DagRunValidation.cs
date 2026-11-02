using System.Security.Cryptography;

namespace OrcaCore.Abstractions.Durable.Dag;

internal static class DagRunValidation
{
    internal static T Required<T>(T? value) where T : class =>
        value ?? throw new InvalidDataException("DAG_PROTOCOL_INVALID: a required value is null.");

    internal static string Fingerprint(string value)
    {
        if (value is null || value.Length != 64 || value.Any(c => !(c is >= '0' and <= '9' or >= 'A' and <= 'F')))
            throw new InvalidDataException(
                "DAG_PROTOCOL_INVALID: fingerprint must be 64 uppercase ASCII hex characters.");
        return value;
    }

    internal static byte[] ValueBytes(bool present, string contract, string codec, byte[] bytes, string fingerprint)
    {
        if (!present || string.IsNullOrWhiteSpace(contract) || codec != "orcacore-json-v1" || bytes is null)
            throw new InvalidDataException("DAG_PROTOCOL_INVALID: invalid present value, contract or codec.");
        StorageText(contract);
        Fingerprint(fingerprint);
        var copy = bytes.ToArray();
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(copy)), fingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("DAG_PROTOCOL_INVALID: committed-byte fingerprint mismatch.");
        return copy;
    }

    internal static string StartKey(string value)
    {
        return StorageText(value);
    }

    internal static string StorageText(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\0'))
            throw new InvalidDataException("DAG_PROTOCOL_INVALID: empty or unrepresentable storage text.");
        try
        {
            _ = new System.Text.UTF8Encoding(false, true).GetByteCount(value);
        }
        catch (System.Text.EncoderFallbackException exception)
        {
            throw new InvalidDataException("DAG_PROTOCOL_INVALID: invalid storage-text Unicode.", exception);
        }

        return value;
    }

    internal static IReadOnlyList<DagRunNodeV1> GraphNodes(
        DagRunBindingV1 definition, string inputKey, IReadOnlyList<DagRunNodeV1> source)
    {
        Required(definition);
        Required(definition.DefinitionId);
        Required(definition.DefinitionVersion);
        Fingerprint(definition.Fingerprint);
        if (string.IsNullOrWhiteSpace(inputKey))
            throw new InvalidDataException("DAG_PROTOCOL_INVALID: input contract.");
        StorageText(inputKey);
        var nodes = Required(source).ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var indegrees = new int[nodes.Length];
        var dependants = Enumerable.Range(0, nodes.Length).Select(_ => new List<int>()).ToArray();
        for (var ordinal = 0; ordinal < nodes.Length; ordinal++)
        {
            var node = Required(nodes[ordinal]);
            if (node.Ordinal != ordinal || string.IsNullOrWhiteSpace(node.NodeId) || node.NodeId.Length > 450 ||
                !string.Equals(node.NodeId,node.NodeId.Trim(),StringComparison.Ordinal) ||
                !names.Add(node.NodeId) || string.IsNullOrWhiteSpace(node.InputContractKey))
                throw new InvalidDataException("DAG_PROTOCOL_INVALID: graph ordinal, node identity or input contract.");
            StorageText(node.NodeId);
            StorageText(node.InputContractKey);
            if (node.OutputContractKey is { } outputKey) StorageText(outputKey);
            Required(node.Workflow);
            Required(node.Workflow.DefinitionId);
            Required(node.Workflow.DefinitionVersion);
            Fingerprint(node.Workflow.Fingerprint);
            var dependencies = node.DirectDependencies;
            if (dependencies.Distinct().Count() != dependencies.Count)
                throw new InvalidDataException("DAG_PROTOCOL_INVALID: duplicate dependency.");
            foreach (var dependency in dependencies)
            {
                if (dependency < 0 || dependency >= nodes.Length || dependency == ordinal)
                    throw new InvalidDataException("DAG_PROTOCOL_INVALID: invalid dependency.");
                dependants[dependency].Add(ordinal);
                indegrees[ordinal]++;
            }
        }

        var ready = new Queue<int>(Enumerable.Range(0, nodes.Length).Where(i => indegrees[i] == 0));
        var visited = 0;
        while (ready.TryDequeue(out var ordinal))
        {
            visited++;
            foreach (var dependant in dependants[ordinal])
                if (--indegrees[dependant] == 0)
                    ready.Enqueue(dependant);
        }

        if (visited != nodes.Length) throw new InvalidDataException("DAG_PROTOCOL_INVALID: cyclic graph.");
        return Array.AsReadOnly(nodes);
    }
}
