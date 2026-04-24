namespace Rulesage.Core.Types

type private DslId = int
type ContextKey = string
type SubtaskKey = string
type ProductionKey = string

type AstSignature = AstNodeSignatureId

type LeafValue =
    | LiteralLeaf of value: string
    // Template of an NL prompt, with placeholders keys in context
    // Produce a literal by prompting an LLM
    | NlLeaf of promptTemplate: string

// The context this dsl expects from the caller
type ContextEntry = ContextKey * AstSignature

// Fill a parameter in an AST signature
// So its type is already defined (as in the singature)
type AstParametersFilling =
    // by filling a leaf value
    | Leaf of value: LeafValue
    // by parameters to fill a literal AST of the required type
    | AstLiteral of value: (AstParamaterKey * AstParametersFilling) list
    | FromContext of key: ContextKey
    | FromSubtask of subtaskKey: SubtaskKey * producedKey: ProductionKey

type FilledAst = AstSignature * (AstParamaterKey * AstParametersFilling) list

type Subtask =
    // Call a dsl and pass required contexts
    | DslCall of dslId: DslId * context: (ContextKey * FilledAst) list
    // An NL task expecting typed ASTs production
    | NlTask of taskTemplate: string * expect: (ProductionKey * AstSignature) list

type DslEntry = {
    id: DslId
    context: ContextEntry list
    produce: (ProductionKey * FilledAst) list
    subtasks: (SubtaskKey * Subtask) list
}