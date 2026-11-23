using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrcaCore.Hosting;
using OrcaCore.Hosting.ResourceLeases;
using OrcaCore.Durable.Hosting;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.InMemory;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.Providers.SqlServer;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

if (args.Length != 2 || args[0] is not ("mem" or "pg" or "sql") || args[1] is not ("timing" or "decode"))
    throw new ArgumentException("Expected provider (mem, pg or sql) and mode (timing or decode).");
var provider = args[0];
var measureDecodes = args[1] == "decode";
var token = CancellationToken.None;
var pool = ResourcePoolName.Create("cpu");
var profile = new DurableEngineHostOptions
{
    StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 1, StepThrottles = [] },
    ResourcePools = new DurableResourcePoolOptions
    {
        PartitionId = ResourceGovernancePartitionId.Create("bench"),
        Pools = [DurableResourcePoolDefinition.Create(pool, 1, TimeSpan.FromMinutes(5))]
    }
};
var partition = profile.ResourcePools.PartitionId;
var bootstrap = new ServiceCollection();
bootstrap.AddLogging();
bootstrap.AddOrcaCoreInMemoryDurableProvider();
bootstrap.AddOrcaCoreDurableEngine(profile);
await using var templateProvider = bootstrap.BuildServiceProvider();
var management = templateProvider.GetRequiredService<IDurableResourcePoolManagement>();
await management.GetAsync(pool);
await management.ResizeAsync(pool, 1, ResourcePoolOperationId.Create("template"));
var originals = await templateProvider.GetRequiredService<IDurableResourceGovernanceStore>().LoadAsync(partition);
var empty = originals.Records[0];
var withResize = originals.Records[1];
await using var pg = provider == "pg" ? new PostgreSqlBuilder("postgres:17-alpine").Build() : null;
await using var sql = provider == "sql" ? new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build() : null;
if (pg is not null) await pg.StartAsync();
if (sql is not null) await sql.StartAsync();
var writer = new JsonSerializerOptions { WriteIndented = false };
foreach (var (length, identities) in new[] { (1, 0), (64, 0), (256, 0), (256, 16), (256, 64), (256, 128) })
{
    var schema = $"bench_{length}_{identities}";
    var services = new ServiceCollection();
    services.AddLogging();
    if (pg is not null)
    {
        await using var connection = new NpgsqlConnection(pg.GetConnectionString());
        await connection.OpenAsync();
        await using var create = new NpgsqlCommand($"create schema {schema}", connection);
        await create.ExecuteNonQueryAsync();
        services.AddOrcaCorePostgreSqlDurableProvider(new(pg.GetConnectionString(), schema));
    }
    else if (sql is not null) services.AddOrcaCoreSqlServerDurableProvider(new(sql.GetConnectionString(), schema));
    else services.AddOrcaCoreInMemoryDurableProvider();
    var original = services.Single(d => d.ServiceType == typeof(IDurableResourceGovernanceStore));
    services.AddSingleton(sp => new MeasuredStore((IDurableResourceGovernanceStore)original.ImplementationFactory!(sp)));
    services.AddSingleton<IDurableResourceGovernanceStore>(sp => sp.GetRequiredService<MeasuredStore>());
    services.AddOrcaCoreDurableEngine(profile);
    await using var owner = services.BuildServiceProvider();
    var store = owner.GetRequiredService<MeasuredStore>();
    var ops = owner.GetRequiredService<IDurableResourcePoolManagement>();
    var initialize = ops.GetType().GetMethod("InitializeAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
        .CreateDelegate<Func<CancellationToken, ValueTask>>(ops);
    var data = new List<ResourceGovernanceRecord>();
    var resize = JsonNode.Parse(withResize.Payload.Span)!.AsObject();
    var operations = resize["State"]!["ResizeOperations"]!.AsArray();
    var item = operations.Single()!.DeepClone().AsObject();
    operations.Clear();
    for (var i = 1; i <= length; i++)
    {
        var payload = empty.Payload;
        if (i > length - identities)
        {
            var next = item.DeepClone().AsObject();
            next["OperationId"] = $"op-{i:D6}";
            operations.Add(next);
            payload = JsonSerializer.SerializeToUtf8Bytes(resize, writer);
        }
        data.Add(ResourceGovernanceRecord.FromPersisted(i, empty.FormatId, payload, Convert.ToHexString(SHA256.HashData(payload.Span)).ToLowerInvariant()));
    }
    if (await store.AppendAsync(partition, 0, data, token) is not ResourceGovernanceAppendResult.Committed committed || committed.Version != length)
        throw new InvalidOperationException("The seed batch was not committed at the expected version.");
    await initialize(token);
    await initialize(token);
    if ((await ops.GetAsync(pool)).ConfiguredCapacity != 1)
        throw new InvalidOperationException("Recovered a different capacity from the seeded value.");
    var before = await DocAsync();
    store.Counting = true;
    var decoded = 0;
    var decodedBytes = 0L;
    AppContext.SetData("OrcaCore.MeasureGovernanceDecode", (Action<int>)(size => { decoded++; decodedBytes += size; }));
    var wire = sql is null ? null : new SqlTransfer();
    using (wire) await initialize(token);
    AppContext.SetData("OrcaCore.MeasureGovernanceDecode", null);
    store.Counting = false;
    var after = await DocAsync();
    var times = new List<double>();
    var bytes = new List<long>();
    var count = measureDecodes ? 1 : 25;
    if (store.Calls != 1 || (measureDecodes && (decoded != store.Count || decodedBytes != store.Bytes)))
        throw new InvalidOperationException($"Observed {store.Calls} port calls returning {store.Count}/{store.Bytes} records/bytes; decoded {decoded}/{decodedBytes}.");
    for (var n = 0; n < count; n++)
    {
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var started = Stopwatch.GetTimestamp();
        await initialize(token);
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var allocated = GC.GetTotalAllocatedBytes(true) - allocatedBefore;
        times.Add(elapsed);
        bytes.Add(allocated);
    }
    using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach (var record in data) digest.AppendData(record.Payload.Span);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        provider,
        runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        length,
        identities,
        historyBytes = data.Sum(x => (long)x.Payload.Length),
        headBytes = data[^1].Payload.Length,
        historySha256 = Convert.ToHexString(digest.GetHashAndReset()),
        returnedRecords = store.Count,
        returnedPayloadBytes = store.Bytes,
        store.Calls,
        decoded = measureDecodes ? (int?)decoded : null,
        decodedBytes = measureDecodes ? (long?)decodedBytes : null,
        wholeSqlDocumentBytes = before.bytes,
        sqlRevisionDelta = after.revision - before.revision,
        sqlConnections = wire?.Closed,
        sqlWireReceived = wire?.Received,
        sqlWireSent = wire?.Sent,
        samples = times.Count,
        latencyP50ms = Q(times, .5),
        latencyP95ms = Q(times, .95),
        allocP50Bytes = Q(bytes, .5),
        allocP95Bytes = Q(bytes, .95),
        rawUs = times.Select(x => Math.Round(x * 1000)).ToArray(),
        rawBytes = bytes
    }));
    if (pg is not null && length == 256 && identities == 0)
    {
        await using var connection = new NpgsqlConnection(pg.GetConnectionString());
        await connection.OpenAsync();
        await new NpgsqlCommand($"set search_path={schema}; analyze orcacore_resource_governance_records; analyze orcacore_resource_governance_streams", connection).ExecuteNonQueryAsync();
        // Execute the current revision's published SQL verbatim (substitute bind parameters using Npgsql).
        var providerSource = await File.ReadAllTextAsync("src/OrcaCore.Providers.PostgreSql/PostgreSqlResourceGovernanceStore.cs");
#if LATEST
        const string loadMethod = " LoadLatestAsync(";
#else
        const string loadMethod = " LoadAsync(";
#endif
        var method = providerSource.IndexOf(loadMethod, StringComparison.Ordinal);
        if (method < 0) throw new InvalidOperationException("Cannot locate PostgreSQL recovery method.");
        var open = providerSource.IndexOf("\"\"\"", method, StringComparison.Ordinal);
        var close = providerSource.IndexOf("\"\"\"", open + 3, StringComparison.Ordinal);
        if (open < 0 || close < 0) throw new InvalidOperationException("Cannot extract recovery SQL.");
        var sqlText = providerSource[(open + 3)..close].Trim();
        await PrintPlanAsync("PLAN");
        await using var addHeads = new NpgsqlCommand("insert into orcacore_resource_governance_streams(partition_id,version) select 'other-' || i,0 from generate_series(1,1024) i; analyze orcacore_resource_governance_streams", connection);
        await addHeads.ExecuteNonQueryAsync();
        await PrintPlanAsync("MANY_HEADS_PLAN");
        async Task PrintPlanAsync(string label)
        {
            await using var q = new NpgsqlCommand("explain (analyze,buffers,format text) " + sqlText, connection);
            q.Parameters.AddWithValue("partition_id", partition.Value);
            await using var rows = await q.ExecuteReaderAsync();
            while (await rows.ReadAsync()) Console.WriteLine(label + " " + rows.GetString(0));
        }
    }
    async Task<(long bytes, long revision)> DocAsync()
    {
        if (sql is null) return (0, 0);
        await using var connection = new SqlConnection(sql.GetConnectionString());
        await connection.OpenAsync();
        await using var cmd = new SqlCommand($"select cast(datalength(payload_json) as bigint), state_revision from [{schema}].orcacore_provider_state where state_key='resource-governance'", connection);
        await using var rows = await cmd.ExecuteReaderAsync();
        if (!await rows.ReadAsync()) throw new Exception("missing state doc");
        return (rows.GetInt64(0), rows.GetInt64(1));
    }
}
static T Q<T>(List<T> sorted, double q) => sorted.Order().ElementAt((int)Math.Ceiling(q * sorted.Count) - 1);

