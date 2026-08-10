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
    continue_as_new_generation integer not null default 0
);

alter table orcacore_checkpoints
    add column if not exists continue_as_new_generation integer not null default 0;

create table if not exists orcacore_inbox (
    instance_id uuid not null,
    event_id text not null,
    envelope_fingerprint text not null,
    state text not null,
    primary key (instance_id, event_id)
);

create table if not exists orcacore_start_idempotency (
    idempotency_key text primary key,
    instance_id uuid not null,
    definition_id uuid not null,
    definition_version integer not null,
    definition_fingerprint text not null,
    input_fingerprint text not null
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
    has_stuck_step boolean not null default false,
    stuck_step_path text null,
    stuck_detected_at timestamp with time zone null,
    saga_audits jsonb not null default '[]'::jsonb
);

alter table orcacore_instance_projections
    add column if not exists parent_instance_id uuid null;

alter table orcacore_instance_projections
    add column if not exists root_instance_id uuid null;

alter table orcacore_instance_projections
    add column if not exists saga_audits jsonb not null default '[]'::jsonb;

alter table orcacore_instance_projections
    add column if not exists continue_as_new_generation integer not null default 0;

alter table orcacore_instance_projections
    add column if not exists archived_at timestamp with time zone null;

create table if not exists orcacore_active_wait_projections (
    wait_id uuid primary key,
    instance_id uuid not null,
    event_name text not null,
    correlation_id text not null,
    registered_at timestamp with time zone not null,
    status text not null,
    mode text not null
);

create index if not exists ix_orcacore_active_wait_lookup
    on orcacore_active_wait_projections (event_name, correlation_id);

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
