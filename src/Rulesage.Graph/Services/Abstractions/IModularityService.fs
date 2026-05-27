namespace Rulesage.Graph.Services.Abstractions

open QuikGraph

type IModularityService =
    abstract Compute: graph: UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>> -> communities: Map<NodeId, string option> -> float