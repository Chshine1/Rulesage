namespace Rulesage.Graph.Services.Implementations

open Rulesage.Graph.Services.Abstractions

type ModularityService() =
    interface IModularityService with
        member _.Compute graph communities =
            let m2 = (graph.Edges |> Seq.sumBy _.Tag) * 2.0
            let nodeIds = graph.Vertices |> Array.ofSeq
            
            let k = nodeIds |> Seq.map (fun n -> n, graph.Edges |> Seq.filter (fun e -> e.Source = n) |> Seq.sumBy _.Tag) |> Map.ofSeq
            
            let mutable sum = 0.0
            let communityIds = communities |> Map.toSeq |> Seq.groupBy snd
            
            for group in communityIds do
                let ids = snd group |> Seq.map fst
                for i in ids do
                    for j in ids do
                        let edge = graph.Edges |> Seq.tryFind (fun e -> e.Source = i && e.Target = j)
                        match edge with
                        | Some e -> sum <- sum + e.Tag - (k[i] * k[j] / m2)
                        | None -> ()
            
            sum / m2