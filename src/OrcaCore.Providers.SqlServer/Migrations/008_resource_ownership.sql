if col_length('dbo.orcacore_resource_tickets', 'fiber_id') is null
begin
    alter table dbo.orcacore_resource_tickets add fiber_id nvarchar(256) null;
end;

if col_length('dbo.orcacore_resource_tickets', 'scope_id') is null
begin
    alter table dbo.orcacore_resource_tickets add scope_id nvarchar(256) null;
end;

if col_length('dbo.orcacore_resource_waiters', 'fiber_id') is null
begin
    alter table dbo.orcacore_resource_waiters add fiber_id nvarchar(256) null;
end;

if col_length('dbo.orcacore_resource_waiters', 'scope_id') is null
begin
    alter table dbo.orcacore_resource_waiters add scope_id nvarchar(256) null;
end;
