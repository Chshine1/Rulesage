namespace Rulesage.Synthesis

open System.Collections.Generic
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open Rulesage.Synthesis.Types

type IRuleSynthesizer =
    abstract member SynthesizeNlTaskAsync:
        nlTask: string * [<Optional; DefaultParameterValue(CancellationToken())>] cancellationToken: CancellationToken ->
            Task<IReadOnlyDictionary<string, SynthesizedNode>>
