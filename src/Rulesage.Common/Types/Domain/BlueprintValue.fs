namespace Rulesage.Common.Types.Domain

type BlueprintValue =
    | Leaf of template: string
    | NodeBlueprint of blueprint: NodeBlueprint
    | FromParameter of parameterKey: string
    | FromSubtask of subtaskKey: string * outputKey: string
    | Array of arr: BlueprintValue array

and NodeBlueprint =
    {
        node: Identifier
        args: Map<string, BlueprintValue>
    }
