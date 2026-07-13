if object_id('dbo.orcacore_events', 'U') is null
begin
    create table dbo.orcacore_events (
        stream_id uniqueidentifier not null,
        version bigint not null,
        event_id uniqueidentifier not null,
        event_type nvarchar(256) not null,
        occurred_at datetimeoffset not null,
        payload nvarchar(max) not null,
        constraint pk_orcacore_events primary key (stream_id, version),
        constraint uq_orcacore_events_event_id unique (event_id)
    );
end;

if object_id('dbo.orcacore_checkpoints', 'U') is null
begin
    create table dbo.orcacore_checkpoints (
        instance_id uniqueidentifier not null primary key,
        stream_version bigint not null,
        content_type nvarchar(256) not null,
        payload varbinary(max) not null,
        definition_id uniqueidentifier null,
        definition_version int null,
        status nvarchar(64) null,
        last_step_path nvarchar(512) null,
        error_summary nvarchar(max) null,
        outcome_name nvarchar(256) null,
        continue_as_new_generation int not null
    );
end;

if object_id('dbo.orcacore_inbox', 'U') is null
begin
    create table dbo.orcacore_inbox (
        event_id uniqueidentifier not null primary key,
        state nvarchar(64) not null
    );
end;

if object_id('dbo.orcacore_start_idempotency', 'U') is null
begin
    create table dbo.orcacore_start_idempotency (
        idempotency_key nvarchar(512) not null primary key,
        instance_id uniqueidentifier not null,
        definition_id uniqueidentifier not null,
        definition_version int not null
    );
end;

if object_id('dbo.orcacore_outbox', 'U') is null
begin
    create table dbo.orcacore_outbox (
        outbox_record_id uniqueidentifier not null primary key,
        instance_id uniqueidentifier not null,
        kind nvarchar(256) not null,
        payload varbinary(max) not null,
        state nvarchar(64) not null,
        claimed_until datetimeoffset null
    );
    create index ix_orcacore_outbox_state on dbo.orcacore_outbox (state);
    create index ix_orcacore_outbox_claim on dbo.orcacore_outbox (state, claimed_until, outbox_record_id);
end;

if object_id('dbo.orcacore_instance_projections', 'U') is null
begin
    create table dbo.orcacore_instance_projections (
        instance_id uniqueidentifier not null primary key,
        parent_instance_id uniqueidentifier null,
        root_instance_id uniqueidentifier null,
        definition_id uniqueidentifier not null,
        definition_version int not null,
        status nvarchar(64) not null,
        created_at datetimeoffset not null,
        updated_at datetimeoffset not null,
        error_summary nvarchar(max) null,
        outcome_name nvarchar(256) null,
        continue_as_new_generation int not null,
        archived_at datetimeoffset null,
        saga_audits nvarchar(max) not null
    );
end;

if object_id('dbo.orcacore_active_wait_projections', 'U') is null
begin
    create table dbo.orcacore_active_wait_projections (
        wait_id uniqueidentifier not null primary key,
        instance_id uniqueidentifier not null,
        event_name nvarchar(256) not null,
        correlation_id nvarchar(512) not null,
        registered_at datetimeoffset not null,
        status nvarchar(64) not null,
        mode nvarchar(64) not null
    );
end;

if object_id('dbo.orcacore_timers', 'U') is null
begin
    create table dbo.orcacore_timers (
        timer_id uniqueidentifier not null primary key,
        instance_id uniqueidentifier not null,
        command_id uniqueidentifier not null,
        fire_at datetimeoffset not null,
        wakeup_name nvarchar(256) not null,
        claimed bit not null default 0,
        claimed_until datetimeoffset null
    );
    create index ix_orcacore_timers_due on dbo.orcacore_timers (fire_at, timer_id);
    create index ix_orcacore_timers_claim on dbo.orcacore_timers (fire_at, claimed_until, timer_id);
end;

if object_id('dbo.orcacore_resource_pools', 'U') is null
begin
    create table dbo.orcacore_resource_pools (
        pool_name nvarchar(512) not null primary key,
        capacity int not null,
        lease_duration_seconds int null
    );
end;

if object_id('dbo.orcacore_resource_tickets', 'U') is null
begin
    create table dbo.orcacore_resource_tickets (
        ticket_id uniqueidentifier not null primary key,
        pool_name nvarchar(512) not null,
        holder_instance_id uniqueidentifier not null,
        holder_key nvarchar(512) not null,
        ticket_count int not null,
        acquired_at datetimeoffset not null,
        expires_at datetimeoffset null,
        constraint fk_orcacore_resource_tickets_pool foreign key (pool_name)
            references dbo.orcacore_resource_pools(pool_name) on delete cascade
    );
    create index ix_orcacore_resource_tickets_pool on dbo.orcacore_resource_tickets (pool_name);
    create index ix_orcacore_resource_tickets_holder on dbo.orcacore_resource_tickets (holder_instance_id, holder_key);
end;

if object_id('dbo.orcacore_resource_waiters', 'U') is null
begin
    create table dbo.orcacore_resource_waiters (
        waiter_id uniqueidentifier not null primary key,
        holder_instance_id uniqueidentifier not null,
        holder_key nvarchar(512) not null,
        requirements nvarchar(max) not null,
        requested_at datetimeoffset not null,
        expires_at datetimeoffset null,
        constraint uq_orcacore_resource_waiters_holder unique (holder_instance_id, holder_key)
    );
    create index ix_orcacore_resource_waiters_requested
        on dbo.orcacore_resource_waiters (requested_at, waiter_id);
end;

if object_id('dbo.orcacore_resource_expired_tickets', 'U') is null
begin
    create table dbo.orcacore_resource_expired_tickets (
        ticket_id uniqueidentifier not null primary key,
        pool_name nvarchar(512) not null,
        ticket nvarchar(max) not null,
        expired_at datetimeoffset not null
    );
end;

if object_id('dbo.orcacore_resource_audit', 'U') is null
begin
    create table dbo.orcacore_resource_audit (
        audit_id uniqueidentifier not null primary key,
        pool_name nvarchar(512) not null,
        action nvarchar(128) not null,
        reason nvarchar(max) not null,
        occurred_at datetimeoffset not null,
        ticket nvarchar(max) null
    );
end;
