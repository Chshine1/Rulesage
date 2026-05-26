namespace Rulesage.Graph.Services.Implementations

open System.Collections.Concurrent
open System.Collections.Generic
open Microsoft.Extensions.Options
open QuikGraph
open QuikGraph.Algorithms.Search
open Rulesage.Graph
open Rulesage.Graph.Services.Abstractions

type GraphFuser(config: IOptions<GraphConfig>) =
    let _config = config.Value

    let bfsDistances (graph: UndirectedBidirectionalGraph<NodeId, Edge<NodeId>>) (root: NodeId) =
        let bfs = UndirectedBreadthFirstSearchAlgorithm(graph)

        let distances = Dictionary<NodeId, int>()
        distances[root] <- 0

        bfs.add_ExamineEdge (fun edge ->
            let u = edge.Source
            let v = edge.Target

            if not (distances.ContainsKey(v)) then
                distances[v] <- distances[u] + 1
            elif not (distances.ContainsKey(u)) then
                distances[u] <- distances[v] + 1
        )

        bfs.SetRootVertex(root)
        bfs.Compute()

        distances

    interface IGraphFuser with
        member _.Fuse structural semantic =
            let undirectedTopo = UndirectedBidirectionalGraph(structural)

            let nodesInSemantic =
                semantic.Edges
                |> Seq.collect (fun e -> [ e.Source; e.Target ])
                |> Seq.distinct
                |> Array.ofSeq

            let distCache = ConcurrentDictionary<NodeId, IDictionary<NodeId, int>>()

            nodesInSemantic
            |> Array.Parallel.iter (fun node ->
                if not (distCache.ContainsKey(node)) then
                    distCache.TryAdd(node, bfsDistances undirectedTopo node) |> ignore
            )

            let computeG (u: NodeId) (v: NodeId) =
                match distCache.TryGetValue(u) with
                | true, distsFromU ->
                    match distsFromU.TryGetValue(v) with
                    | true, dist -> max _config.GMin (_config.Alpha ** float (dist - 1))
                    | false, _ -> _config.GMin
                | false, _ -> _config.GMin

            let fusedGraph = UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>>()
            semantic.Vertices |> Seq.iter (fusedGraph.AddVertex >> ignore)

            for edge in semantic.Edges do
                let newWeight = edge.Tag * computeG edge.Source edge.Target

                fusedGraph.AddEdge(TaggedUndirectedEdge(edge.Source, edge.Target, newWeight))
                |> ignore

            fusedGraph
