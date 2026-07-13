alter table orcacore_outbox
    add column if not exists claimed_until timestamp with time zone null;

create index if not exists ix_orcacore_outbox_claim
    on orcacore_outbox (state, claimed_until, outbox_record_id);

alter table orcacore_timers
    add column if not exists claimed_until timestamp with time zone null;

create index if not exists ix_orcacore_timers_claim
    on orcacore_timers (fire_at, claimed_until, timer_id);
