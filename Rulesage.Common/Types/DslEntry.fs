namespace Rulesage.Common.Types

type DslEntryId = int

type ContextKey = string
type SubtaskKey = string
type ProductionKey = string

type LeafValue =
    | LiteralLeaf of value: string
    // Template of an NL prompt, with placeholders keys in context
    // Produce a literal by prompting an LLM
    | NlLeaf of promptTemplate: string
with
     member x.AsLiteralLeaf() = match x with LeafValue.LiteralLeaf v -> v | _ -> failwith "not literal"
     member x.AsNlLeaf() = match x with LeafValue.NlLeaf t -> t | _ -> failwith "not nl"
    
// The type a context entry will accept
type ContextEntry =
    // accepts a leaf (thus no other specification)
    | LiteralLeaf
    // accepts an AST node with the given signature
    | AstNode of signature: AstNodeSignatureId

// Fill a parameter in an AST signature
// so its type is already defined (as in the singature)
type AstParametersFilling =
    // by filling a leaf value
    | Leaf of value: LeafValue
    // by parameters to fill a literal AST of the required type
    | AstLiteral of value: (AstParamaterKey * AstParametersFilling) list
    | FromContext of key: ContextKey
    | FromSubtask of subtaskKey: SubtaskKey * producedKey: ProductionKey
with
    member x.AsLeaf() = match x with AstParametersFilling.Leaf v -> v | _ -> failwith "not leaf"
    member x.AsAstLiteral() = match x with AstParametersFilling.AstLiteral v -> v | _ -> failwith "not literal ast"
    member x.AsFromContext() = match x with AstParametersFilling.FromContext v -> v | _ -> failwith "not from context"
    member x.AsFromSubtask() = match x with AstParametersFilling.FromSubtask (v1, v2) -> (v1, v2) | _ -> failwith "not from subtask"

type FilledAst = {
    astId: AstNodeSignatureId
    paramaterFillings: (AstParamaterKey * AstParametersFilling) list
}

type Subtask =
    // Call a dsl and pass required contexts
    | DslCall of dslId: DslEntryId * context: (ContextKey * AstParametersFilling) list
    // An NL task expecting typed ASTs production
    | NlTask of taskTemplate: string * expect: (ProductionKey * AstNodeSignatureId) list
with
    member x.AsDslCall() = match x with Subtask.DslCall (v1, v2) -> (v1, v2) | _ -> failwith "not dsl call"
    member x.AsNlTask() = match x with Subtask.NlTask (v1, v2) -> (v1, v2) | _ -> failwith "not nl task"

type DslEntry = {
    id: DslEntryId
    context: (ContextKey * ContextEntry) list
    produce: (ProductionKey * FilledAst) list
    subtasks: (SubtaskKey * Subtask) list
}