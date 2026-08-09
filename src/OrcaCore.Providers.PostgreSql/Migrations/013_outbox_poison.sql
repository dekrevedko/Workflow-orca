alter table orcacore_outbox
    add column if not exists poison_code text null,
    add column if not exists poison_detail text null;
