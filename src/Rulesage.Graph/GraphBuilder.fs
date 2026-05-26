namespace Rulesage.Graph

open System.Collections.Concurrent
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.Extensions.Options
open QuikGraph
open QuikGraph.Algorithms.Search
open QuikGraph.Graphviz
open QuikGraph.Graphviz.Dot
open Rulesage.Common.Grammar.Ast
open Rulesage.Graph.Services.Abstractions
open Rulesage.Shared.Services.Abstractions

type GraphBuilder
    (
        embeddingService: IEmbeddingService,
        structureBuilder: IStructureBuilder,
        descriptionCleaner: IDescriptionCleaner,
        semanticGraphBuilder: ISemanticGraphBuilder,
        config: IOptions<GraphConfig>
    ) =
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

    interface IGraphBuilder with
        member _.BuildAsync
            (rules: RuleExpr seq, records: RecordExpr seq, actions: ActionExpr seq)
            : Task<RulesageGraph> =
            task {
                let nodesMap, structuralGraph = structureBuilder.Build rules records actions

                let nodeIds = nodesMap.Keys |> Array.ofSeq
                let n = nodeIds.Length

                let descriptions =
                    seq {
                        for i in 1..n do
                            yield (nodesMap |> Map.find nodeIds[i]).Description
                    }

                let cleanedDescriptions = descriptionCleaner.Clean n descriptions
                let embeddings = embeddingService.GetBatchEmbeddings cleanedDescriptions
                let semanticGraph = semanticGraphBuilder.Build nodeIds embeddings

                return
                    {
                        Nodes = nodesMap
                        StructuralLayer = structuralGraph
                        SemanticLayer = semanticGraph
                    }
            }

        member this.CombineGraphs(raw) =
            let undirectedTopo = UndirectedBidirectionalGraph(raw.StructuralLayer)

            let nodesInSemantic =
                raw.SemanticLayer.Edges
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
            raw.SemanticLayer.Vertices |> Seq.iter (fusedGraph.AddVertex >> ignore)

            for edge in raw.SemanticLayer.Edges do
                let newWeight = edge.Tag * computeG edge.Source edge.Target

                fusedGraph.AddEdge(TaggedUndirectedEdge(edge.Source, edge.Target, newWeight))
                |> ignore

            fusedGraph

        member this.PropagateLabels
            (graph: UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>>, seeds: Map<NodeId, string>)
            : Map<NodeId, string option> =

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

        member this.ToDotAsync(rules, records, actions) =
            task {
                let! graph = (this :> IGraphBuilder).BuildAsync(rules, records, actions)
                let structural = GraphvizAlgorithm<NodeId, Edge<NodeId>>(graph.StructuralLayer)
                structural.CommonVertexFormat.Style <- GraphvizVertexStyle.Filled
                structural.CommonVertexFormat.FillColor <- GraphvizColor(255uy, 255uy, 150uy, 255uy)

                structural.FormatVertex.Add(fun args ->
                    match args.Vertex with
                    | NodeId.Record _ -> args.VertexFormat.Shape <- GraphvizVertexShape.InvTrapezium
                    | NodeId.Rule _ -> args.VertexFormat.Shape <- GraphvizVertexShape.MSquare
                    | NodeId.Action _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Diamond
                    | NodeId.Ref _ ->
                        args.VertexFormat.Style <- GraphvizVertexStyle.Dashed
                        args.VertexFormat.Shape <- GraphvizVertexShape.Circle
                )

                structural.FormatEdge.Add(fun args -> args.EdgeFormat.StrokeColor <- GraphvizColor.Black)

                let semantic =
                    GraphvizAlgorithm<NodeId, TaggedUndirectedEdge<NodeId, float>>(graph.SemanticLayer)

                semantic.CommonVertexFormat.Style <- GraphvizVertexStyle.Filled
                semantic.CommonVertexFormat.FillColor <- GraphvizColor(255uy, 255uy, 150uy, 255uy)

                semantic.FormatVertex.Add(fun args ->
                    match args.Vertex with
                    | NodeId.Record _ -> args.VertexFormat.Shape <- GraphvizVertexShape.InvTrapezium
                    | NodeId.Rule _ -> args.VertexFormat.Shape <- GraphvizVertexShape.MSquare
                    | NodeId.Action _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Diamond
                    | NodeId.Ref _ ->
                        args.VertexFormat.Style <- GraphvizVertexStyle.Dashed
                        args.VertexFormat.Shape <- GraphvizVertexShape.Circle
                )

                semantic.FormatEdge.Add(fun args -> args.EdgeFormat.StrokeColor <- GraphvizColor.Black)

                return structural.Generate(), semantic.Generate()
            }
