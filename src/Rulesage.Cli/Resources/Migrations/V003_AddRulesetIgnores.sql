alter table records
    add column if not exists ignore boolean not null default false;

alter table actions
    add column if not exists ignore boolean not null default false;

alter table rules
    add column if not exists ignore boolean not null default false;