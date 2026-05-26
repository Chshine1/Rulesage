namespace Rulesage.Graph

open System
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

[<CLIMutable>]
type GraphConfig =
    {
        R: int
        SimThreshold: float
        TfIdfThreshold: float
        GMin: float
        Alpha: float
        PropergateMaxIter: int
    }

type GraphBuilder
    (embeddingService: IEmbeddingService, structureBuilder: IStructureBuilder, config: IOptions<GraphConfig>) =
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
                let semanticGraph = UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>>()
                semanticGraph.AddVertexRange(structuralGraph.Vertices) |> ignore

                let nodeIds = nodesMap |> Map.keys |> Seq.toArray
                let n = nodeIds.Length

                let tokenizedDocs =
                    nodeIds
                    |> Array.map (fun id ->
                        nodesMap[id]
                            .Description.Split(
                                [| ' '; '\n'; '\t'; '.'; ','; '"'; '('; ')' |],
                                StringSplitOptions.RemoveEmptyEntries
                            )
                    )

                let df =
                    tokenizedDocs
                    |> Seq.collect (fun words -> words |> Set.ofArray)
                    |> Seq.groupBy id
                    |> Seq.map (fun (word, occurrences) -> word, float (Seq.length occurrences))
                    |> Map.ofSeq

                printfn $"[DEBUG] Number of total documents: %d{n}"
                printfn $"[DEBUG] Number of distinct words: %d{df.Count}"

                let idf word =
                    match Map.tryFind word df with
                    | Some dfValue -> Math.Log((float n + 1.0) / (dfValue + 1.0))
                    | _ -> 0.0

                let cleanedDescriptions =
                    tokenizedDocs
                    |> Array.map (fun words ->
                        let tfMap =
                            words
                            |> Array.groupBy id
                            |> Array.map (fun (w, arr) -> w, 1.0 + log (float arr.Length))
                            |> Map.ofArray

                        printfn $"\n[DEBUG] Original document words count=%d{words.Length}"
                        printfn "  Original document: %s" (String.concat " " words)

                        let k = max 5 (int (_config.TfIdfThreshold * float words.Length))
                        let dWords = words |> Array.distinct

                        let topWords =
                            dWords
                            |> Array.map (fun w ->
                                let tf = tfMap |> Map.tryFind w |> Option.defaultValue 0.0
                                let idfVal = idf w
                                let tfidf = tf * idfVal

                                printfn $"    [Word: %-20s{w}] TF=%.2f{tf}, IDF=%.4f{idfVal}, TF-IDF=%.4f{tfidf}"
                                w, tfidf
                            )
                            |> Array.sortByDescending snd
                            |> Array.take (min k dWords.Length)
                            |> Array.map fst

                        let cleaned =
                            words
                            |> Array.filter (fun w -> Set.contains w (topWords |> Set.ofArray))
                            |> String.concat " "

                        printfn $"  Cleaned: %s{cleaned}"
                        cleaned
                    )

                let embeddings = embeddingService.GetBatchEmbeddings(cleanedDescriptions)

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
