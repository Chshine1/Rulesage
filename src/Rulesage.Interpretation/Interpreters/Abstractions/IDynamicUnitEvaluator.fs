namespace Rulesage.Interpretation.Interpreters.Abstractions

open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis.Types

type IDynamicUnitEvaluator<'TExpr> =
    abstract EvaluateAsync:
        cancellationToken: CancellationToken ->
        expr: 'TExpr ->
        genericArgs: Map<string, TypeExpr> ->
        args: Map<string, InterpretedValue> ->
            Task<InterpretedValue>
