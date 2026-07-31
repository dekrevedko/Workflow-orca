if object_id('dbo.orcacore_resource_pools', 'U') is null
begin
    create table dbo.orcacore_resource_pools (
        pool_name nvarchar(512) not null primary key,
        creation_capacity int not null,
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
        fiber_id nvarchar(256) null,
        scope_id nvarchar(256) null,
        constraint fk_orcacore_resource_tickets_pool foreign key (pool_name)
            references dbo.orcacore_resource_pools(pool_name) on delete cascade
    );
end;

if not exists (
    select 1
    from sys.indexes
    where name = 'ix_orcacore_resource_tickets_pool'
      and object_id = object_id('dbo.orcacore_resource_tickets'))
begin
    create index ix_orcacore_resource_tickets_pool
        on dbo.orcacore_resource_tickets (pool_name);
end;

if not exists (
    select 1
    from sys.indexes
    where name = 'ix_orcacore_resource_tickets_holder'
      and object_id = object_id('dbo.orcacore_resource_tickets'))
begin
    create index ix_orcacore_resource_tickets_holder
        on dbo.orcacore_resource_tickets (holder_instance_id, holder_key);
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
        fiber_id nvarchar(256) null,
        scope_id nvarchar(256) null,
        constraint uq_orcacore_resource_waiters_holder unique (holder_instance_id, holder_key)
    );
end;

if not exists (
    select 1
    from sys.indexes
    where name = 'ix_orcacore_resource_waiters_requested'
      and object_id = object_id('dbo.orcacore_resource_waiters'))
begin
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
