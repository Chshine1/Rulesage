namespace Rulesage.Synthesis

open System.Threading
open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis.Types

type SynthesisContext =
    {
        CtSource: CancellationTokenSource
        Rule: RuleExpr
        ForArgs: Map<string, InterpretedValue>
    }
