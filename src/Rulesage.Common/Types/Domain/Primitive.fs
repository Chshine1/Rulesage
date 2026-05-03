namespace Rulesage.Common.Types.Domain

type Identifier = { id: int; ir: string }

type ParamType =
    | Leaf
    | Node of nodeType: Identifier

type Node =
    {
        id: Identifier
        description: string
        parameters: Map<string, ParamType>
    }

type Converter =
    {
        id: Identifier
        description: string
        parameters: Map<string, ParamType>
        outputs: Map<string, ParamType>
    }

type OperationSignature =
    {
        id: Identifier
        description: string
        level: float32
        parameters: Map<string, ParamType>
        outputs: Map<string, ParamType>
    }
