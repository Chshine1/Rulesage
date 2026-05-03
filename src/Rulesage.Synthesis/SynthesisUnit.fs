namespace Rulesage.Synthesis

open System.Collections.Concurrent
open System.Text.Json
open System.Text.RegularExpressions
open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Types.Domain
open Rulesage.Synthesis.Services.Abstractions
open Rulesage.Synthesis.Types

type SynthesisUnit
    (
        factory: SynthesisUnitFactory,
        cancellationToken: CancellationToken,
        operation: OperationBlueprint,
        operationArgs: Map<string, SynthesizedValue>,
        converterService: IConverterService,
        operationService: IOperationService,
        nlTaskResolver: INlTaskResolver,
        jsonOptions: JsonSerializerOptions
    ) =
    static let placeholderRegex = Regex(@"\{(\w+)\}", RegexOptions.Compiled)

    static let Format (template: string) (values: Map<string, string>) : string =
        placeholderRegex.Replace(
            template,
            fun (m: Match) ->
                let key = m.Groups[1].Value

                match values.TryFind key with
                | Some v -> v
                | None -> failwithf $"Missing argument key: %s{key}"
        )

    let internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)

    let whenAll (tasks: seq<Task<'T>>) =
        task {
            try
                return! Task.WhenAll(tasks)
            with ex ->
                internalCts.Cancel()
                return! Task.FromException<'T[]>(ex)
        }

    let formattedArgs =
        operationArgs |> Map.map (fun _ a -> JsonSerializer.Serialize(a, jsonOptions))

    let subtasksCache = ConcurrentDictionary<string, Task<SynthesizedValue>>()

    let rec SynthesizeOperationAsync () : Task<Map<string, SynthesizedNode>> =
        task {
            let! nodePairs =
                operation.outputs
                |> Seq.map (fun kv ->
                    task {
                        let! node = SynthesizeNodeAsync kv.Value
                        return kv.Key, node
                    }
                )
                |> whenAll

            return nodePairs |> Map.ofSeq
        }

    and SynthesizeNodeAsync (node: NodeBlueprint) : Task<SynthesizedNode> =
        task {
            let! args = SynthesizeArgumentsAsync node.args

            return
                {
                    nodeType = node.node
                    arguments = args
                }
        }

    and SynthesizeArgumentsAsync (args: Map<string, BlueprintValue>) : Task<Map<string, SynthesizedValue>> =
        task {
            let! tasks =
                args
                |> Seq.map (fun kv ->
                    task {
                        let! node = SynthesizeValueAsync kv.Value
                        return kv.Key, node
                    }
                )
                |> whenAll

            return tasks |> Map.ofSeq
        }

    and SynthesizeValueAsync (value: BlueprintValue) : Task<SynthesizedValue> =
        match value with
        | BlueprintValue.Leaf template -> Format template formattedArgs |> SynthesizedValue.Leaf |> Task.FromResult
        | BlueprintValue.NodeBlueprint nodeBlueprint ->
            task {
                let! node = SynthesizeNodeAsync nodeBlueprint
                return node |> SynthesizedValue.Node
            }
        | BlueprintValue.FromParameter parameterKey ->
            let arg = operationArgs |> Map.tryFind parameterKey

            match arg with
            | Some a -> a |> Task.FromResult
            | None -> failwith "argument key not found"
        | BlueprintValue.FromSubtask(subtaskKey, outputKey) ->
            subtasksCache.GetOrAdd(
                subtaskKey,
                task {
                    let subtask = operation.subtasks |> Map.tryFind subtaskKey

                    match subtask with
                    | Some s ->
                        match s with
                        | Subtask.InvokeConverter(converter, converterArgs) ->
                            let! synArgs = SynthesizeArgumentsAsync converterArgs
                            return! converterService.ConvertAsync internalCts.Token converter.id synArgs
                        | Subtask.InvokeOperation(subOp, subArgs) ->
                            let bp = operationService.FindOneById subOp.id
                            let! args = SynthesizeArgumentsAsync subArgs
                            let subUnit = factory.Create internalCts.Token bp args
                            let! outputs = subUnit.SynthesizeAsync()
                            return outputs |> Map.find outputKey |> SynthesizedValue.Node
                        | Subtask.NlTask template ->
                            let! outputs = Format template formattedArgs |> SynthesizeNlTaskAsync
                            return outputs |> Map.find outputKey |> SynthesizedValue.Node
                    | None -> return failwith "subtask not found"
                }
            )

    and SynthesizeNlTaskAsync (nlTask: string) =
        task {
            let! op = nlTaskResolver.ResolveAsync internalCts.Token nlTask
            let unit = factory.Create internalCts.Token op Map.empty
            return! unit.SynthesizeAsync()
        }

    member _.SynthesizeAsync() : Task<Map<string, SynthesizedNode>> = SynthesizeOperationAsync()

and SynthesisUnitFactory
    (
        converterService: IConverterService,
        operationService: IOperationService,
        nlTaskResolver: INlTaskResolver,
        jsonOptions: JsonSerializerOptions
    ) =
    member this.Create
        (cancellationToken: CancellationToken)
        (operation: OperationBlueprint)
        (operationArgs: Map<string, SynthesizedValue>)
        : SynthesisUnit =
        SynthesisUnit(
            this,
            cancellationToken,
            operation,
            operationArgs,
            converterService,
            operationService,
            nlTaskResolver,
            jsonOptions
        )
