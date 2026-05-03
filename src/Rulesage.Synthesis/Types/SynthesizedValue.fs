namespace Rulesage.Synthesis.Types

open Rulesage.Common.Types.Domain

type SynthesizedValue =
    | Leaf of value: string
    | Node of instance: SynthesizedNode

and SynthesizedNode =
    {
        nodeType: Identifier
        arguments: Map<string, SynthesizedValue>
    }
