namespace Rulesage.Synthesis

open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis.Interpreters.Abstractions
open Rulesage.Synthesis.Types

type SynthesisUnit(ctx: SynthesisContext, valueItp: IExprInterpreter<ValueExpr>) =

    member _.SynthesizeAsync() : Task<SynthesizedValue> =
        valueItp.InterpretAsync ctx ctx.Rule.MustBe
