alter table orcacore_inbox
    drop constraint if exists orcacore_inbox_pkey;

create sequence if not exists orcacore_inbox_delivery_sequence;

alter table orcacore_inbox
    alter column instance_id drop not null,
    add column if not exists route_key text null,
    add column if not exists acceptance_sequence bigint not null default nextval('orcacore_inbox_delivery_sequence'),
    add column if not exists accepted_at timestamp with time zone not null default '1970-01-01 00:00:00+00',
    add column if not exists poison_code text null,
    add column if not exists poison_detail text null;

alter sequence orcacore_inbox_delivery_sequence
    owned by orcacore_inbox.acceptance_sequence;

drop index if exists ux_orcacore_inbox_event_id;

alter table orcacore_inbox
    add primary key (event_id);

create table if not exists orcacore_inbox_routes (
    route_key text primary key,
    revision bigint not null
);

create index if not exists ix_orcacore_inbox_pending_route_order
    on orcacore_inbox (route_key, state, acceptance_sequence, event_id);
