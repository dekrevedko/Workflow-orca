if col_length('dbo.orcacore_instance_projections', 'stream_version') is null
begin
    alter table dbo.orcacore_instance_projections add stream_version bigint null;
end;
