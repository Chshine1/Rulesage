namespace Rulesage.Synthesis

open System.Threading
open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Services.Abstractions

type RuleInterpreter(nlTaskResolver: INlTaskResolver, valueItp: IExprInterpreter<ValueExpr>) =
    interface IRuleInterpreter with
        member this.InterpretSubjectAsync(nlTask, cancellationToken) =
            task {
                let! rule = nlTask |> nlTaskResolver.ResolveAsync cancellationToken

                let ctx: SynthesisContext =
                    {
                        CtSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                        Rule = rule
                        ForArgs = Map.empty
                    }

                return! valueItp.InterpretAsync ctx (rule.Givens |> Seq.last |> snd |> _.Value)
            }