sealed class SqlTransfer : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
{
    private readonly List<IDisposable> subscriptions = [];
    internal long Sent { get; private set; }
    internal long Received { get; private set; }
    internal int Closed { get; private set; }
    internal SqlTransfer() { subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this)); }
    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "SqlClientDiagnosticListener")
            subscriptions.Add(listener.Subscribe(this, s => s.EndsWith("ConnectionOpenAfter", StringComparison.Ordinal) || s.EndsWith("ConnectionCloseBefore", StringComparison.Ordinal)));
    }
    public void OnNext(KeyValuePair<string, object?> ev)
    {
        if (ev.Value?.GetType().GetProperty("Connection")?.GetValue(ev.Value) is not SqlConnection conn) return;
        if (ev.Key.EndsWith("OpenAfter", StringComparison.Ordinal))
        {
            conn.StatisticsEnabled = true;
            conn.ResetStatistics();
        }
        else
        {
            var stats = conn.RetrieveStatistics();
            Received += Convert.ToInt64(stats["BytesReceived"]);
            Sent += Convert.ToInt64(stats["BytesSent"]);
            Closed++;
        }
    }
    public void OnCompleted() { }
    public void OnError(Exception ex) => throw ex;
    public void Dispose() { foreach (var handle in subscriptions) handle.Dispose(); }
}

