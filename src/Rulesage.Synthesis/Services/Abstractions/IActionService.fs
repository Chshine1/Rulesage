namespace Rulesage.Synthesis.Services.Abstractions

open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar
open Rulesage.Synthesis.Types

type IActionService =
    abstract member CallAsync:
        cancellationToken: CancellationToken ->
        actionId: Identifier ->
        args: Map<string, SynthesizedValue> ->
            Task<SynthesizedValue>
