namespace Rulesage.Graph.Services.Implementations

open System
open Microsoft.Extensions.Options
open QuikGraph
open Rulesage.Graph
open Rulesage.Graph.Services.Abstractions

type SemanticGraphBuilder(config: IOptions<GraphConfig>) =
    let _config = config.Value

    interface ISemanticGraphBuilder with
        member this.Build nodeIds embeddings =
            let semanticGraph = UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>>()
            semanticGraph.AddVertexRange nodeIds |> ignore

            let n = nodeIds.Length
            let distanceMatrix = Array2D.create n n 0.0

            let dotProduct (a: float32[]) (b: float32[]) =
                let mutable sum = 0.0f

                for k in 0 .. a.Length - 1 do
                    sum <- sum + a[k] * b[k]

                sum

            for i in 0 .. n - 1 do
                for j in i + 1 .. n - 1 do
                    let sim = dotProduct embeddings[i] embeddings[j]
                    let dsim = sim |> Convert.ToDouble
                    distanceMatrix[i, j] <- 1.0 - dsim
                    distanceMatrix[j, i] <- 1.0 - dsim

            let localScales =
                Array.init
                    n
                    (fun i ->
                        let dists = Array.init n (fun j -> distanceMatrix[i, j])

                        dists |> Array.sort |> Array.tryItem (_config.R - 1) |> Option.defaultValue 0.0
                    )

            for i in 0 .. n - 1 do
                for j in i + 1 .. n - 1 do
                    let scaledSim =
                        Math.Exp(-distanceMatrix[i, j] * distanceMatrix[i, j] / (localScales[i] * localScales[j]))

                    if scaledSim > _config.SimThreshold then
                        let semEdge = TaggedUndirectedEdge(nodeIds[i], nodeIds[j], scaledSim)
                        semanticGraph.AddEdge(semEdge) |> ignore

            semanticGraph
