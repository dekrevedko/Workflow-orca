# First usable addressed DAG wake qualification

The reviewed implementation is `bc4ab313a124874481312d2b738a796f36ceca6c` (tree `ad5932964169d42866febccb2c79bd95c2207dff`). The approved contract is `20203c48`. PR-012a and the synchronized outbox delta own the provider rule.

## Forced schedules

All 19 focused cases passed with the Release build on .NET SDK 10.0.403. The in-memory provider ran five cases. PostgreSQL 17 and SQL Server 2022 each ran seven against actual databases in Linux containers. The three suites share the assertions in [DagRunCertification.FirstUsableWake.cs](../../../../tests/OrcaCore.ProviderCertification/DagRunCertification.FirstUsableWake.cs).

| First operation | Competing operation | Observed result |
| --- | --- | --- |
| Renew from cursor 1 to head 3 | Append version 4 | Cursor 3 and its shortened lease survive. Version 4 replaces the claimed version 2 under the same ID. The old attempt cannot acknowledge, release or poison before or after reclaim. |
| Append version 4 | Renew at version 3 | Renewal returns `AlreadyChanged(4)`. Cursor 1 and its previous expiry survive. The second store reads committed head 4. The original version 2 claim still settles. |
| Acknowledge version 2 | Append version 3 | The dispatched row keeps its history. The append creates a fresh pending ID. |
| Append version 3 | Acknowledge version 2 | The useful claim remains valid and its acknowledgement leaves no extra pending notice. |

For cursor races the relational tests hold a run row, start the first provider call, observe its database blocker, then start the second call and verify it blocks behind the first. The settlement tests apply the same two-edge check to outbox or document ownership. A separate monitoring connection verifies the lock chain between the named writer, reader and blocker sessions. The in-memory cursor tests hold the existing provider clock call inside its mutation lock until the competing thread waits. Both in-memory orderings passed five additional repeats.

The shared tests also verify lease expiry and reclaim, unchanged payload after a consumer mutates its own claim, preservation of poison and independent explicit records, operation replay, conflicting subscription ownership and atomic rollback on both same-batch and stored identity collisions. Running the five new memory cases against product code from `2e1afcc2` produced four failures. Both cursor schedules accumulated two pending notices alongside the original claim, the same-batch collision committed, and a later append made another addressed notice claimable during the first claim’s lease. The existing stored-collision check passed on that base.

## Reproduction

From the repository root, build the Release solution. Run each project below with `dotnet test <project> -c Release --no-build --filter FullyQualifiedName~DagFirstUsableWake`. Both relational projects start their own Testcontainers.

- `tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj`
- `tests/OrcaCore.Providers.PostgreSql.Tests/OrcaCore.Providers.PostgreSql.Tests.csproj`
- `tests/OrcaCore.Providers.SqlServer.Tests/OrcaCore.Providers.SqlServer.Tests.csproj`

## Validation

A fresh checkout of the implementation built in Release with warnings as errors (zero warnings and errors). The counts distinguish passing cases from cases the test adapter listed without executing.

| Suite | Passed | Listed without execution |
| --- | ---: | ---: |
| Core | 408 | 0 |
| Ephemeral | 477 | 0 |
| Durable | 158 | 0 |
| Hosting | 127 | 42 |
| Acceptance | 37 | 0 |
| In-memory and shared certification | 162 | 0 |
| PostgreSQL | 173 | 1 |
| SQL Server | 144 | 1 |
| Integration | 11 | 0 |
| Infrastructure | 323 | 0 |

The exact local feed packed all twelve packages. Strict OpenSpec validation passed all 35 items. The canonical gate recorded 244 rows, 65,321 bytes and zero pending operations at synchronization.

## Independent review

The contract reviewer traced the predecessor's per-append obligation. A second reviewer reconstructed the pending provenance row and ran the canonical gate. Source review reproduced a mutable in-memory claim and found the behind-head cross-run identity check. Both fixes are included in the implementation checkpoint. A reviewer independent of the test author inspected the four ordering schedules, reran all five in-memory cases, and checked the real-provider results by test identity.

The storage bound covers live provider-generated addressed hints for each registration. Append still examines the eligible subscribers. The change requires fresh prerelease relational schemas.
