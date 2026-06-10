namespace Rulesage.Interpretation.Interpreters.Implementations.Evaluators

open Rulesage.Common.Grammar.Ast
open Rulesage.Interpretation.Interpreters.Abstractions

type ActionEvaluator =
    interface IDynamicUnitEvaluator<ActionExpr> with
        member this.EvaluateAsync cancellationToken expr genericArgs args = task { }
