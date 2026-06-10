namespace Rulesage.Synthesis.Interpreters.Implementations

open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis.Interpreters.Abstractions

type VarExprInterpreter(givenItp: IExprInterpreter<GivenExpr>) =
    interface IExprInterpreter<VarExpr> with
        member _.InterpretAsync ctx expr =
            task {
                let! source =
                    (match expr.Source with
                     | VarSource.For -> ctx.ForArgs |> Map.find expr.Key |> Task.FromResult
                     | VarSource.Given ->
                         ctx.Rule.Givens
                         |> Seq.find (fun (k, _) -> k = expr.Key)
                         |> snd
                         |> givenItp.InterpretAsync ctx)

                return (source, expr.Fields) ||> Seq.fold _.GetNodeField
            }
