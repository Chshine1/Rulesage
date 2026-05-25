namespace Rulesage.Graph

open System
open System.Collections.Generic
open System.Threading.Tasks
open Microsoft.Extensions.Options
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast
open Rulesage.Graph.Services.Abstractions

type NodeId =
    | Record of id: Identifier
    | Rule of id: Identifier
    | Action of id: Identifier
    | Ref of id: string

type GraphNode = { Id: NodeId; Description: string }

[<CustomEquality; CustomComparison>]
type StructuralEdge =
    {
        SourceId: NodeId
        TargetId: NodeId
    }

    interface IComparable with
        member this.CompareTo obj =
            match obj with
            | :? StructuralEdge as e ->
                let c1 = compare this.SourceId e.SourceId
                if c1 <> 0 then c1 else compare this.TargetId e.TargetId
            | _ -> invalidArg "obj" "Cannot compare values of different types"

    override this.Equals obj =
        match obj with
        | :? StructuralEdge as e -> this.SourceId = e.SourceId && this.TargetId = e.TargetId
        | _ -> false

    override this.GetHashCode() = hash (this.SourceId, this.TargetId)

[<CustomEquality; CustomComparison>]
type SemanticEdge =
    {
        SourceId: NodeId
        TargetId: NodeId
        Weight: float
    }

    interface IComparable with
        member this.CompareTo obj =
            match obj with
            | :? SemanticEdge as e ->
                let c1 = compare this.SourceId e.SourceId
                if c1 <> 0 then c1 else compare this.TargetId e.TargetId
            | _ -> invalidArg "obj" "Cannot compare values of different types"

    override this.Equals obj =
        match obj with
        | :? SemanticEdge as e -> this.SourceId = e.SourceId && this.TargetId = e.TargetId
        | _ -> false

    override this.GetHashCode() = hash (this.SourceId, this.TargetId)

type RulesageGraph =
    {
        Nodes: Map<NodeId, GraphNode>
        StructuralLayer: Map<NodeId, StructuralEdge Set>
        SemanticLayer: Map<NodeId, SemanticEdge Set>
    }

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
        | AtomicType.Record id -> [ DependencyItem.Record id ]
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
            | DynamicExpr.ResultOf(actionId, args) ->
                let rs = getRefDepsToArgs args
                (DependencyItem.Action actionId) :: rs
            | DynamicExpr.Record(recordId, args) ->
                let rs = getRefDepsToArgs args
                (DependencyItem.Record recordId) :: rs
        | Seq s ->
            match s with
            | SeqExpr.Satisfying(ruleId, args) ->
                let rs = getRefDepsToIterArgs args
                (DependencyItem.Rule ruleId) :: rs
            | SeqExpr.ResultOf(actionId, args) ->
                let rs = getRefDepsToIterArgs args
                (DependencyItem.Action actionId) :: rs
            | SeqExpr.Record(recordId, args) ->
                let rs = getRefDepsToIterArgs args
                (DependencyItem.Record recordId) :: rs

    let getDepsToParam (p: ParamExpr) : DependencyItem list = getRecordDepsToType p.Type
    let getDepsToGiven (g: GivenExpr) : DependencyItem list = getDepsToValue g.Value

    member this.BuildAsync
        (rules: RuleExpr seq)
        (records: RecordExpr seq)
        (actions: ActionExpr seq)
        : Task<RulesageGraph> =
        task {
            let mutable nodesMap = Map.empty<NodeId, GraphNode>
            let mutable structEdgesList: StructuralEdge seq = []

            let addNode (id: NodeId) (desc: string) : unit =
                nodesMap <- Map.add id { Id = id; Description = desc } nodesMap

            let addStructEdges (targetId: NodeId) (sources: DependencyItem seq) =
                structEdgesList <-
                    sources
                    |> Seq.map (fun d ->
                        let source =
                            match d with
                            | DependencyItem.Record id -> NodeId.Record id
                            | DependencyItem.Rule id -> NodeId.Rule id
                            | DependencyItem.Action id -> NodeId.Action id
                            | DependencyItem.Ref expr ->
                                let id = NodeId.Ref $"ref_{Guid.NewGuid().ToString()}"
                                addNode id (expr.ToString())
                                id

                        {
                            SourceId = source
                            TargetId = targetId
                        }
                    )
                    |> Seq.append structEdgesList

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
                addStructEdges id (retDeps |> Seq.append paramDeps)

            for r in rules do
                let id = NodeId.Rule r.Id
                addNode id r.Annotation

                let typeDeps = r.Fors.Values |> Seq.collect getDepsToParam
                let givenDeps = r.Givens.Values |> Seq.collect getDepsToGiven
                let mustBeDeps = r.MustBe |> getDepsToValue

                let allDeps = mustBeDeps |> Seq.append givenDeps |> Seq.append typeDeps

                addStructEdges id allDeps

            let structLayer =
                structEdgesList
                |> Seq.filter (fun e -> Map.containsKey e.SourceId nodesMap)
                |> Seq.groupBy _.SourceId
                |> Map.ofSeq
                |> Map.map (fun _ -> Set.ofSeq)

            let nodeIds = nodesMap |> Map.keys |> Seq.toArray
            let n = nodeIds.Length
            let simMatrix = Array2D.create n n 0.0
            let distanceMatrix = Array2D.create n n 0.0

            for i in 0 .. n - 1 do
                for j in i + 1 .. n - 1 do
                    let desc1 = nodesMap[nodeIds[i]].Description
                    let desc2 = nodesMap[nodeIds[j]].Description
                    let! sim = simService.ComputeSimilarityAsync desc1 desc2
                    simMatrix[i, j] <- sim
                    simMatrix[j, i] <- sim
                    distanceMatrix[i, j] <- 1.0 - sim
                    distanceMatrix[j, i] <- 1.0 - sim

            let localScales =
                Array.init
                    n
                    (fun i ->
                        let sims = Array.init n (fun j -> simMatrix[i, j])

                        (_config.R - 1) |> (sims |> Array.sortBy id |> Array.get)
                    )

            let semanticEdges = List<SemanticEdge>()
            
            for i in 0 .. n - 1 do
                for j in i + 1 .. n - 1 do
                    let sim = Math.Exp (- distanceMatrix[i, j] * distanceMatrix[i, j] / (localScales[i] * localScales[j]))
                    if sim > _config.SimThreshold then
                        semanticEdges.Add(
                            {
                                SourceId = nodeIds[i]
                                TargetId = nodeIds[j]
                                Weight = sim
                            }
                        )
                        semanticEdges.Add(
                            {
                                SourceId = nodeIds[j]
                                TargetId = nodeIds[i]
                                Weight = sim
                            }
                        )

            let semLayer =
                semanticEdges
                |> Seq.groupBy _.SourceId
                |> Map.ofSeq
                |> Map.map (fun _ -> Set.ofSeq)

            return
                {
                    Nodes = nodesMap
                    StructuralLayer = structLayer
                    SemanticLayer = semLayer
                }
        }
