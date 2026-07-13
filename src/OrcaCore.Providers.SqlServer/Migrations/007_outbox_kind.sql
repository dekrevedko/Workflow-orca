-- DR-037: kind-partitioned outbox claims filter on kind; index it so a partitioned
-- claim does not scan the whole outbox.
if not exists (
    select 1
    from sys.indexes
    where name = 'ix_orcacore_outbox_kind'
      and object_id = object_id('dbo.orcacore_outbox'))
begin
    create index ix_orcacore_outbox_kind
        on dbo.orcacore_outbox (kind, state);
end;
