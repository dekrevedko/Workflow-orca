## Why

The v1 requirements call SQL Server a production provider, while governance documentation already describes it as experimental. A schema-wide lock serializes ordinary workflow mutations and governance reads can transfer an entire history document. There is no qualified SQL Server scale envelope.

## What Changes

- Publish the accepted provider tiers for the next release. PostgreSQL is the production storage target, SQL Server ships as experimental, and in-memory is for development and tests.
- Require complete real-storage correctness certification and package consumers for both relational providers, including restart and competing hosts.
- Require a separate storage and scale qualification before promoting SQL Server.

## Capabilities

### Modified Capabilities

- `durable-persistence-and-outbox`: Identifies the support tier and retains schema, pressure and messaging obligations.
- `developer-facing-surface`: Gives each provider registration its correct tier.
- `repository-foundation`: Retains both packages, their role ownership and clean consumers.
- `quality-and-verification`: Retains both relational certification lanes after the support change.

## Impact

The change affects the numbered requirements, capability matrix, current guidance and candidate package information. Both adapters retain their APIs, schemas and CI coverage.
