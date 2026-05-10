namespace Rulesage.Common.Types.Domain

type Identifier = string

type ParamType =
    | Literal
    | Node of nodeType: Identifier
    | Array of paramType: ParamType

type Node =
    {
        id: Identifier
        description: string
        parameters: Map<string, ParamType>
    }

type Derivation =
    {
        id: Identifier
        description: string
        parameters: Map<string, ParamType>
        outputs: Map<string, ParamType>
    }

type RuleSignature =
    {
        id: Identifier
        description: string
        level: float32
        parameters: Map<string, ParamType>
    }
