namespace Rulesage.Common.Types.Domain

type GivenItem =
    | Rule of rule: Identifier * args: Map<string, BlueprintValue>
    | Derive of derivation: Identifier * args: Map<string, BlueprintValue>
    | Ref of template: string
    | Sequential of task: GivenItem

type Rule =
    {
        parameters: Map<string, ParamType>
        given: Map<string, GivenItem>
        mustBe: Map<string, BlueprintValue>
    }
