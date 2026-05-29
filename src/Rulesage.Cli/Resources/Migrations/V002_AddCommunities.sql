create table if not exists communities
(
    id                   varchar(256) primary key,
    parent_id            varchar(256) not null,
    annotation           text         not null,
    annotation_embedding vector(384)
);

alter table records
    add column if not exists community_id varchar(256) not null default '';
alter table records
    drop column if exists community;

alter table actions
    add column if not exists community_id varchar(256) not null default '';
alter table actions
    drop column if exists community;

alter table rules
    add column if not exists community_id varchar(256) not null default '';
alter table rules
    drop column if exists community;

create index if not exists idx_records_community_id on records (community_id);
create index if not exists idx_actions_community_id on actions (community_id);
create index if not exists idx_rules_community_id on rules (community_id);