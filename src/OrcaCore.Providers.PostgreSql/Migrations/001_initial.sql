create table if not exists orcacore_events (
    stream_id uuid not null,
    version bigint not null,
    event_id text not null,
    event_type text not null,
    occurred_at timestamp with time zone not null,
    payload jsonb not null,
    primary key (stream_id, version),
    unique (event_id)
);

create index if not exists ix_orcacore_events_stream_id_version
    on orcacore_events (stream_id, version);

create table if not exists orcacore_stream_heads (
    instance_id uuid primary key,
    stream_version bigint not null check (stream_version > 0)
);

create table if not exists orcacore_checkpoints (
    instance_id uuid primary key,
    stream_version bigint not null,
    content_type text not null,
    payload bytea not null,
    definition_id uuid null,
    definition_version integer null,
    status text null,
    last_step_path text null,
    error_summary text null,
    outcome_name text null,
    continue_as_new_generation integer not null default 0,
    runtime_state jsonb null
);

create sequence if not exists orcacore_inbox_delivery_sequence;

create table if not exists orcacore_inbox (
    event_id text primary key,
    instance_id uuid null,
    envelope_fingerprint text not null,
    state text not null,
    event_name text null,
    event_contract_version integer null,
    correlation_id text null,
    causation_event_id text null,
    occurred_at timestamp with time zone null,
    route_kind text null,
    route_instance_id uuid null,
    route_definition_id uuid null,
    route_definition_version integer null,
    start_idempotency_key text null,
    workflow_input_content_type text null,
    workflow_input_payload bytea null,
    payload_content_type text null,
    payload bytea null,
    route_key text null,
    acceptance_sequence bigint not null default nextval('orcacore_inbox_delivery_sequence'),
    accepted_at timestamp with time zone not null default '1970-01-01 00:00:00+00',
    poison_code text null,
    poison_detail text null,
    handoff_failure_count integer not null default 0,
    handoff_retry_not_before timestamp with time zone null
);

alter sequence orcacore_inbox_delivery_sequence
    owned by orcacore_inbox.acceptance_sequence;

create index if not exists ix_orcacore_inbox_pending_route_order
    on orcacore_inbox (route_key, state, acceptance_sequence, event_id);

create index if not exists ix_orcacore_inbox_received_order
    on orcacore_inbox (state, acceptance_sequence, event_id)
    include (handoff_failure_count, handoff_retry_not_before);

create index if not exists ix_orcacore_inbox_handoff_retry
    on orcacore_inbox (state, handoff_retry_not_before, acceptance_sequence, event_id)
    where handoff_failure_count > 0;

create index if not exists ix_orcacore_inbox_pending_start
    on orcacore_inbox (route_kind, start_idempotency_key, state, acceptance_sequence, event_id);

create table if not exists orcacore_inbox_routes (
    route_key text primary key,
    revision bigint not null
);

create table if not exists orcacore_inbox_fanout_targets (
    event_id text not null references orcacore_inbox(event_id),
    instance_id uuid not null,
    state text not null,
    route_key text not null,
    acceptance_sequence bigint not null default nextval('orcacore_inbox_delivery_sequence'),
    poison_code text null,
    poison_detail text null,
    handoff_failure_count integer not null default 0,
    handoff_retry_not_before timestamp with time zone null,
    primary key (event_id, instance_id),
    unique (acceptance_sequence)
);

create index if not exists ix_orcacore_inbox_fanout_pending_route_order
    on orcacore_inbox_fanout_targets (route_key, state, acceptance_sequence, event_id, instance_id);

create index if not exists ix_orcacore_inbox_fanout_received_order
    on orcacore_inbox_fanout_targets (state, acceptance_sequence, event_id, instance_id)
    include (handoff_failure_count, handoff_retry_not_before);

create index if not exists ix_orcacore_inbox_fanout_handoff_retry
    on orcacore_inbox_fanout_targets (
        state,
        handoff_retry_not_before,
        acceptance_sequence,
        event_id,
        instance_id)
    where handoff_failure_count > 0;

create table if not exists orcacore_start_idempotency (
    idempotency_key text primary key,
    instance_id uuid not null,
    definition_id uuid not null,
    definition_version integer not null,
    definition_fingerprint text not null,
    input_fingerprint text not null
);

create table if not exists orcacore_pending_start_intents (
    start_idempotency_key text primary key,
    definition_id uuid not null,
    definition_version integer not null,
    workflow_input_content_type text not null,
    workflow_input_payload bytea not null,
    workflow_input_fingerprint text not null,
    state text not null,
    instance_id uuid null,
    definition_fingerprint text null,
    poison_code text null,
    poison_detail text null
);

create table if not exists orcacore_outbox (
    outbox_record_id uuid primary key,
    instance_id uuid not null,
    kind text not null,
    payload bytea not null,
    state text not null,
    claimed_until timestamp with time zone null,
    poison_code text null,
    poison_detail text null
);

create index if not exists ix_orcacore_outbox_state
    on orcacore_outbox (state);

create index if not exists ix_orcacore_outbox_claim
    on orcacore_outbox (state, claimed_until, outbox_record_id);

create index if not exists ix_orcacore_outbox_kind
    on orcacore_outbox (kind, state);

create table if not exists orcacore_instance_projections (
    instance_id uuid primary key,
    parent_instance_id uuid null,
    root_instance_id uuid null,
    definition_id uuid not null,
    definition_version integer not null,
    status text not null,
    created_at timestamp with time zone not null,
    updated_at timestamp with time zone not null,
    error_summary text null,
    outcome_name text null,
    continue_as_new_generation integer not null default 0,
    archived_at timestamp with time zone null,
    last_active_at timestamp with time zone null,
    is_stuck boolean not null default false,
    stuck_detected_at timestamp with time zone null,
    saga_audits jsonb not null default '[]'::jsonb,
    stream_version bigint null
);

create table if not exists orcacore_active_wait_projections (
    wait_id uuid primary key,
    instance_id uuid not null,
    event_name text not null,
    event_contract_version integer not null default 1,
    correlation_id text not null,
    registered_at timestamp with time zone not null,
    status text not null,
    mode text not null
);

create index if not exists ix_orcacore_active_wait_lookup
    on orcacore_active_wait_projections (event_name, event_contract_version, correlation_id);

create table if not exists orcacore_timers (
    timer_id uuid primary key,
    instance_id uuid not null,
    command_id uuid not null,
    fire_at timestamp with time zone not null,
    wakeup_name text not null,
    claimed boolean not null default false,
    claimed_until timestamp with time zone null
);

create index if not exists ix_orcacore_timers_due
    on orcacore_timers (claimed, fire_at);

create index if not exists ix_orcacore_timers_claim
    on orcacore_timers (fire_at, claimed_until, timer_id);

create table if not exists orcacore_history_projections (
    history_id uuid primary key,
    instance_id uuid not null,
    recorded_at timestamp with time zone not null,
    kind text not null,
    payload jsonb not null
);

create index if not exists ix_orcacore_history_projections_instance
    on orcacore_history_projections (instance_id, recorded_at);

create table if not exists orcacore_resource_governance_streams (
    partition_id text primary key,
    version bigint not null check (version >= 0)
);

create table if not exists orcacore_resource_governance_records (
    partition_id text not null references orcacore_resource_governance_streams(partition_id)
        on delete cascade,
    sequence bigint not null check (sequence > 0),
    format_id text not null,
    payload bytea not null,
    checksum text not null,
    primary key (partition_id, sequence)
);
