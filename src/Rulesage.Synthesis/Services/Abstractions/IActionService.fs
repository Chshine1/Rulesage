namespace Rulesage.Synthesis.Services.Abstractions

open System.Threading
open System.Threading.Tasks
open Rulesage.Synthesis.Types

type IActionService =
    abstract member CallAsync:
        cancellationToken: CancellationToken ->
        converterId: string ->
        args: Map<string, SynthesizedValue> ->
            Task<SynthesizedValue>
