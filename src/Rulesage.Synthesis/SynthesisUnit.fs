namespace Rulesage.Synthesis

open System.Text.Json
open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Shared.Repositories.Abstractions
open Rulesage.Synthesis.Services.Abstractions
open Rulesage.Synthesis.Types

type SynthesisUnit
    (
        factory: SynthesisUnitFactory,
        cancellationToken: CancellationToken,
        rule: RuleExpr,
        forArgs: Map<string, SynthesizedValue>,
        actionService: IActionService,
        nodeService: INodeService,
        ruleRepository: IRuleRepository,
        nlTaskResolver: INlTaskResolver,
        jsonOptions: JsonSerializerOptions
    ) =

    let internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)

    let whenAll (tasks: seq<Task<'T>>) =
        task {
            try
                return! Task.WhenAll(tasks)
            with ex ->
                internalCts.Cancel()
                return! Task.FromException<'T[]>(ex)
        }

    let rec synthesizeVarExprAsync (varExpr: VarExpr) : Task<SynthesizedValue> =
        task {
            let! source =
                (match varExpr.Source with
                 | VarSource.For -> forArgs |> Map.find varExpr.Key |> Task.FromResult
                 | VarSource.Given -> synthesizeGivenExprAsync varExpr.Key)

            return (source, varExpr.Fields) ||> Seq.fold _.GetNodeField
        }

    and synthesizeStringTemplateAsync (template: StringTemplate) : Task<string> =
        task {
            let parts =
                template
                |> Seq.map (fun p ->
                    match p with
                    | StringPart.Literal l -> Task.FromResult l
                    | StringPart.Interpolation i ->
                        task {
                            let! syn = synthesizeVarExprAsync i
                            return JsonSerializer.Serialize(syn, jsonOptions)
                        }
                )

            let! result = parts |> whenAll
            return result |> String.concat ""
        }

    and synthesizePrimitiveExprAsync (primitiveExpr: PrimitiveExpr) : Task<SynthesizedValue> =
        match primitiveExpr with
        | PrimitiveExpr.Var v -> synthesizeVarExprAsync v
        | PrimitiveExpr.Array a ->
            task {
                let! r = a |> Seq.map synthesizePrimitiveExprAsync |> whenAll
                return SynthesizedValue.Array r
            }
        | PrimitiveExpr.StringLiteral s ->
            task {
                let! str = synthesizeStringTemplateAsync s
                return SynthesizedValue.Leaf str
            }
        | PrimitiveExpr.Ref r ->
            task {
                let! s = synthesizeStringTemplateAsync r.Desc
                return! synthesizeNlTaskAsync s
            }

    and synthesizeArgsAsync (args: ArgBlock) : Task<Map<string, SynthesizedValue>> =
        task {
            let paramTasks =
                args
                |> Seq.map (fun a ->
                    task {
                        let! syn = synthesizePrimitiveExprAsync a.Value
                        return a.Key, syn
                    }
                )

            let! ps = paramTasks |> whenAll
            return ps |> Map.ofArray
        }

    and processSeq (args: IterArgBlock) (op: Map<string, SynthesizedValue> -> Task<SynthesizedValue>) =
        task {
            let! synthesizedArgs =
                args
                |> Seq.map (fun a ->
                    task {
                        let! v = synthesizePrimitiveExprAsync a.Value
                        return a.Key, a.Iter, v
                    }
                )
                |> whenAll

            let synthesizedArgs = synthesizedArgs |> Array.toList

            let iterArgs = synthesizedArgs |> List.filter (fun (_, iter, _) -> iter)

            let length =
                match iterArgs with
                | [] -> 1
                | (_, _, firstArr) :: _ ->
                    match firstArr with
                    | SynthesizedValue.Array items -> items.Length
                    | _ -> failwith "Iter parameter value must be an array"

            for key, _, arrVal in iterArgs do
                match arrVal with
                | SynthesizedValue.Array items ->
                    if items.Length <> length then
                        failwithf
                            $"Iter parameter '%s{key}' array length mismatch: expected %d{length}, got %d{items.Length}"
                | _ -> failwithf $"Iter parameter '%s{key}' must be an array"

            let buildMap (idx: int) : Map<string, SynthesizedValue> =
                synthesizedArgs
                |> List.map (fun (key, iter, value) ->
                    let paramValue =
                        if iter then
                            match value with
                            | SynthesizedValue.Array items -> items[idx]
                            | _ -> failwith "unexpected"
                        else
                            value

                    key, paramValue
                )
                |> Map.ofList

            let tasks = [| for i in 0 .. length - 1 -> op (buildMap i) |]
            let! results = tasks |> whenAll
            return SynthesizedValue.Array results
        }

    and synthesizeValueExprAsync (valueExpr: ValueExpr) : Task<SynthesizedValue> =
        task {
            match valueExpr with
            | ValueExpr.Primitive e -> return! synthesizePrimitiveExprAsync e
            | ValueExpr.Dynamic d ->
                match d with
                | DynamicExpr.Node(nodeSignature, args) ->
                    let! withValues = synthesizeArgsAsync args
                    let! node = nodeService.BuildAsync internalCts.Token nodeSignature.id withValues
                    return node |> SynthesizedValue.Node
                | DynamicExpr.ResultOf(actionId, args) ->
                    let! whereValues = synthesizeArgsAsync args
                    return! actionService.CallAsync internalCts.Token actionId whereValues
                | DynamicExpr.Satisfying(ruleId, args) ->
                    let! subRule = ruleRepository.FindByIdAsync(ruleId, internalCts.Token)
                    let! whereValues = synthesizeArgsAsync args
                    let subUnit = factory.Create internalCts.Token subRule whereValues
                    return! subUnit.SynthesizeAsync()
            | ValueExpr.Seq s ->
                match s with
                | SeqExpr.Node(nodeSig, args) ->
                    return!
                        processSeq
                            args
                            (fun withValues ->
                                task {
                                    let! node = nodeService.BuildAsync internalCts.Token nodeSig.id withValues
                                    return node |> SynthesizedValue.Node
                                }
                            )
                | SeqExpr.ResultOf(actionId, args) ->
                    return!
                        processSeq
                            args
                            (fun withValues -> actionService.CallAsync internalCts.Token actionId withValues)
                | SeqExpr.Satisfying(ruleId, args) ->
                    return!
                        processSeq
                            args
                            (fun withValues ->
                                task {
                                    let! subRule = ruleRepository.FindByIdAsync(ruleId, internalCts.Token)
                                    let subUnit = factory.Create internalCts.Token subRule withValues
                                    return! subUnit.SynthesizeAsync()
                                }
                            )
        }

    and synthesizeGivenExprAsync (givenKey: string) : Task<SynthesizedValue> =
        task {
            let e = rule.Givens |> Map.find givenKey
            return! (e.Value |> synthesizeValueExprAsync)
        }

    and synthesizeNlTaskAsync (nlTask: string) : Task<SynthesizedValue> =
        task {
            let! rl = nlTaskResolver.ResolveAsync internalCts.Token nlTask
            let unit = factory.Create internalCts.Token rl Map.empty
            return! unit.SynthesizeAsync()
        }

    member _.SynthesizeAsync() : Task<SynthesizedValue> = synthesizeValueExprAsync rule.MustBe

and SynthesisUnitFactory
    (
        actionService: IActionService,
        nodeService: INodeService,
        ruleRepository: IRuleRepository,
        nlTaskResolver: INlTaskResolver,
        jsonOptions: JsonSerializerOptions
    ) =
    member this.Create
        (cancellationToken: CancellationToken)
        (rule: RuleExpr)
        (ruleArgs: Map<string, SynthesizedValue>)
        : SynthesisUnit =
        SynthesisUnit(
            this,
            cancellationToken,
            rule,
            ruleArgs,
            actionService,
            nodeService,
            ruleRepository,
            nlTaskResolver,
            jsonOptions
        )
