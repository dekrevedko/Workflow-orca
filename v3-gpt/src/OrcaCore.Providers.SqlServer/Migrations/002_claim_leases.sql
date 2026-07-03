if col_length('dbo.orcacore_outbox', 'claimed_until') is null
begin
    alter table dbo.orcacore_outbox
        add claimed_until datetimeoffset null;
end;

if not exists (
    select 1
    from sys.indexes
    where name = 'ix_orcacore_outbox_claim'
      and object_id = object_id('dbo.orcacore_outbox'))
begin
    create index ix_orcacore_outbox_claim
        on dbo.orcacore_outbox (state, claimed_until, outbox_record_id);
end;

if col_length('dbo.orcacore_timers', 'claimed') is null
begin
    alter table dbo.orcacore_timers
        add claimed bit not null
            constraint df_orcacore_timers_claimed default 0;
end;

if col_length('dbo.orcacore_timers', 'claimed_until') is null
begin
    alter table dbo.orcacore_timers
        add claimed_until datetimeoffset null;
end;

if not exists (
    select 1
    from sys.indexes
    where name = 'ix_orcacore_timers_claim'
      and object_id = object_id('dbo.orcacore_timers'))
begin
    create index ix_orcacore_timers_claim
        on dbo.orcacore_timers (fire_at, claimed_until, timer_id);
end;
