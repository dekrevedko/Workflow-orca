create table if not exists orcacore_start_idempotency (
    idempotency_key text primary key,
    instance_id uuid not null,
    definition_id uuid not null,
    definition_version integer not null
);
