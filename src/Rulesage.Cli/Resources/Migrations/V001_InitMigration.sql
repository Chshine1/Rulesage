CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS operations (
    id                 INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ir                 VARCHAR(64) NOT NULL,
    description        TEXT NOT NULL,
    embedding          VECTOR(384) NOT NULL,
    level              REAL NOT NULL,
    signature_params   JSONB NOT NULL,
    signature_outputs  JSONB NOT NULL,
    subtasks           JSONB NOT NULL,
    outputs            JSONB NOT NULL
);

CREATE TABLE IF NOT EXISTS nodes (
    id                 INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    ir                 VARCHAR(64) NOT NULL,
    description        TEXT NOT NULL,
    embedding          VECTOR(384) NOT NULL,
    parameters         JSONB NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_operations_ir_unique ON operations (ir);
CREATE UNIQUE INDEX IF NOT EXISTS idx_nodes_ir_unique ON nodes (ir);

CREATE INDEX IF NOT EXISTS idx_operations_embedding_hnsw
    ON operations USING hnsw (embedding vector_cosine_ops);
CREATE INDEX IF NOT EXISTS idx_nodes_embedding_hnsw
    ON nodes USING hnsw (embedding vector_cosine_ops);