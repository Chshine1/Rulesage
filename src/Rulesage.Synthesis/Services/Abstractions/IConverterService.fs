namespace Rulesage.Synthesis.Services.Abstractions

open System.Threading
open System.Threading.Tasks
open Rulesage.Synthesis.Types

type IConverterService =
    abstract member ConvertAsync:
        cancellationToken: CancellationToken ->
        converterId: int ->
        args: Map<string, SynthesizedValue> ->
            Task<SynthesizedValue>
