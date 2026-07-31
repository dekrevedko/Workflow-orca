create table if not exists orcacore_resource_governance_streams (
    partition_id text primary key,
    version bigint not null check (version >= 0)
);

create table if not exists orcacore_resource_governance_records (
    partition_id text not null references orcacore_resource_governance_streams(partition_id)
        on delete cascade,
    sequence bigint not null check (sequence > 0),
    format_id text not null,
    payload bytea not null,
    checksum text not null,
    primary key (partition_id, sequence)
);
