namespace Rulesage.Graph.Services.Abstractions

open QuikGraph

type IGraphDotExporter =
    abstract ExportDirectional: graph: BidirectionalGraph<NodeId, Edge<NodeId>> -> string
    abstract ExportUndirectional: graph: UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>> -> string
