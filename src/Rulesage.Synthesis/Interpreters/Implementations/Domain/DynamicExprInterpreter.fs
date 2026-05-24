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

type DynamicExprInterpreter
    (
        primitiveItp: IExprInterpreter<PrimitiveExpr>,
        valueItp: IExprInterpreter<ValueExpr>,
        actionService: IActionService,
        nodeService: INodeService,
        ruleRepository: IRuleRepository
    ) =
    let synthesizeArgsAsync (ctx: SynthesisContext) (args: ArgBlock) : Task<Map<string, SynthesizedValue>> =
        task {
            let paramTasks =
                args
                |> Seq.map (fun a ->
                    task {
                        let! syn = primitiveItp.InterpretAsync ctx a.Value
                        return a.Key, syn
                    }
                )

            let! ps = paramTasks |> whenAll ctx.CtSource
            return ps |> Map.ofArray
        }

    interface IExprInterpreter<DynamicExpr> with
        member _.InterpretAsync ctx expr =
            task {
                match expr with
                | DynamicExpr.Record(nodeSignature, args) ->
                    let! withValues = synthesizeArgsAsync ctx args
                    let! node = nodeService.BuildAsync ctx.CtSource.Token nodeSignature.id withValues
                    return node |> SynthesizedValue.Node
                | DynamicExpr.ResultOf(actionId, args) ->
                    let! whereValues = synthesizeArgsAsync ctx args
                    return! actionService.CallAsync ctx.CtSource.Token actionId whereValues
                | DynamicExpr.Satisfying(ruleId, args) ->
                    let! subRule = ruleRepository.FindByIdAsync(ruleId, ctx.CtSource.Token)
                    let! whereValues = synthesizeArgsAsync ctx args

                    let subCtx: SynthesisContext =
                        {
                            CtSource = CancellationTokenSource.CreateLinkedTokenSource(ctx.CtSource.Token)
                            Rule = subRule
                            ForArgs = whereValues
                        }

                    return! valueItp.InterpretAsync subCtx subRule.MustBe
            }
