namespace Rulesage.Common.Types.Domain

type Subtask =
    | InvokeOperation of operation: Identifier * args: Map<string, BlueprintValue>
    | InvokeConverter of converter: Identifier * args: Map<string, BlueprintValue>
    | NlTask of template: string

type OperationBlueprint =
    {
        subtasks: Map<string, Subtask>
        outputs: Map<string, NodeBlueprint>
    }
