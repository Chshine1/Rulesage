namespace Rulesage.Common.Types.Domain

type BlueprintValue =
    | Leaf of template: string
    | NodeBlueprint of node: Identifier * args: Map<string, BlueprintValue>
    | FromParameter of parameterKey: string
    | FromSubtask of subtaskKey: string * outputKey: string
    | Array of arr: BlueprintValue array
