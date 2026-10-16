using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed class SqlServerStateDocumentStore
{
    internal const string WorkflowStateKey = "workflow";
    internal const string ResourcePoolStateKey = "resource-pools";
    internal const string ResourceGovernanceStateKey = "resource-governance";
    private const int PayloadFormat = 1;
    private const int StateKeyMaxLength = 128;
    private const string StateLockPrefix = "orcacore:state:";
    private const string StateTable = "orcacore_provider_state";
    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private readonly SqlServerSchema schemaOwner;
    private readonly string quotedSchema;
    private bool initialized;

    internal SqlServerStateDocumentStore(string connectionString, string schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ConnectionString = new SqlConnectionStringBuilder(connectionString).ConnectionString;
        Schema = schema;
        quotedSchema = SqlServerSchema.QuoteSchema(schema);
        schemaOwner = new SqlServerSchema(ConnectionString, Schema);
    }

    internal string ConnectionString { get; }

    internal string Schema { get; }

    internal static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref initialized))
        {
            return;
        }

        await initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized)
            {
                return;
            }

            await schemaOwner.InitializeAsync(cancellationToken).ConfigureAwait(false);
            Volatile.Write(ref initialized, true);
        }
        finally
        {
            initializationGate.Release();
        }
    }

    internal async Task<TResult> ExecuteAsync<TState, TResult>(
        string stateKey,
        Func<string?, TState> restore,
        Func<TState, Task<TResult>> operation,
        Func<TState, string> capture,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateKey);
        ArgumentNullException.ThrowIfNull(restore);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(capture);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        await SqlServerSchema.AcquireTransactionLockAsync(
            connection,
            transaction,
            StateLockPrefix + Schema + ":" + stateKey,
            cancellationToken).ConfigureAwait(false);

        var payload = await LoadPayloadAsync(
            connection,
            transaction,
            stateKey,
            cancellationToken).ConfigureAwait(false);
        var state = restore(payload);
        var result = await operation(state).ConfigureAwait(false);
        await SavePayloadAsync(
            connection,
            transaction,
            stateKey,
            capture(state),
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    internal async Task<TResult> ReadAsync<TState, TResult>(
        string stateKey,
        Func<string?, TState> restore,
        Func<TState, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateKey);
        ArgumentNullException.ThrowIfNull(restore);
        ArgumentNullException.ThrowIfNull(operation);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var payload = await LoadPayloadAsync(
            connection,
            transaction: null,
            stateKey,
            cancellationToken).ConfigureAwait(false);
        return await operation(restore(payload)).ConfigureAwait(false);
    }

    private async Task<string?> LoadPayloadAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string stateKey,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var lockHint = transaction is null ? string.Empty : " with (updlock, holdlock)";
        command.CommandText = $"select [payload_json] from {quotedSchema}.[{StateTable}]{lockHint} where [state_key] = @key;";
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, StateKeyMaxLength) { Value = stateKey });
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    private async Task SavePayloadAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string stateKey,
        string payload,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            update {quotedSchema}.[{StateTable}]
            set [state_revision] = [state_revision] + 1,
                [payload_format] = @format,
                [payload_json] = @payload,
                [updated_at] = sysdatetimeoffset()
            where [state_key] = @key;
            if @@rowcount = 0
            begin
                insert into {quotedSchema}.[{StateTable}]
                    ([state_key], [state_revision], [payload_format], [payload_json], [updated_at])
                values (@key, 0, @format, @payload, sysdatetimeoffset());
            end;
            """;
        command.Parameters.Add(new SqlParameter("@key", SqlDbType.NVarChar, StateKeyMaxLength) { Value = stateKey });
        command.Parameters.Add(new SqlParameter("@format", SqlDbType.Int) { Value = PayloadFormat });
        command.Parameters.Add(new SqlParameter("@payload", SqlDbType.NVarChar, -1) { Value = payload });
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static JsonSerializerOptions CreateJsonOptions()
        => new(JsonSerializerDefaults.Web);
}
