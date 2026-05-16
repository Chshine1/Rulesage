namespace Rulesage.Synthesis.Services.Abstractions

open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Synthesis.Types

type IActionService =
    abstract member CallAsync:
        cancellationToken: CancellationToken ->
        action: ActionExpr ->
        args: Map<string, SynthesizedValue> ->
            Task<SynthesizedValue>
