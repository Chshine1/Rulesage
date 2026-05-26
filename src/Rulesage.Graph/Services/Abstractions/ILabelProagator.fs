namespace Rulesage.Graph.Services.Abstractions

open QuikGraph

type ILabelPropagator =
    abstract Propagate:
        graph: UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>> ->
        seeds: Map<NodeId, string> ->
            Map<NodeId, string option>
