namespace Rulesage.Graph.Services.Abstractions

open QuikGraph
open Rulesage.Common.Grammar
open Rulesage.Common.Grammar.Ast

type NodeId =
    | Record of id: Identifier
    | Rule of id: Identifier
    | Action of id: Identifier
    | Ref of id: string

type GraphNode = { Id: NodeId; Description: string }

type IStructureBuilder =
    abstract member Build:
        rules: RuleExpr seq ->
        records: RecordExpr seq ->
        actions: ActionExpr seq ->
            Map<NodeId, GraphNode> * BidirectionalGraph<NodeId, Edge<NodeId>>
