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
    dag_run_owner uuid null,
    recipient_host_id uuid null,
    dag_run_subscription_id uuid null,
    instance_id uuid not null,
    kind text not null,
    payload bytea not null,
    state text not null,
    dispatch_attempt integer not null default 0,
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


-- DAG run aggregate: complete first-create schema; no runtime upgrade path.
create table if not exists dag_runs (
    run_id uuid primary key,
    start_key_digest char(64) collate "C" not null unique check (start_key_digest ~ '^[0-9a-f]{64}$'),
    start_key_text text collate "C" not null,
    creation_sequence bigint generated always as identity not null unique,
    version bigint not null check (version >= 1),
    definition_id text collate "C" not null,
    definition_version bigint not null check (definition_version > 0),
    definition_fingerprint char(64) collate "C" not null check (definition_fingerprint ~ '^[0-9A-F]{64}$'),
    state text collate "C" not null check (state in ('Running','CancellationRequested','Succeeded','Failed','Cancelled','IntegrityFault')),
    last_operational_at timestamp with time zone not null,
    is_stuck boolean not null default false,
    last_progress_at timestamp with time zone not null,
    graph_payload bytea not null,
    input_payload bytea not null,
    checkpoint_payload bytea not null,
    checkpoint_sha256 char(64) collate "C" not null check (checkpoint_sha256 ~ '^[0-9a-f]{64}$'),
    created_at timestamp with time zone not null,
    updated_at timestamp with time zone not null,
    completed_at timestamp with time zone null,
    owner_id uuid null,
    owner_fence bigint not null default 0 check (owner_fence >= 0),
    lease_request_id uuid null,
    lease_granted_at timestamp with time zone null,
    lease_expires_at timestamp with time zone null,
    lease_hard_deadline timestamp with time zone null,
    check ((owner_id is null and lease_request_id is null and lease_granted_at is null and lease_expires_at is null and lease_hard_deadline is null) or (owner_id is not null and lease_request_id is not null and lease_granted_at is not null and lease_expires_at is not null and lease_hard_deadline is not null and owner_fence > 0 and lease_expires_at <= lease_hard_deadline and lease_hard_deadline = lease_granted_at + interval '15 seconds'))
);
create index if not exists ix_dag_runs_recovery on dag_runs (state,creation_sequence);

create table if not exists dag_run_events (
    run_id uuid not null references dag_runs(run_id),
    stream_version bigint not null check (stream_version > 0),
    operation_id uuid not null,
    record_kind text collate "C" not null check (record_kind in ('dag-run-created-v1','dag-node-input-admitted-v1','dag-node-mapping-failed-v1','dag-child-terminal-joined-v1','dag-run-cancellation-requested-v1','dag-run-terminal-v1','dag-run-integrity-faulted-v1','dag-run-poison-reconciled-v1')),
    record_version int not null check (record_version = 1),
    payload bytea not null,
    checksum char(64) collate "C" not null check (checksum ~ '^[0-9a-f]{64}$'),
    created_at timestamp with time zone not null,
    primary key (run_id,stream_version)
);


create table if not exists dag_run_operations (
    run_id uuid not null references dag_runs(run_id),
    operation_id uuid not null,
    operation_fingerprint char(64) collate "C" not null check (operation_fingerprint ~ '^[0-9a-f]{64}$'),
    result_version bigint not null check (result_version > 0),
    receipt_payload bytea not null,
    primary key (run_id,operation_id)
);


create table if not exists dag_run_nodes (
    run_id uuid not null references dag_runs(run_id),
    ordinal int not null check (ordinal >= 0),
    node_id text collate "C" not null,
    binding_payload bytea not null,
    dependencies_payload bytea not null,
    state text collate "C" not null check (state in ('Pending','Ready','StartAuthorized','CancellationRequested','Succeeded','Failed','Cancelled','DependencyBlocked')),
    child_id uuid null,
    input_present boolean not null,
    input_bytes bytea null,
    input_contract text collate "C" null,
    input_codec text collate "C" null,
    input_fingerprint char(64) collate "C" null check (input_fingerprint ~ '^[0-9A-F]{64}$'),
    check ((input_present = false and input_bytes is null and input_contract is null and input_codec is null and input_fingerprint is null) or (input_present = true and input_bytes is not null and input_contract is not null and input_codec = 'orcacore-json-v1' and input_fingerprint is not null)),
    output_present boolean not null,
    output_bytes bytea null,
    output_contract text collate "C" null,
    output_codec text collate "C" null,
    output_fingerprint char(64) collate "C" null check (output_fingerprint ~ '^[0-9A-F]{64}$'),
    check ((output_present = false and output_bytes is null and output_contract is null and output_codec is null and output_fingerprint is null) or (output_present = true and output_bytes is not null and output_contract is not null and output_codec = 'orcacore-json-v1' and output_fingerprint is not null)),
    failure_code text collate "C" null,
    ready_at timestamp with time zone null,
    started_at timestamp with time zone null,
    completed_at timestamp with time zone null,
    primary key (run_id,ordinal),
    unique(run_id,node_id)
);
create unique index if not exists uq_dag_run_child on dag_run_nodes (child_id) where child_id is not null;
create index if not exists ix_dag_run_nodes_state on dag_run_nodes (run_id,state,ordinal);

create table if not exists dag_run_lease_requests (
    run_id uuid not null references dag_runs(run_id),
    request_id uuid not null,
    queue_sequence bigint generated always as identity not null unique,
    owner_id uuid not null,
    purpose text collate "C" not null check (purpose in ('Admission','ChildJoin','RunCancellation','IntegrityReconciliation')),
    state text collate "C" not null check (state in ('Waiting','Granted','Consumed')),
    wait_expires_at timestamp with time zone not null,
    granted_at timestamp with time zone null,
    lease_expires_at timestamp with time zone null,
    hard_deadline timestamp with time zone null,
    granted_token bigint null check (granted_token > 0),
    append_committed boolean not null default false,
    primary key (run_id,request_id)
);
create unique index if not exists uq_dag_live_owner on dag_run_lease_requests (run_id,owner_id) where state in ('Waiting','Granted');
create index if not exists ix_dag_run_queue on dag_run_lease_requests (run_id,state,queue_sequence,wait_expires_at);

create table if not exists dag_run_subscriptions (
    subscription_id uuid primary key,
    recipient_host_id uuid not null,
    run_id uuid not null references dag_runs(run_id),
    after_version bigint not null check (after_version >= 0),
    lease_expires_at timestamp with time zone not null
);
create index if not exists ix_dag_run_subscriptions_expiry on dag_run_subscriptions (lease_expires_at,run_id);
create index if not exists ix_dag_run_subscriptions_run on dag_run_subscriptions (run_id,recipient_host_id);

create index if not exists ix_orcacore_outbox_dag_recipient on orcacore_outbox (kind,state,recipient_host_id,claimed_until);
create index if not exists ix_orcacore_outbox_dag_observer on orcacore_outbox (dag_run_owner,dag_run_subscription_id,recipient_host_id);
