namespace Rulesage.Graph

open System
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.Extensions.Options
open QuikGraph
open QuikGraph.Graphviz
open QuikGraph.Graphviz.Dot
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Shared.Services.Abstractions

[<CLIMutable>]
type GraphConfig = { R: int; SimThreshold: float; TfIdfThreshold: float }

type DependencyItem =
    | Record of id: Identifier
    | Rule of id: Identifier
    | Action of id: Identifier
    | Ref of expr: RefExpr

type GraphBuilder(embeddingService: IEmbeddingService, config: IOptions<GraphConfig>) =
    let _config = config.Value

    let getRecordDepsToType (t: TypeExpr) : DependencyItem list =
        match t.Atomic with
        | AtomicType.Record(id, _) -> [ DependencyItem.Record id ]
        | _ -> []

    let rec getRefDepsToPrimitive (p: PrimitiveExpr) : DependencyItem list =
        match p with
        | PrimitiveExpr.StringLiteral _
        | Var _ -> []
        | PrimitiveExpr.Ref r -> [ DependencyItem.Ref r ]
        | PrimitiveExpr.Array arr ->
            arr
            |> List.fold
                (fun rs x ->
                    let r = getRefDepsToPrimitive x
                    rs @ r
                )
                []

    // Arg items can only be assigned primitive values, thus only refs
    let getRefDepsToArgs (args: ArgBlock) : DependencyItem list =
        args
        |> List.fold
            (fun rs arg ->
                let r = getRefDepsToPrimitive arg.Value
                rs @ r
            )
            []

    let getRefDepsToIterArgs (args: IterArgBlock) : DependencyItem list =
        args
        |> List.fold
            (fun rs arg ->
                let r = getRefDepsToPrimitive arg.Value
                rs @ r
            )
            []

    let rec getDepsToValue (v: ValueExpr) : DependencyItem list =
        match v with
        | Primitive p -> getRefDepsToPrimitive p
        | Dynamic d ->
            match d with
            | DynamicExpr.Satisfying(ruleId, args) ->
                let rs = getRefDepsToArgs args
                (DependencyItem.Rule ruleId) :: rs
            | DynamicExpr.ResultOf(action, args) ->
                let rs = getRefDepsToArgs args
                DependencyItem.Action(fst action) :: rs
            | DynamicExpr.Record(record, args) ->
                let rs = getRefDepsToArgs args
                DependencyItem.Record(fst record) :: rs
        | Seq s ->
            match s with
            | SeqExpr.Satisfying(ruleId, args) ->
                let rs = getRefDepsToIterArgs args
                (DependencyItem.Rule ruleId) :: rs
            | SeqExpr.ResultOf(action, args) ->
                let rs = getRefDepsToIterArgs args
                DependencyItem.Action(fst action) :: rs
            | SeqExpr.Record(record, args) ->
                let rs = getRefDepsToIterArgs args
                DependencyItem.Record(fst record) :: rs

    let getDepsToParam (p: ParamExpr) : DependencyItem list = getRecordDepsToType p.Type
    let getDepsToGiven (g: GivenExpr) : DependencyItem list = getDepsToValue g.Value

    interface IGraphBuilder with
        member _.BuildAsync
            (rules: RuleExpr seq, records: RecordExpr seq, actions: ActionExpr seq)
            : Task<RulesageGraph> =
            task {
                let structuralGraph = BidirectionalGraph<NodeId, StructuralEdge>()
                let semanticGraph = UndirectedGraph<NodeId, SemanticEdge>()

                let mutable nodesMap = Map.empty<NodeId, GraphNode>

                let addedVertices = HashSet<NodeId>()

                let ensureVertex (id: NodeId) =
                    if addedVertices.Add(id) then
                        structuralGraph.AddVertex(id) |> ignore
                        semanticGraph.AddVertex(id) |> ignore

                let addNode (id: NodeId) (desc: string) =
                    if not (nodesMap.ContainsKey id) then
                        nodesMap <- Map.add id { Id = id; Description = desc } nodesMap
                        ensureVertex id

                let addStructEdges (targetId: NodeId) (sources: DependencyItem seq) =
                    for dep in sources do
                        let sourceId =
                            match dep with
                            | DependencyItem.Record id -> NodeId.Record id
                            | DependencyItem.Rule id -> NodeId.Rule id
                            | DependencyItem.Action id -> NodeId.Action id
                            | DependencyItem.Ref expr ->
                                let refId = NodeId.Ref $"ref_{Guid.NewGuid()}"

                                addNode
                                    refId
                                    (expr.Desc
                                     |> Seq.map (fun s ->
                                         match s with
                                         | StringPart.Literal l -> l
                                         | StringPart.Interpolation _ -> ""
                                     )
                                     |> String.Concat)

                                refId

                        ensureVertex sourceId
                        ensureVertex targetId

                        structuralGraph.AddEdge(Edge(sourceId, targetId)) |> ignore

                for r in records do
                    let id = NodeId.Record r.Id
                    addNode id r.Annotation
                    let deps = r.Fors.Values |> Seq.collect getDepsToParam
                    addStructEdges id deps

                for a in actions do
                    let id = NodeId.Action a.Id
                    addNode id a.Annotation
                    let paramDeps = a.Fors.Values |> Seq.collect getDepsToParam
                    let retDeps = getRecordDepsToType a.Returns
                    addStructEdges id (Seq.append retDeps paramDeps)

                for r in rules do
                    let id = NodeId.Rule r.Id
                    addNode id r.Annotation
                    let typeDeps = r.Fors.Values |> Seq.collect getDepsToParam
                    let givenDeps = r.Givens.Values |> Seq.collect getDepsToGiven
                    let mustBeDeps = r.MustBe |> getDepsToValue
                    let allDeps = Seq.append mustBeDeps (Seq.append givenDeps typeDeps)
                    addStructEdges id allDeps

                let nodeIds = nodesMap |> Map.keys |> Seq.toArray
                let n = nodeIds.Length
                
                let tokenizedDocs =
                    nodeIds
                    |> Array.map (fun id ->
                        nodesMap[id].Description.Split(
                            [|' '; '\n'; '\t'; '.'; ','; '"'; '('; ')'|],
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
                            |> Array.map (fun (w, arr) -> w, 1.0 + log(float arr.Length))
                            |> Map.ofArray

                        printfn $"\n[DEBUG] Original document words count=%d{words.Length}"
                        printfn "  Original document: %s" (String.concat " " words)
                        
                        let k = max 5 (int(_config.TfIdfThreshold * float words.Length))
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
                    for k in 0 .. a.Length-1 do
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

                            dists
                            |> Array.sort
                            |> Array.tryItem (_config.R - 1)
                            |> Option.defaultValue 0.0
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
