alter table orcacore_inbox
    alter column acceptance_sequence drop identity if exists;

create sequence if not exists orcacore_inbox_delivery_sequence;

select setval(
    'orcacore_inbox_delivery_sequence',
    greatest(coalesce(max(acceptance_sequence), 0) + 1, 1),
    false)
from orcacore_inbox;

alter table orcacore_inbox
    alter column acceptance_sequence set default nextval('orcacore_inbox_delivery_sequence');

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
