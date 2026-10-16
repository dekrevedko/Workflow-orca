if object_id(N'{{schema}}.[orcacore_provider_state]', N'U') is null
begin
    create table {{schema}}.[orcacore_provider_state]
    (
        [state_key] nvarchar(128) not null,
        [state_revision] bigint not null,
        [payload_format] int not null,
        [payload_json] nvarchar(max) not null,
        [updated_at] datetimeoffset(7) not null,
        constraint [pk_orcacore_provider_state] primary key ([state_key]),
        constraint [ck_orcacore_provider_state_revision] check ([state_revision] >= 0),
        constraint [ck_orcacore_provider_state_payload_format] check ([payload_format] = 1),
        constraint [ck_orcacore_provider_state_payload_json] check (isjson([payload_json]) = 1)
    );
end;
