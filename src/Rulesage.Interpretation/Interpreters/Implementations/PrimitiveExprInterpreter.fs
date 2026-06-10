namespace Rulesage.Synthesis.Interpreters.Implementations

open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Utils.TaskUtils
open Rulesage.Interpretation.Interpreters.Abstractions
open Rulesage.Synthesis
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Services.Abstractions
open Rulesage.Synthesis.Types

type PrimitiveExprInterpreter
    (
        subjectResolver: ISubjectResolver,
        varItp: IExprInterpreter<VarExpr>,
        stringItp: IExprInterpreter<StringTemplate>,
        ruleEvaluator: IDynamicUnitEvaluator<RuleExpr>
    ) =
    interface IExprInterpreter<PrimitiveExpr> with
        member this.InterpretAsync ctx expr =
            match expr with
            | PrimitiveExpr.Var v -> varItp.InterpretAsync ctx v
            | PrimitiveExpr.Array a ->
                task {
                    let interpret = (this :> IExprInterpreter<PrimitiveExpr>).InterpretAsync ctx
                    let! r = a |> Seq.map interpret |> whenAll ctx.CtSource
                    return InterpretedValue.Array r
                }
            | PrimitiveExpr.StringLiteral s -> stringItp.InterpretAsync ctx s
            | PrimitiveExpr.Ref r ->
                task {
                    let! subject = stringItp.InterpretAsync ctx r.Desc

                    let literalSubj =
                        match subject with
                        | Literal l -> l
                        | _ -> failwith ""

                    let! rule =
                        literalSubj
                        |> subjectResolver.ResolveWithConstraintAsync ctx.CtSource.Token r.ExpctedType

                    return! ruleEvaluator.EvaluateAsync ctx.CtSource.Token rule Map.empty Map.empty
                }
