namespace Rulesage.Graph.Services.Abstractions

open QuikGraph

type IGraphDotExporter =
    abstract ExportDirectional: graph: BidirectionalGraph<NodeId, Edge<NodeId>> -> string
    abstract ExportUndirectional: graph: UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>> -> string

    abstract ExportUndirectionalWithCommunities:
        graph: UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>> ->
        communities: Map<NodeId, string option> ->
            string
