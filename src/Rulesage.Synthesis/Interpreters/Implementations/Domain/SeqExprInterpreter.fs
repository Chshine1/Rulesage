namespace Rulesage.Synthesis.Interpreters.Implementations.Domain

open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Utils.TaskUtils
open Rulesage.Shared.Repositories.Abstractions
open Rulesage.Synthesis
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Services.Abstractions
open Rulesage.Synthesis.Types

type SeqExprInterpreter
    (
        primitiveItp: IExprInterpreter<PrimitiveExpr>,
        actionService: IActionService,
        nodeService: INodeService,
        ruleRepository: IRuleRepository
    ) =
    let processSeq
        (ctx: SynthesisContext)
        (args: IterArgBlock)
        (op: Map<string, SynthesizedValue> -> Task<SynthesizedValue>)
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
            let! results = tasks |> whenAll ctx.CtSource
            return SynthesizedValue.Array results
        }

    interface IExprInterpreter<SeqExpr> with
        member _.InterpretAsync ctx expr =
            let ct = ctx.CtSource.Token

            match expr with
            | SeqExpr.Node(nodeSig, args) ->
                processSeq
                    ctx
                    args
                    (fun withValues ->
                        task {
                            let! node = nodeService.BuildAsync ct nodeSig.id withValues
                            return node |> SynthesizedValue.Node
                        }
                    )
            | SeqExpr.ResultOf(actionId, args) -> processSeq ctx args (actionService.CallAsync ct actionId)
            | SeqExpr.Satisfying(ruleId, args) ->
                processSeq
                    ctx
                    args
                    (fun withValues ->
                        task {
                            let! subRule = ruleRepository.FindByIdAsync(ruleId, ct)

                            let subUnit =
                                ctx.Factory
                                    {
                                        CtSource = CancellationTokenSource.CreateLinkedTokenSource(ct)
                                        Rule = subRule
                                        ForArgs = withValues
                                        Factory = ctx.Factory
                                    }

                            return! subUnit.SynthesizeAsync()
                        }
                    )
