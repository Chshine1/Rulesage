namespace Rulesage.Interpretation.Interpreters.Implementations.Evaluators

open Rulesage.Common.Grammar.Ast
open Rulesage.Interpretation.Interpreters.Abstractions
open Rulesage.Synthesis.Types

type ConceptEvaluator() =
    interface IDynamicUnitEvaluator<ConceptExpr> with
        member this.EvaluateAsync _ expr genericArgs args =
            task {
                return
                    InterpretedValue.Concept(
                        {
                            ConceptName = expr.Header.Name
                            GenericArgs = genericArgs
                            Arguments = args
                        }
                    )
            }
