namespace Rulesage.Graph

open System.Threading.Tasks
open QuikGraph
open Rulesage.Common.Grammar.Ast
open Rulesage.Graph.Services.Abstractions

type RulesageGraph =
    {
        Nodes: Map<NodeId, GraphNode>
        StructuralLayer: BidirectionalGraph<NodeId, Edge<NodeId>>
        SemanticLayer: UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>>
    }

type IGraphBuilder =
    abstract member BuildAsync:
        rules: RuleExpr seq * records: RecordExpr seq * actions: ActionExpr seq -> Task<RulesageGraph>
