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
open Rulesage.Graph.Services.Abstractions

[<CLIMutable>]
type GraphConfig = { R: int; SimThreshold: float }

type DependencyItem =
    | Record of id: Identifier
    | Rule of id: Identifier
    | Action of id: Identifier
    | Ref of expr: RefExpr

type GraphBuilder(simService: ISimilarityService, config: IOptions<GraphConfig>) =
    let _config = config.Value

    let getRecordDepsToType (t: TypeExpr) : DependencyItem list =
        match t.Atomic with
        | AtomicType.Record (id, _) -> [ DependencyItem.Record id ]
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
                (DependencyItem.Action (fst action)) :: rs
            | DynamicExpr.Record(record, args) ->
                let rs = getRefDepsToArgs args
                (DependencyItem.Record (fst record)) :: rs
        | Seq s ->
            match s with
            | SeqExpr.Satisfying(ruleId, args) ->
                let rs = getRefDepsToIterArgs args
                (DependencyItem.Rule ruleId) :: rs
            | SeqExpr.ResultOf(action, args) ->
                let rs = getRefDepsToIterArgs args
                (DependencyItem.Action (fst action)) :: rs
            | SeqExpr.Record(record, args) ->
                let rs = getRefDepsToIterArgs args
                (DependencyItem.Record (fst record)) :: rs

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
                                addNode refId (expr.ToString())
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

                let simMatrix = Array2D.create n n 0.0
                let distanceMatrix = Array2D.create n n 0.0

                for i in 0 .. n - 1 do
                    for j in i + 1 .. n - 1 do
                        let desc1 = nodesMap[nodeIds[i]].Description
                        let desc2 = nodesMap[nodeIds[j]].Description
                        let! sim = simService.ComputeSimilarityAsync desc1 desc2
                        let dsim = sim |> Convert.ToDouble
                        simMatrix[i, j] <- dsim
                        simMatrix[j, i] <- dsim
                        distanceMatrix[i, j] <- 1.0 - dsim
                        distanceMatrix[j, i] <- 1.0 - dsim

                let localScales =
                    Array.init
                        n
                        (fun i ->
                            let sims = Array.init n (fun j -> simMatrix[i, j])

                            sims
                            |> Array.sortDescending
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
                let graphviz = GraphvizAlgorithm<NodeId, Edge<NodeId>>(graph.StructuralLayer)
                graphviz.CommonVertexFormat.Style <- GraphvizVertexStyle.Filled
                graphviz.CommonVertexFormat.FillColor <- GraphvizColor(255uy, 255uy, 150uy, 255uy)

                graphviz.FormatVertex.Add(fun args ->
                    match args.Vertex with
                    | NodeId.Record _ ->
                        args.VertexFormat.Label <- $"{args.Vertex}"
                        args.VertexFormat.FillColor <- GraphvizColor(200uy, 230uy, 255uy, 255uy)
                    | NodeId.Rule _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Box
                    | NodeId.Action _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Diamond
                    | NodeId.Ref _ -> args.VertexFormat.Style <- GraphvizVertexStyle.Dashed
                )

                graphviz.FormatEdge.Add(fun args -> args.EdgeFormat.StrokeColor <- GraphvizColor.Black)

                return graphviz.Generate()
            }
