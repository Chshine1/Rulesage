namespace Rulesage.Synthesis.Interpreters.Implementations.Domain

open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Repositories.Abstractions
open Rulesage.Common.Utils.TaskUtils
open Rulesage.Interpretation.Interpreters.Abstractions
open Rulesage.Synthesis
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Types

type DynamicExprInterpreter
    (
        primitiveItp: IExprInterpreter<PrimitiveExpr>,
        conceptRepository: IConceptRepository,
        conceptEvaluator: IDynamicUnitEvaluator<ConceptExpr>,
        actionRepository: IActionRepository,
        actionEvaluator: IDynamicUnitEvaluator<ActionExpr>,
        ruleRepository: IRuleRepository,
        ruleEvaluator: IDynamicUnitEvaluator<RuleExpr>
    ) =
    let synthesizeArgsAsync (ctx: SynthesisContext) (args: ArgBlock) : Task<Map<string, InterpretedValue>> =
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
                | DynamicExpr.Concept((conceptId, gArgs), args) ->
                    let! withValues = synthesizeArgsAsync ctx args
                    let! concept = conceptRepository.FindByIdsAsync([ conceptId ], ctx.CtSource.Token)
                    return! conceptEvaluator.EvaluateAsync ctx.CtSource.Token (concept |> Seq.head) gArgs withValues
                | DynamicExpr.ResultOf((actionId, gArgs), args) ->
                    let! whereValues = synthesizeArgsAsync ctx args
                    let! action = actionRepository.FindByIdsAsync([ actionId ], ctx.CtSource.Token)
                    return! actionEvaluator.EvaluateAsync ctx.CtSource.Token (action |> Seq.head) gArgs whereValues
                | DynamicExpr.InterpretationOf((ruleId, gArgs), args) ->
                    let! forValues = synthesizeArgsAsync ctx args
                    let! rs = ruleRepository.FindByIdsAsync([ ruleId ], ctx.CtSource.Token)
                    return! ruleEvaluator.EvaluateAsync ctx.CtSource.Token (rs |> Seq.head) gArgs forValues
            }
