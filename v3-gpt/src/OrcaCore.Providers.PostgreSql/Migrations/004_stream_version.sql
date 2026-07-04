alter table orcacore_instance_projections
    add column if not exists stream_version bigint;
