if object_id(N'{{schema}}.[orcacore_provider_state]', N'U') is null
begin
    create table {{schema}}.[orcacore_provider_state]
    (
        [state_key] nvarchar(128) not null,
        [state_revision] bigint not null,
        [payload_format] int not null,
        [payload_json] nvarchar(max) not null,
        [updated_at] datetimeoffset(7) not null,
        constraint [pk_orcacore_provider_state] primary key ([state_key]),
        constraint [ck_orcacore_provider_state_revision] check ([state_revision] >= 0),
        constraint [ck_orcacore_provider_state_payload_format] check ([payload_format] = 1),
        constraint [ck_orcacore_provider_state_payload_json] check (isjson([payload_json]) = 1)
    );
end;


-- DAG run aggregate: complete first-create schema; no runtime upgrade path.
if object_id(N'{{schema}}.[dag_runs]', N'U') is null
begin
create table {{schema}}.[dag_runs] (
    run_id uniqueidentifier primary key,
    start_key_digest nvarchar(64) collate Latin1_General_100_BIN2 not null unique,
    start_key_text nvarchar(max) collate Latin1_General_100_BIN2 not null,
    creation_sequence bigint identity(1,1) not null unique,
    version bigint not null check (version >= 1),
    definition_id nvarchar(450) collate Latin1_General_100_BIN2 not null,
    definition_version bigint not null check (definition_version > 0),
    definition_fingerprint nvarchar(64) collate Latin1_General_100_BIN2 not null,
    state nvarchar(64) collate Latin1_General_100_BIN2 not null check (state in ('Running','CancellationRequested','Succeeded','Failed','Cancelled','IntegrityFault')),
    last_operational_at datetimeoffset(7) not null,
    is_stuck bit not null default 0,
    last_progress_at datetimeoffset(7) not null,
    graph_payload varbinary(max) not null,
    input_payload varbinary(max) not null,
    checkpoint_payload varbinary(max) not null,
    checkpoint_sha256 nvarchar(64) collate Latin1_General_100_BIN2 not null,
    created_at datetimeoffset(7) not null,
    updated_at datetimeoffset(7) not null,
    completed_at datetimeoffset(7) null,
    owner_id uniqueidentifier null,
    owner_fence bigint not null default 0 check (owner_fence >= 0),
    lease_request_id uniqueidentifier null,
    lease_granted_at datetimeoffset(7) null,
    lease_expires_at datetimeoffset(7) null,
    lease_hard_deadline datetimeoffset(7) null,
    check ((owner_id is null and lease_request_id is null and lease_granted_at is null and lease_expires_at is null and lease_hard_deadline is null) or (owner_id is not null and lease_request_id is not null and lease_granted_at is not null and lease_expires_at is not null and lease_hard_deadline is not null and owner_fence > 0 and lease_expires_at <= lease_hard_deadline and lease_hard_deadline = dateadd(second,15,lease_granted_at)))
);
create index ix_dag_runs_recovery on {{schema}}.[dag_runs] (state,creation_sequence);
end;

if object_id(N'{{schema}}.[dag_run_events]', N'U') is null
begin
create table {{schema}}.[dag_run_events] (
    run_id uniqueidentifier not null references {{schema}}.[dag_runs](run_id),
    stream_version bigint not null check (stream_version > 0),
    operation_id uniqueidentifier not null,
    record_kind nvarchar(64) collate Latin1_General_100_BIN2 not null check (record_kind in ('dag-run-created-v1','dag-node-input-admitted-v1','dag-node-mapping-failed-v1','dag-child-terminal-joined-v1','dag-run-cancellation-requested-v1','dag-run-terminal-v1','dag-run-integrity-faulted-v1','dag-run-poison-reconciled-v1')),
    record_version int not null check (record_version = 1),
    payload varbinary(max) not null,
    checksum nvarchar(64) collate Latin1_General_100_BIN2 not null,
    created_at datetimeoffset(7) not null,
    primary key (run_id,stream_version)
);

end;

if object_id(N'{{schema}}.[dag_run_operations]', N'U') is null
begin
create table {{schema}}.[dag_run_operations] (
    run_id uniqueidentifier not null references {{schema}}.[dag_runs](run_id),
    operation_id uniqueidentifier not null,
    operation_fingerprint nvarchar(64) collate Latin1_General_100_BIN2 not null,
    result_version bigint not null check (result_version > 0),
    receipt_payload varbinary(max) not null,
    primary key (run_id,operation_id)
);

