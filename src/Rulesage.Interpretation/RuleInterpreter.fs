namespace Rulesage.Synthesis

open Rulesage.Common.Grammar.Ast
open Rulesage.Interpretation.Interpreters.Abstractions
open Rulesage.Synthesis.Services.Abstractions

type RuleInterpreter(subjectResolver: ISubjectResolver, ruleEvaluator: IDynamicUnitEvaluator<RuleExpr>) =
    interface IRuleInterpreter with
        member this.InterpretSubjectAsync(subject, cancellationToken) =
            task {
                let! rule = subject |> subjectResolver.ResolveAsync cancellationToken
                return! ruleEvaluator.EvaluateAsync cancellationToken rule Map.empty Map.empty
            }
