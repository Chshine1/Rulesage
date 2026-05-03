module Rulesage.Common.Db.Schemas

/// <summary>
/// Table: operations
/// F# types: OperationBlueprint(subtasks, outputs) + OperationSignature(parameters, outputs)
///
/// Fields map:
///   id                ->  Identifier.id
///   ir                ->  Identifier.ir
///   signature_params  ->  Map&lt;string, ParamType&gt; (OperationSignature.parameters)
///   signature_outputs ->  Map&lt;string, ParamType&gt; (OperationSignature.outputs)
///   subtasks          ->  Map&lt;string, Subtask&gt; (OperationBlueprint.subtasks)
///   outputs           ->  Map&lt;string, NodeBlueprint&gt; (OperationBlueprint.outputs)
/// </summary>
let createOperationsTableSql =
    """
    CREATE TABLE operations (
        id                 INT  GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        ir                 VARCHAR(64) NOT NULL,
        description        TEXT NOT NULL,
        embedding          VECTOR(384) NOT NULL,
        level              REAL NOT NULL,
        signature_params   JSONB NOT NULL,
        signature_outputs  JSONB NOT NULL,
        subtasks           JSONB NOT NULL,
        outputs            JSONB NOT NULL
    );
    """

/// <summary>
/// Table: operations
/// F# types: OperationBlueprint(subtasks, outputs) + OperationSignature(parameters, outputs)
///
/// Fields map:
///   id                ->  Identifier.id
///   ir                ->  Identifier.ir
///   parameters ->  Map&lt;string, ParamType&gt; (OperationSignature.outputs)
/// </summary>
let createNodesTableSql =
    """
    CREATE TABLE nodes (
        id                 INT  GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        ir                 VARCHAR(64) NOT NULL,
        description        TEXT NOT NULL,
        embedding          VECTOR(384) NOT NULL,
        parameters         JSONB NOT NULL
    );
    """

/// <summary>
/// Table: converters
/// F# type: Converter
///
/// Fields map:
///   id         ->  Identifier.id
///   ir         ->  Identifier.ir
///   parameters ->  Map&lt;string, ParamType&gt; (Converter.parameters)
///   outputs    ->  Map&lt;string, ParamType&gt; (Converter.outputs)
/// </summary>
let createConvertersTableSql =
    """
    CREATE TABLE converters (
        id           INT  GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
        ir           VARCHAR(64) NOT NULL,
        description  TEXT NOT NULL,
        embedding    VECTOR(384) NOT NULL,
        parameters   JSONB NOT NULL,
        outputs      JSONB NOT NULL
    );
    """
