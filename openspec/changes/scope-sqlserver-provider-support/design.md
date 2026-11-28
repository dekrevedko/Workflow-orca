## Context

SQL Server implements the durable role and passes the shared and database-native correctness lanes. Ordinary workflow state and governance use documents. Independent workloads can contend for the same document lock, and a latest-only engine read can still transfer retained governance history. DAG streams have their own relational tables.

## Decision

Keep the twelve-package manifest and both relational certification lanes. Use PostgreSQL as the v1 production storage target. Mark SQL Server experimental wherever a consumer chooses a provider, including the package itself. In-memory remains a development and test provider. Describe these as source and next-release tiers; published preview evidence retains its original scope.

## Promotion

A future owner can propose indexed SQL Server storage and measure unrelated-stream concurrency, recovery bytes as history grows, and tail latency against a stated workload. A support-tier promotion requires independent review of those results. The current measurements qualify no SQL Server operating envelope.
