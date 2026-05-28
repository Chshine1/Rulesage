create extension if not exists vector;

create table if not exists records
(
    id                   varchar(128) primary key,
    community            varchar(128) not null,
    annotation           text         not null,
    annotation_embedding vector(384)  not null,
    generic_params       jsonb        not null,
    fors                 jsonb        not null
);

create table if not exists actions
(
    id                   varchar(128) primary key,
    community            varchar(128) not null,
    annotation           text         not null,
    annotation_embedding vector(384)  not null,
    generic_params       jsonb        not null,
    fors                 jsonb        not null,
    returns              jsonb        not null,
    script               text         not null
);

create table if not exists rules
(
    id                   varchar(128) primary key,
    community            varchar(128) not null,
    annotation           text         not null,
    annotation_embedding vector(384)  not null,
    fors                 jsonb        not null,
    givens               jsonb        not null,
    must_be              jsonb        not null
);

create index if not exists idx_records_embedding_hnsw on records using hnsw (annotation_embedding vector_cosine_ops);
create index if not exists idx_actions_embedding_hnsw on actions using hnsw (annotation_embedding vector_cosine_ops);
create index if not exists idx_rules_embedding_hnsw on rules using hnsw (annotation_embedding vector_cosine_ops);
