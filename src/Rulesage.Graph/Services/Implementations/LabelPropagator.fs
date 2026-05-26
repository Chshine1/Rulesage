namespace Rulesage.Graph.Services.Implementations

open System.Collections.Generic
open Microsoft.Extensions.Options
open Rulesage.Graph
open Rulesage.Graph.Services.Abstractions

type LabelPropagator(config: IOptions<GraphConfig>) =
    let _config = config.Value

    interface ILabelPropagator with
        member _.Propagate graph seeds =
            let allNodes = graph.Vertices |> Seq.toArray
            let labels = Dictionary<NodeId, string option>()

            for node in allNodes do
                labels[node] <- seeds.TryFind node

            let mutable changed = true
            let mutable iter = 0

            while changed && iter < _config.PropergateMaxIter do
                changed <- false
                iter <- iter + 1

                for node in allNodes do
                    if not (seeds.ContainsKey node) then
                        let scores = Dictionary<string, float>()

                        for edge in graph.AdjacentEdges(node) do
                            let neighbor =
                                if edge.Source.Equals(node) then
                                    edge.Target
                                else
                                    edge.Source

                            let weight = edge.Tag

                            match labels.TryGetValue neighbor with
                            | true, Some lbl ->
                                match scores.TryGetValue lbl with
                                | true, s -> scores[lbl] <- s + weight
                                | false, _ -> scores[lbl] <- weight
                            | _ -> ()

                        if scores.Count > 0 then
                            let bestLabel = scores |> Seq.maxBy _.Value |> _.Key

                            match labels[node] with
                            | Some current when current = bestLabel -> ()
                            | _ ->
                                labels[node] <- Some bestLabel
                                changed <- true

            labels |> Seq.map (fun kvp -> kvp.Key, kvp.Value) |> Map.ofSeq
