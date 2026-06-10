namespace Rulesage.Synthesis.Interpreters.Implementations.Domain

open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Repositories.Abstractions
open Rulesage.Common.Utils.TaskUtils
open Rulesage.Interpretation.Interpreters.Abstractions
open Rulesage.Synthesis
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Services.Abstractions
open Rulesage.Synthesis.Types

type SeqExprInterpreter
    (
        primitiveItp: IExprInterpreter<PrimitiveExpr>,
        actionService: IActionService,
        nodeService: INodeService,
        ruleRepository: IRuleRepository,
        ruleEvaluator: IDynamicUnitEvaluator<RuleExpr>
    ) =
    let processSeq
        (ctx: SynthesisContext)
        (args: IterArgBlock)
        (op: Map<string, InterpretedValue> -> Task<InterpretedValue>)
        =
        task {
            let! synthesizedArgs =
                args
                |> Seq.map (fun a ->
                    task {
                        let! v = primitiveItp.InterpretAsync ctx a.Value
                        return a.Key, a.Iter, v
                    }
                )
                |> whenAll ctx.CtSource

            let synthesizedArgs = synthesizedArgs |> Array.toList

            let iterArgs = synthesizedArgs |> List.filter (fun (_, iter, _) -> iter)

            let length =
                match iterArgs with
                | [] -> 1
                | (_, _, firstArr) :: _ ->
                    match firstArr with
                    | InterpretedValue.Array items -> items.Length
                    | _ -> failwith "Iter parameter value must be an array"

            for key, _, arrVal in iterArgs do
                match arrVal with
                | InterpretedValue.Array items ->
                    if items.Length <> length then
                        failwithf
                            $"Iter parameter '%s{key}' array length mismatch: expected %d{length}, got %d{items.Length}"
                | _ -> failwithf $"Iter parameter '%s{key}' must be an array"

            let buildMap (idx: int) : Map<string, InterpretedValue> =
                synthesizedArgs
                |> List.map (fun (key, iter, value) ->
                    let paramValue =
                        if iter then
                            match value with
                            | InterpretedValue.Array items -> items[idx]
                            | _ -> failwith "unexpected"
                        else
                            value

                    key, paramValue
                )
                |> Map.ofList

            let tasks = [| for i in 0 .. length - 1 -> op (buildMap i) |]
            let! results = tasks |> whenAll ctx.CtSource
            return InterpretedValue.Array results
        }

    interface IExprInterpreter<SeqExpr> with
        member _.InterpretAsync ctx expr =
            let ct = ctx.CtSource.Token

            match expr with
            | SeqExpr.Record(record, args) ->
                processSeq
                    ctx
                    args
                    (fun withValues ->
                        task {
                            let! node = nodeService.BuildAsync ct (fst record) withValues
                            return node |> InterpretedValue.Concept
                        }
                    )
            | SeqExpr.ResultOf(action, args) -> processSeq ctx args (actionService.CallAsync ct (fst action))
            | SeqExpr.Satisfying(ruleId, args) ->
                processSeq
                    ctx
                    args
                    (fun forValues ->
                        task {
                            let! rs = ruleRepository.FindByIdsAsync([ ruleId ], ct)
                            let subRule = rs |> Seq.head
                            return! ruleEvaluator.EvaluateAsync ctx.CtSource.Token subRule Map.empty forValues
                        }
                    )
