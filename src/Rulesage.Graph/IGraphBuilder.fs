namespace Rulesage.Graph

open System
open System.Threading.Tasks
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast

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

type IGraphBuilder =
    abstract member BuildAsync: rules: RuleExpr seq -> records: RecordExpr seq -> actions: ActionExpr seq -> Task<RulesageGraph>