sealed class MeasuredStore(IDurableResourceGovernanceStore inner) : IDurableResourceGovernanceStore
{
    internal bool Counting { get; set; }
    internal int Count { get; private set; }
    internal long Bytes { get; private set; }
    internal int Calls { get; private set; }
    public async ValueTask<ResourceGovernanceStream> LoadAsync(ResourceGovernancePartitionId partitionId, CancellationToken cancellationToken = default)
    {
        var stream = await inner.LoadAsync(partitionId, cancellationToken);
        if (Counting)
        {
            Calls++;
            Count += stream.Records.Count;
            Bytes += stream.Records.Sum(record => (long)record.Payload.Length);
        }
        return stream;
    }
#if LATEST
    public async ValueTask<ResourceGovernanceHead> LoadLatestAsync(ResourceGovernancePartitionId partitionId, CancellationToken cancellationToken = default)
    {
        var head = await inner.LoadLatestAsync(partitionId, cancellationToken);
        if (Counting)
        {
            Calls++;
            Count += head.Record is null ? 0 : 1;
            Bytes += head.Record?.Payload.Length ?? 0;
        }
        return head;
    }
#endif
    public ValueTask<ResourceGovernanceAppendResult> AppendAsync(ResourceGovernancePartitionId partitionId, long expectedVersion, IReadOnlyList<ResourceGovernanceRecord> records, CancellationToken cancellationToken = default)
        => inner.AppendAsync(partitionId, expectedVersion, records, cancellationToken);
}
