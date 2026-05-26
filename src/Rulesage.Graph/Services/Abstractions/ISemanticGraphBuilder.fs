namespace Rulesage.Graph.Services.Abstractions

open QuikGraph

type ISemanticGraphBuilder =
    abstract Build:
        nodeIds: NodeId[] -> embeddings: float32[][] -> UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>>
