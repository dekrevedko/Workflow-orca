if col_length('dbo.orcacore_checkpoints', 'runtime_state') is null
begin
    alter table dbo.orcacore_checkpoints add runtime_state varbinary(max) null;
end;
