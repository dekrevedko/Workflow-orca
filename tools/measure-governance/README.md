# Governance recovery measurement

The PowerShell runner creates a disposable checkout of the requested commit, builds for .NET 10 in Release and runs the in-memory provider, PostgreSQL 17 Alpine and experimental SQL Server 2022. The two database cases require Docker with Linux containers. The repository and its selected checkout are read-only inputs. The runner prints the path of its scratch checkout and retains the full logs in that checkout.

Run once for the baseline and once for the accepted implementation. Supply an absolute path to a compatible `dotnet` executable when it is outside PATH.

```powershell
./tools/measure-governance/Measure.ps1 -Repository (Get-Location).Path -Revision aba8a6f8
./tools/measure-governance/Measure.ps1 -Repository (Get-Location).Path -Revision <implementation-commit>
```

Each provider runs in a separate process; each population uses an isolated in-memory store or database schema. The seeded records reuse the production serializer's pool-created and pool-resized shapes. Fixed-size histories contain identical 302-byte complete states at 1, 64 and 256 records. At 256 records, the three growing-state populations accumulate one distinct resize ID per record over the last 16, 64 or 128 records. Sequences and checksums are valid. The synthetic repeats hold current content constant; seed creation and append lie outside measured recovery. Each sample calls the production startup `InitializeAsync` on the same initialized provider, so driver/container/schema startup and cold caches are excluded. A separate public management query checks recovered capacity.

In `timing-*.log`, JSON rows include 25 individual elapsed microsecond and managed-byte samples, with nearest-rank P50 and P95. The stopwatch covers the asynchronous recovery call and process-wide GC allocation counters bracket it. A separate recovery counts returned record count/payload bytes. SQL Server's per-connection statistics measure bytes received/sent over that operation; an administrative query independently measures `DATALENGTH(payload_json)` and the change in `state_revision`. These are document and SQL-client protocol bytes, not physical disk I/O. PostgreSQL prints an ordinary `EXPLAIN (ANALYZE, BUFFERS)` of the actual provider query at 256 constant-size records after `ANALYZE`, without disabling sequential scans or forcing indexes.

SQL-client statistics can miss protocol bytes in the observation window. The driver reported zero in some cells where the query demonstrably returned a nonempty document. Treat zero sent or received as unavailable, retain the raw counters and use the column size and revision to establish document selection and rewrites. Actual network savings cannot be inferred from these protocol counters.

The same PostgreSQL plan is captured again after seeding 1,024 extra empty partition heads and analyzing the head table, outside the timed region. This distinguishes the planner's one-row head scan from its lookup when many partitions exist.

In `decode-*.log` the script adds a counter immediately before the aggregate deserializes a governance envelope in its disposable checkout. For each population it verifies the actual count and total payload bytes against what the provider returned. This pass is separate from timing. It does not count outer SQL Server document parsing as aggregate state decoding. The updated SQL Server read can transfer its whole document while passing one envelope to the runtime. Payload SHA-256 covers the concatenated record payloads in sequence order so data shape can be compared across runs.
