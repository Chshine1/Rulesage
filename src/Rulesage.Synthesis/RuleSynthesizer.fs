namespace Rulesage.Synthesis

open System.Threading
open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Services.Abstractions

type RuleSynthesizer(nlTaskResolver: INlTaskResolver, valueItp: IExprInterpreter<ValueExpr>) =
    interface IRuleSynthesizer with
        member this.SynthesizeNlTaskAsync(nlTask, cancellationToken) =
            task {
                let! rule = nlTask |> nlTaskResolver.ResolveAsync cancellationToken

                let ctx: SynthesisContext =
                    {
                        CtSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
                        Rule = rule
                        ForArgs = Map.empty

                    }

                return! valueItp.InterpretAsync ctx rule.MustBe
            }
