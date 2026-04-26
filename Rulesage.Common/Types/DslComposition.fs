namespace Rulesage.Common.Types

type SemanticDslEntry = {
    // Semantic names of dsl needed for building this entry
    useDsls: string list
    // All in the form of key * semantic description
    context: (string * string) list
    produce: (string * string) list
    subtasks: (string * string) list
}
