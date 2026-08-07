alter table orcacore_inbox
    add column if not exists event_name text null,
    add column if not exists event_contract_version integer null,
    add column if not exists correlation_id text null,
    add column if not exists causation_event_id text null,
    add column if not exists occurred_at timestamp with time zone null,
    add column if not exists route_kind text null,
    add column if not exists route_instance_id uuid null,
    add column if not exists route_definition_id uuid null,
    add column if not exists route_definition_version integer null,
    add column if not exists start_idempotency_key text null,
    add column if not exists workflow_input_content_type text null,
    add column if not exists workflow_input_payload bytea null,
    add column if not exists payload_content_type text null,
    add column if not exists payload bytea null;

create unique index if not exists ux_orcacore_inbox_event_id
    on orcacore_inbox (event_id);

alter table orcacore_active_wait_projections
    add column if not exists event_contract_version integer not null default 1;

drop index if exists ix_orcacore_active_wait_lookup;

create index if not exists ix_orcacore_active_wait_lookup
    on orcacore_active_wait_projections (event_name, event_contract_version, correlation_id);
