namespace Rulesage.Graph.Services.Abstractions

open QuikGraph

type IGraphFuser =
    abstract Fuse:
        structural: BidirectionalGraph<NodeId, Edge<NodeId>> ->
        semantic: UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>> ->
            UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>>
