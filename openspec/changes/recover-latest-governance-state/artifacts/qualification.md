# Phase E governance recovery qualification

Reviewed implementation commit `6d7cf078dea9b1fd2df412f33c0a8a3c3a133f30`

Reviewed implementation tree `fd31147335de13c9a9d09f192799fe9750d7b80b`

Comparison base `aba8a6f8c29aca3301c490d99a318db06e268ca8` (includes PR #31).

## Reproduction and implementation

Every governance append at the base already stored the complete partition state. Startup and ordinary operations read every prior record, deserialized it and kept only the final state. The in-memory control, real PostgreSQL and experimental SQL Server all reproduced that work. SQL Server also rewrote its state document during the read.

Each nonempty engine load now selects the committed head, verifies its identity and decodes one v1 state. Each failed append fence causes the command to reload the winning state before retrying. Explicit history inspection still checks the entire record sequence, format and checksums. PostgreSQL selects the head and latest record in one statement. The in-memory provider selects both under one lock.

Independent reviewers exercised the base and the candidate in separate checkouts. The same monotonic-clock schedule passed at both revisions and checked ownership, review marks, confirmation identity and release outcomes across restarts. Separate tests verified resize replay and conflict handling. A skewed clock exposed an existing FIFO defect. Release now honors the committed array order, including for snapshots written by earlier engines. Review also reproduced a malformed owner surviving recovery, a queued request drawing twice on the same pool and a low-level caller mutating an acquire request after validation. Regression tests exercise each fault and fence stale writes.

## Provider limits

Experimental SQL Server still transfers and traverses the entire JSON document to find the selected partition. Reads leave the document and its revision untouched. An append still restores, validates and rewrites all partitions. Consequently a damaged historical record can allow a current read yet prevent a subsequent write, even for a different partition.

New SQL Server writes persist each partition's version. A preexisting document without that field derives its version from the array length. A coherent missing suffix in that legacy representation has no independent evidence to detect it. All providers retain the existing full-state write format. Current snapshot size can grow with retained resize identities, confirmations and other accounting state.

## Measured result

At 256 records and a fixed state, each provider reduced aggregate recovery from 256 decodes and 77,312 bytes to one decode and 302 bytes. With 128 retained resize IDs, the current record grew to 7,724 bytes. PostgreSQL fetched a single indexed record. Experimental SQL Server selected a 285,094-byte document in the fixed case and a 1,561,342-byte document with 128 resize IDs. Its document revision stayed constant on read. Native SQL storage and history-dependent write cost remain with the SQL Server provider owner before promotion.

The [measurement report](recovery-measurements.md) and [samples and query plans](recovery-samples.json) cover both revisions, three providers, six populations and 25 warmed timing samples per cell. Their normalized SHA-256 digests are `3f6fd8b3bc59b39f3ea13956390c6533f12db16ed7f0b995822e5cd0002f9dce` and `c59b253ec57af7e68b7fd187cc9c8b4bc7bccbc1723522cb66e91b54a272cdf0` respectively. Host load varied. SQL client byte counters were partial, so the document-size claim uses the size of the selected column.

## Verification

The Release build used `dotnet build OrcaCore.slnx -c Release -warnaserror`. It completed with zero warnings and errors. A separate checkout of the reviewed commit produced all 12 packages with the correct source commit. Each embedded DLL matched its Release assembly. Public API and package source provenance checks passed. The custom provider example restored its three packages from the source-stamped feed into an empty cache, compiled and ran.

Run each test project with `dotnet test tests/<project> -c Release --no-build`. The Infrastructure lane adds `--filter "Disposition=Infrastructure"` after building the exact package feed. The benchmark smoke command is `dotnet run --project benchmarks/OrcaCore.Benchmarks --no-build -c Release -- --smoke`.

Independent focused runs passed 13 hosting recovery cases, six in-memory/protocol cases, 12 real PostgreSQL governance cases and 14 real SQL Server governance cases. The serialized record produced byte-identical JSON on the base and implementation for empty, four-byte and 1,024-byte payloads. The Section 7 executable scenarios exercise the four handoff barriers, quarantine and truthful settlement.
