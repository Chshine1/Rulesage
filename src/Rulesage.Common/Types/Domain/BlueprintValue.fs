namespace Rulesage.Common.Types.Domain

type RefSource = 
    | FromSubtask of subtaskKey: string * outputKey: string
    | FromParameter of parameterKey: string

type BlueprintValue =
    | Ref of source: RefSource * keys: string list
    | Leaf of template: string
    | NodeBlueprint of node: Identifier * args: Map<string, BlueprintValue>
    | Array of arr: BlueprintValue array
