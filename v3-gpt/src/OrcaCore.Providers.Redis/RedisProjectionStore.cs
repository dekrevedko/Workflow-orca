using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StackExchange.Redis;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.Redis;

/// <summary>
/// Provides Redis-profile projection/cache behavior without implementing durable event-store semantics.
/// </summary>
public sealed class RedisProjectionStore : IWorkflowProjectionStore
{
    private const string InstanceIndexKey = "orcacore:projection:instances";
    private const string SnapshotValuePrefix = "orcacore:v1:";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private readonly object gate = new();
    private readonly Dictionary<InstanceId, WorkflowInstanceSnapshot> summaries = [];
    private readonly IDatabase? redisDatabase;

    /// <summary>
    /// Initializes a projection store using the in-process cache profile.
    /// </summary>
    public RedisProjectionStore()
    {
    }

    /// <summary>
    /// Initializes a projection store bound to a StackExchange.Redis database adapter.
    /// </summary>
    public RedisProjectionStore(IDatabase redisDatabase)
    {
        ArgumentNullException.ThrowIfNull(redisDatabase);
        this.redisDatabase = redisDatabase;
    }

    /// <summary>
    /// Gets whether this instance was created with a Redis database adapter.
    /// </summary>
    public bool UsesRedisAdapter => redisDatabase is not null;

    /// <inheritdoc />
    public async Task ApplyAsync(IReadOnlyList<ProjectionWrite> operations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);
        cancellationToken.ThrowIfCancellationRequested();

        if (redisDatabase is { } database)
        {
            foreach (var operation in operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (operation.InstanceSnapshot is not { } snapshot)
                {
                    continue;
                }

                await UpsertRedisAsync(database, operation.InstanceId, snapshot).ConfigureAwait(false);
            }

            return;
        }

        lock (gate)
        {
            foreach (var operation in operations)
            {
                if (operation.InstanceSnapshot is { } snapshot)
                {
                    summaries[operation.InstanceId] = CloneSnapshot(snapshot);
                }
            }
        }

    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (redisDatabase is { } database)
        {
            return await ListRedisAsync(database, query, cancellationToken).ConfigureAwait(false);
        }

