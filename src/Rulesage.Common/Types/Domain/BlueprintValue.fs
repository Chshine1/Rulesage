namespace Rulesage.Common.Types.Domain

type RefSource = 
    | FromGiven of givenKey: string * mustBeKey: string
    | FromFor of forKey: string

type BlueprintValue =
    | Ref of source: RefSource * keys: string list
    | Literal of template: string
    | NodeBlueprint of node: Identifier * args: Map<string, BlueprintValue>
    | Array of arr: BlueprintValue array
