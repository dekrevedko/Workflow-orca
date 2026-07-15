alter table if exists orcacore_resource_tickets
    add column if not exists fiber_id text null;

alter table if exists orcacore_resource_tickets
    add column if not exists scope_id text null;

alter table if exists orcacore_resource_waiters
    add column if not exists fiber_id text null;

alter table if exists orcacore_resource_waiters
    add column if not exists scope_id text null;
