namespace Rulesage.Synthesis.Interpreters.Implementations

open Rulesage.Common.Grammar.Ast
open Rulesage.Common.Utils.TaskUtils
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Types

type PrimitiveExprInterpreter(varItp: IExprInterpreter<VarExpr>, stringItp: IExprInterpreter<StringTemplate>) =
    interface IExprInterpreter<PrimitiveExpr> with
        member this.InterpretAsync ctx expr =
            match expr with
            | PrimitiveExpr.Var v -> varItp.InterpretAsync ctx v
            | PrimitiveExpr.Array a ->
                task {
                    let interpret = (this :> IExprInterpreter<PrimitiveExpr>).InterpretAsync ctx
                    let! r = a |> Seq.map interpret |> whenAll ctx.CtSource
                    return SynthesizedValue.Array r
                }
            | PrimitiveExpr.StringLiteral s -> stringItp.InterpretAsync ctx s
            | PrimitiveExpr.Ref r -> stringItp.InterpretAsync ctx r.Desc
