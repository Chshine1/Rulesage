namespace Rulesage.Interpretation.Interpreters.Implementations

open System.Threading
open Rulesage.Common.Grammar.Ast
open Rulesage.Interpretation.Interpreters.Abstractions
open Rulesage.Synthesis
open Rulesage.Synthesis.Interpreters.Abstractions

type RuleEvaluator(valueItp: IExprInterpreter<ValueExpr>) =
    interface IDynamicUnitEvaluator<RuleExpr> with
        member this.EvaluateAsync cancellationToken expr genericArgs args =
            task {
                let ctx: SynthesisContext =
                    {
                        CtSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                        Rule = expr
                        GenericArgs = genericArgs
                        ForArgs = args
                    }

                return! valueItp.InterpretAsync ctx (expr.Givens |> Seq.last |> snd |> _.Value)
            }
