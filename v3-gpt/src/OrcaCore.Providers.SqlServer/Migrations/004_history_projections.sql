if object_id('dbo.orcacore_history_projections', 'U') is null
begin
    create table dbo.orcacore_history_projections (
        history_id uniqueidentifier not null primary key,
        instance_id uniqueidentifier not null,
        recorded_at datetimeoffset not null,
        kind nvarchar(256) not null,
        payload nvarchar(max) not null
    );
end;

if not exists (
    select 1
    from sys.indexes
    where name = 'ix_orcacore_history_projections_instance'
      and object_id = object_id('dbo.orcacore_history_projections'))
begin
    create index ix_orcacore_history_projections_instance
        on dbo.orcacore_history_projections (instance_id, recorded_at);
end;