        lock (gate)
        {
            return
                summaries.Values
                    .Where(snapshot => Matches(snapshot, query))
                    .Select(CloneSnapshot)
                    .ToArray();
        }
    }

    /// <inheritdoc />
    public async Task<int> CountAsync(WorkflowProjectionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (redisDatabase is { } database)
        {
            var snapshots = await ListRedisAsync(database, query, cancellationToken).ConfigureAwait(false);
            return snapshots.Count;
        }

        lock (gate)
        {
            return summaries.Values.Count(snapshot => Matches(snapshot, query));
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActiveWaitSnapshot>> ListActiveWaitsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        var snapshots = await ListAsync(query, cancellationToken).ConfigureAwait(false);
        return snapshots
            .SelectMany(snapshot => snapshot.ActiveWaits)
            .Select(wait => wait with { })
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<WorkflowStatistics> GetStatisticsAsync(
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        var snapshots = await ListAsync(query, cancellationToken).ConfigureAwait(false);
        return new WorkflowStatistics
        {
            Groups = snapshots
                .GroupBy(snapshot => new
                {
                    snapshot.DefinitionId,
                    snapshot.DefinitionVersion,
                    snapshot.Status
                })
                .Select(group => new WorkflowStatisticsGroup
                {
                    DefinitionId = group.Key.DefinitionId,
                    DefinitionVersion = group.Key.DefinitionVersion,
                    Status = group.Key.Status,
                    Count = group.Count()
                })
                .ToArray()
        };
    }

    private static bool Matches(WorkflowInstanceSnapshot snapshot, WorkflowProjectionQuery query)
    {
        return (query.InstanceId is null || snapshot.InstanceId == query.InstanceId) &&
            (query.ParentInstanceId is null || snapshot.ParentInstanceId == query.ParentInstanceId) &&
            (query.RootInstanceId is null || snapshot.RootInstanceId == query.RootInstanceId) &&
            (query.DefinitionId is null || snapshot.DefinitionId == query.DefinitionId) &&
            (query.DefinitionVersion is null || snapshot.DefinitionVersion == query.DefinitionVersion) &&
            (query.Status is null || snapshot.Status == query.Status) &&
            (query.ActiveWaitEventName is null || snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, query.ActiveWaitEventName, StringComparison.Ordinal))) &&
            (query.ActiveWaitCorrelationId is null || snapshot.ActiveWaits.Any(wait =>
                wait.CorrelationId == query.ActiveWaitCorrelationId));
    }

    private static WorkflowInstanceSnapshot CloneSnapshot(WorkflowInstanceSnapshot snapshot)
    {
        return snapshot with
        {
            ActiveWaits = snapshot.ActiveWaits.Select(wait => wait with { }).ToArray(),
            SagaAudits = snapshot.SagaAudits.Select(scope => scope with
            {
                ForwardActions = scope.ForwardActions.Select(action => action with { }).ToArray(),
                CompensationActions = scope.CompensationActions.Select(action => action with { }).ToArray(),
                RecoveryInterventions = scope.RecoveryInterventions.Select(intervention => intervention with { }).ToArray()
            }).ToArray()
        };
    }

    private static async Task<IReadOnlyList<WorkflowInstanceSnapshot>> ListRedisAsync(
        IDatabase database,
        WorkflowProjectionQuery query,
        CancellationToken cancellationToken)
    {
        if (query.InstanceId is { } instanceId)
        {
            var value = await database.StringGetAsync(SnapshotKey(instanceId)).ConfigureAwait(false);
            var snapshot = TryDeserializeSnapshot(value);
            return snapshot is not null && Matches(snapshot, query)
                ? [CloneSnapshot(snapshot)]
                : [];
        }

        var candidateIndexKeys = CandidateIndexKeys(query).ToArray();
        var members = candidateIndexKeys.Length switch
        {
            0 => await database.SetMembersAsync(InstanceIndexKey).ConfigureAwait(false),
            1 => await database.SetMembersAsync(candidateIndexKeys[0]).ConfigureAwait(false),
            _ => await database.SetCombineAsync(SetOperation.Intersect, candidateIndexKeys).ConfigureAwait(false)
        };
        if (members.Length == 0)
        {
            return [];
        }

        var keys = members
            .Select(SnapshotKeyFromMember)
            .Where(key => key is not null)
            .Select(key => (RedisKey)key!)
            .ToArray();
        if (keys.Length == 0)
        {
            return [];
        }

        var values = await database.StringGetAsync(keys).ConfigureAwait(false);
        var snapshots = new List<WorkflowInstanceSnapshot>();
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = TryDeserializeSnapshot(value);
            if (snapshot is not null && Matches(snapshot, query))
            {
                snapshots.Add(CloneSnapshot(snapshot));
            }
        }

        return snapshots;
    }

    private static async Task UpsertRedisAsync(
        IDatabase database,
        InstanceId instanceId,
        WorkflowInstanceSnapshot snapshot)
    {
        var snapshotKey = SnapshotKey(instanceId);
        var oldValue = await database.StringGetAsync(snapshotKey).ConfigureAwait(false);
        if (TryDeserializeSnapshot(oldValue) is { } oldSnapshot)
        {
            await RemoveIndexMembershipsAsync(database, oldSnapshot).ConfigureAwait(false);
        }

        await database.StringSetAsync(snapshotKey, SerializeSnapshot(snapshot)).ConfigureAwait(false);
        await database.SetAddAsync(InstanceIndexKey, InstanceMember(instanceId)).ConfigureAwait(false);
        await AddIndexMembershipsAsync(database, snapshot).ConfigureAwait(false);
    }

    private static async Task AddIndexMembershipsAsync(
        IDatabase database,
        WorkflowInstanceSnapshot snapshot)
    {
        var member = InstanceMember(snapshot.InstanceId);
        await database.SetAddAsync(DefinitionIndexKey(snapshot.DefinitionId), member).ConfigureAwait(false);
        await database.SetAddAsync(StatusIndexKey(snapshot.Status), member).ConfigureAwait(false);
        if (snapshot.RootInstanceId is { } rootInstanceId)
        {
            await database.SetAddAsync(RootIndexKey(rootInstanceId), member).ConfigureAwait(false);
        }

        if (snapshot.ParentInstanceId is { } parentInstanceId)
        {
            await database.SetAddAsync(ParentIndexKey(parentInstanceId), member).ConfigureAwait(false);
        }
    }

    private static async Task RemoveIndexMembershipsAsync(
        IDatabase database,
        WorkflowInstanceSnapshot snapshot)
    {
        var member = InstanceMember(snapshot.InstanceId);
        await database.SetRemoveAsync(DefinitionIndexKey(snapshot.DefinitionId), member).ConfigureAwait(false);
        await database.SetRemoveAsync(StatusIndexKey(snapshot.Status), member).ConfigureAwait(false);
        if (snapshot.RootInstanceId is { } rootInstanceId)
        {
            await database.SetRemoveAsync(RootIndexKey(rootInstanceId), member).ConfigureAwait(false);
        }

        if (snapshot.ParentInstanceId is { } parentInstanceId)
        {
            await database.SetRemoveAsync(ParentIndexKey(parentInstanceId), member).ConfigureAwait(false);
        }
    }

    private static IEnumerable<RedisKey> CandidateIndexKeys(WorkflowProjectionQuery query)
    {
        if (query.DefinitionId is { } definitionId)
        {
            yield return DefinitionIndexKey(definitionId);
        }

        if (query.Status is { } status)
        {
            yield return StatusIndexKey(status);
        }

        if (query.RootInstanceId is { } rootInstanceId)
        {
            yield return RootIndexKey(rootInstanceId);
        }

        if (query.ParentInstanceId is { } parentInstanceId)
        {
            yield return ParentIndexKey(parentInstanceId);
        }
    }

    private static string SerializeSnapshot(WorkflowInstanceSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);
        var checksum = SHA256.HashData(payload);
        return string.Concat(
            SnapshotValuePrefix,
            Convert.ToBase64String(checksum),
            ":",
            Convert.ToBase64String(payload));
    }

    private static WorkflowInstanceSnapshot? TryDeserializeSnapshot(RedisValue value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var text = value.ToString();
        try
        {
            if (!text.StartsWith(SnapshotValuePrefix, StringComparison.Ordinal))
            {
                return JsonSerializer.Deserialize<WorkflowInstanceSnapshot>(text, JsonOptions);
            }

            var envelope = text[SnapshotValuePrefix.Length..];
            var separator = envelope.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0 || separator == envelope.Length - 1)
            {
                return null;
            }

            var expectedChecksum = Convert.FromBase64String(envelope[..separator]);
            var payload = Convert.FromBase64String(envelope[(separator + 1)..]);
            var actualChecksum = SHA256.HashData(payload);
            if (!CryptographicOperations.FixedTimeEquals(expectedChecksum, actualChecksum))
            {
                return null;
            }

            return JsonSerializer.Deserialize<WorkflowInstanceSnapshot>(payload, JsonOptions);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string SnapshotKey(InstanceId instanceId)
    {
        return $"orcacore:projection:instance:{instanceId.Value:N}";
    }

    private static string? SnapshotKeyFromMember(RedisValue member)
    {
        return Guid.TryParse(member.ToString(), out var instanceId)
            ? SnapshotKey(new InstanceId(instanceId))
            : null;
    }

    private static RedisValue InstanceMember(InstanceId instanceId)
    {
        return instanceId.Value.ToString("N");
    }

    private static RedisKey DefinitionIndexKey(DefinitionId definitionId)
    {
        return $"orcacore:projection:index:definition:{definitionId.Value:N}";
    }

    private static RedisKey StatusIndexKey(WorkflowStatus status)
    {
        return $"orcacore:projection:index:status:{status}";
    }

    private static RedisKey RootIndexKey(InstanceId rootInstanceId)
    {
        return $"orcacore:projection:index:root:{rootInstanceId.Value:N}";
    }

    private static RedisKey ParentIndexKey(InstanceId parentInstanceId)
    {
        return $"orcacore:projection:index:parent:{parentInstanceId.Value:N}";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }
}
