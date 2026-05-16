namespace Rulesage.Synthesis

open System.Threading
open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis.Types

type SynthesisContext = {
    Token : CancellationToken
    Rule: RuleExpr
    ForArgs : Map<string, SynthesizedValue>
    Factory : SynthesisContext -> RuleExpr -> Map<string, SynthesizedValue> -> SynthesisUnit
}