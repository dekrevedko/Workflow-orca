using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed partial class ProtectedStepLeaseRecoveryTests
{
    // Test-owned tables implement one external resource in the same disposable database.
    // It permits concurrent jobs. Its own token fence is the sole reason to reject a submission.
    private sealed class JobResource(string provider, string connectionString)
    {
        private DbConnection Connect() => provider == "PostgreSql"
            ? new NpgsqlConnection(connectionString) : new SqlConnection(connectionString);

        internal async Task InitializeAsync(CancellationToken token)
        {
            await using var connection = Connect();
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE test_job_resource (id int PRIMARY KEY, revision int NOT NULL, fenced_token varchar(512) NULL);
                INSERT INTO test_job_resource (id, revision) VALUES (1, 0);
                CREATE TABLE test_job_submission (revision int PRIMARY KEY, lease_token varchar(512) NOT NULL,
                    operation_id varchar(512) NOT NULL, attempt int NOT NULL, worker_pid int NOT NULL,
                    accepted int NOT NULL, active int NOT NULL);
                """;
            await command.ExecuteNonQueryAsync(token);
        }

        internal async Task<int> FenceAsync(string lease, CancellationToken token)
        {
            await using var connection = Connect();
            await connection.OpenAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            Add(command, "@lease", lease);
            // Fence and submit take the same exclusive row lock through commit. The active-job
            // check rejects a fence-only proof if an earlier accepted job still needs draining.
            command.CommandText = """
                UPDATE test_job_resource SET revision = revision + 1, fenced_token = @lease
                WHERE id = 1 AND fenced_token IS NULL;
                """;
            Assert.Equal(1, await command.ExecuteNonQueryAsync(token));
            command.CommandText = "SELECT COUNT(*) FROM test_job_submission WHERE lease_token = @lease AND active = 1";
            Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync(token)));
            command.CommandText = "SELECT revision FROM test_job_resource WHERE id = 1 AND fenced_token = @lease";
            var revision = Convert.ToInt32(await command.ExecuteScalarAsync(token));
            await transaction.CommitAsync(token);
            return revision;
        }

        internal async Task SubmitAsync(Admission admission, CancellationToken token)
        {
            await using var connection = Connect();
            await connection.OpenAsync(token);
            await using var transaction = await connection.BeginTransactionAsync(token);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE test_job_resource SET revision = revision + 1 WHERE id = 1";
            Assert.Equal(1, await command.ExecuteNonQueryAsync(token));
            Add(command, "@lease", admission.Lease);
            Add(command, "@operation", admission.Operation);
            Add(command, "@attempt", admission.Attempt);
            Add(command, "@pid", Environment.ProcessId);
            command.CommandText = """
                INSERT INTO test_job_submission (revision, lease_token, operation_id, attempt, worker_pid, accepted, active)
                SELECT revision, @lease, @operation, @attempt, @pid,
                    CASE WHEN fenced_token = @lease THEN 0 ELSE 1 END,
                    CASE WHEN fenced_token = @lease THEN 0 ELSE 1 END
                FROM test_job_resource WHERE id = 1 AND NOT EXISTS
                    (SELECT 1 FROM test_job_submission WHERE lease_token = @lease AND operation_id = @operation)
                """;
            // A replay of the same logical operation observes its existing job under the resource row lock.
            Assert.InRange(await command.ExecuteNonQueryAsync(token), 0, 1);
            await transaction.CommitAsync(token);
        }

        internal async Task FinishAsync(Admission work, CancellationToken token)
        {
            await using var connection = Connect();
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            Add(command, "@lease", work.Lease);
            Add(command, "@operation", work.Operation);
            // Every returning submitter finishes or observes the same accepted job already finished.
            command.CommandText = """
                UPDATE test_job_submission SET active = 0
                WHERE lease_token = @lease AND operation_id = @operation AND accepted = 1
                """;
            Assert.Equal(1, await command.ExecuteNonQueryAsync(token));
        }

        internal async Task<List<Job>> ReadAsync(CancellationToken token)
        {
            await using var connection = Connect();
            await connection.OpenAsync(token);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT revision, lease_token, operation_id, attempt, worker_pid, accepted, active
                FROM test_job_submission ORDER BY revision
                """;
            await using var rows = await command.ExecuteReaderAsync(token);
            var jobs = new List<Job>();
            while (await rows.ReadAsync(token))
                jobs.Add(new(rows.GetInt32(0), rows.GetString(1), rows.GetString(2), rows.GetInt32(3),
                    rows.GetInt32(4), rows.GetInt32(5) == 1, rows.GetInt32(6) == 1));
            return jobs;
        }

        private static void Add(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }

    private sealed record Job(int Revision, string Lease, string Operation, int Attempt, int Pid, bool Accepted, bool Active);
}
