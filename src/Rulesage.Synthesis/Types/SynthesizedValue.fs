namespace Rulesage.Synthesis.Types

open Rulesage.Common.Grammar

type SynthesizedValue =
    | Leaf of value: string
    | Node of instance: SynthesizedNode
    | Array of arr: SynthesizedValue array

    member s.GetNodeField(fieldKey: string) : SynthesizedValue =
        match s with
        | Node i -> i.arguments |> Map.find fieldKey
        | _ -> failwith "A value that's not a node cannot extract fields"


and SynthesizedNode =
    {
        nodeType: Identifier
        arguments: Map<string, SynthesizedValue>
    }
