-- DR-037: kind-partitioned outbox claims filter on kind; index it so a partitioned
-- claim does not scan the whole outbox.
create index if not exists ix_orcacore_outbox_kind
    on orcacore_outbox (kind, state);
