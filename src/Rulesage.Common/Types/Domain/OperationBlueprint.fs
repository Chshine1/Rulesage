namespace Rulesage.Common.Types.Domain

type Subtask =
    | InvokeOperation of operation: Identifier * args: Map<string, BlueprintValue>
    | InvokeConverter of converter: Identifier * args: Map<string, BlueprintValue>
    // TODO: Output type expected for an NL task
    | NlTask of template: string
    // When the subtask is of "map" kind, all of its args should be arrays of the same length
    // and the subtask executes by applying the actual task to each group of elements at the same index
    | Map of task: Subtask

type OperationBlueprint =
    {
        subtasks: Map<string, Subtask>
        outputs: Map<string, BlueprintValue>
    }