end;

if object_id(N'{{schema}}.[dag_run_nodes]', N'U') is null
begin
create table {{schema}}.[dag_run_nodes] (
    run_id uniqueidentifier not null references {{schema}}.[dag_runs](run_id),
    ordinal int not null check (ordinal >= 0),
    node_id nvarchar(450) collate Latin1_General_100_BIN2 not null,
    binding_payload varbinary(max) not null,
    dependencies_payload varbinary(max) not null,
    state nvarchar(64) collate Latin1_General_100_BIN2 not null check (state in ('Pending','Ready','StartAuthorized','CancellationRequested','Succeeded','Failed','Cancelled','DependencyBlocked')),
    child_id uniqueidentifier null,
    input_present bit not null,
    input_bytes varbinary(max) null,
    input_contract nvarchar(max) collate Latin1_General_100_BIN2 null,
    input_codec nvarchar(64) collate Latin1_General_100_BIN2 null,
    input_fingerprint nvarchar(64) collate Latin1_General_100_BIN2 null,
    check ((input_present = 0 and input_bytes is null and input_contract is null and input_codec is null and input_fingerprint is null) or (input_present = 1 and input_bytes is not null and input_contract is not null and input_codec = 'orcacore-json-v1' and input_fingerprint is not null)),
    output_present bit not null,
    output_bytes varbinary(max) null,
    output_contract nvarchar(max) collate Latin1_General_100_BIN2 null,
    output_codec nvarchar(64) collate Latin1_General_100_BIN2 null,
    output_fingerprint nvarchar(64) collate Latin1_General_100_BIN2 null,
    check ((output_present = 0 and output_bytes is null and output_contract is null and output_codec is null and output_fingerprint is null) or (output_present = 1 and output_bytes is not null and output_contract is not null and output_codec = 'orcacore-json-v1' and output_fingerprint is not null)),
    failure_code nvarchar(128) collate Latin1_General_100_BIN2 null,
    ready_at datetimeoffset(7) null,
    started_at datetimeoffset(7) null,
    completed_at datetimeoffset(7) null,
    primary key (run_id,ordinal),
    unique(run_id,node_id)
);
create unique index uq_dag_run_child on {{schema}}.[dag_run_nodes] (child_id) where child_id is not null;
create index ix_dag_run_nodes_state on {{schema}}.[dag_run_nodes] (run_id,state,ordinal);
end;

if object_id(N'{{schema}}.[dag_run_lease_requests]', N'U') is null
begin
create table {{schema}}.[dag_run_lease_requests] (
    run_id uniqueidentifier not null references {{schema}}.[dag_runs](run_id),
    request_id uniqueidentifier not null,
    queue_sequence bigint identity(1,1) not null unique,
    owner_id uniqueidentifier not null,
    purpose nvarchar(64) collate Latin1_General_100_BIN2 not null check (purpose in ('Admission','ChildJoin','RunCancellation','IntegrityReconciliation')),
    state nvarchar(64) collate Latin1_General_100_BIN2 not null check (state in ('Waiting','Granted','Consumed')),
    wait_expires_at datetimeoffset(7) not null,
    granted_at datetimeoffset(7) null,
    lease_expires_at datetimeoffset(7) null,
    hard_deadline datetimeoffset(7) null,
    granted_token bigint null check (granted_token > 0),
    append_committed bit not null default 0,
    primary key (run_id,request_id)
);
create unique index uq_dag_live_owner on {{schema}}.[dag_run_lease_requests] (run_id,owner_id) where state in ('Waiting','Granted');
create index ix_dag_run_queue on {{schema}}.[dag_run_lease_requests] (run_id,state,queue_sequence,wait_expires_at);
end;

if object_id(N'{{schema}}.[dag_run_subscriptions]', N'U') is null
begin
create table {{schema}}.[dag_run_subscriptions] (
    subscription_id uniqueidentifier primary key,
    recipient_host_id uniqueidentifier not null,
    run_id uniqueidentifier not null references {{schema}}.[dag_runs](run_id),
    after_version bigint not null check (after_version >= 0),
    lease_expires_at datetimeoffset(7) not null
);
create index ix_dag_run_subscriptions_expiry on {{schema}}.[dag_run_subscriptions] (lease_expires_at,run_id);
create index ix_dag_run_subscriptions_run on {{schema}}.[dag_run_subscriptions] (run_id,recipient_host_id);
end;
