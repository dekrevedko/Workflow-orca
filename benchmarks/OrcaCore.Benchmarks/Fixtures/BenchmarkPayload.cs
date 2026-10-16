namespace OrcaCore.Benchmarks.Fixtures;

public sealed record BenchmarkPayload(
    int Sequence,
    string Name,
    IReadOnlyList<int> Values,
    IReadOnlyDictionary<string, string> Tags);
