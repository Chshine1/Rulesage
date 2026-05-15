namespace Rulesage.Synthesis.Services.Abstractions

open System.Threading
open System.Threading.Tasks
open Rulesage.Common.Grammar
open Rulesage.Synthesis.Types

type INodeService =
    abstract member BuildAsync:
        cancellationToken: CancellationToken ->
        nodeId: Identifier ->
        whereValues: Map<string, SynthesizedValue> ->
            Task<SynthesizedNode>
