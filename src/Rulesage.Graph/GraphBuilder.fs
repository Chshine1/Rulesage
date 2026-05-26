namespace Rulesage.Graph

open System.Threading.Tasks
open Microsoft.Extensions.Options
open QuikGraph
open QuikGraph.Graphviz
open QuikGraph.Graphviz.Dot
open Rulesage.Common.Grammar.Ast
open Rulesage.Graph.Services.Abstractions
open Rulesage.Shared.Services.Abstractions

type GraphBuilder
    (
        embeddingService: IEmbeddingService,
        structureBuilder: IStructureBuilder,
        descriptionCleaner: IDescriptionCleaner,
        semanticGraphBuilder: ISemanticGraphBuilder,
        graphFuser: IGraphFuser,
        lablePropagator: ILabelPropagator,
        config: IOptions<GraphConfig>
    ) =
    let _config = config.Value

    interface IGraphBuilder with
        member _.BuildAsync
            (rules: RuleExpr seq, records: RecordExpr seq, actions: ActionExpr seq)
            : Task<RulesageGraph> =
            task {
                let nodesMap, structuralGraph = structureBuilder.Build rules records actions

                let nodeIds = nodesMap.Keys |> Array.ofSeq
                let n = nodeIds.Length

                let descriptions =
                    seq {
                        for i in 1..n do
                            yield (nodesMap |> Map.find nodeIds[i]).Description
                    }

                let cleanedDescriptions = descriptionCleaner.Clean n descriptions
                let embeddings = embeddingService.GetBatchEmbeddings cleanedDescriptions
                let semanticGraph = semanticGraphBuilder.Build nodeIds embeddings

                return
                    {
                        Nodes = nodesMap
                        StructuralLayer = structuralGraph
                        SemanticLayer = semanticGraph
                    }
            }

        member _.CombineGraphs(raw) =
            graphFuser.Fuse raw.StructuralLayer raw.SemanticLayer

        member this.PropagateLabels
            (graph: UndirectedGraph<NodeId, TaggedUndirectedEdge<NodeId, float>>, seeds: Map<NodeId, string>)
            : Map<NodeId, string option> =
            lablePropagator.Propagate graph seeds

        member this.ToDotAsync(rules, records, actions) =
            task {
                let! graph = (this :> IGraphBuilder).BuildAsync(rules, records, actions)
                let structural = GraphvizAlgorithm<NodeId, Edge<NodeId>>(graph.StructuralLayer)
                structural.CommonVertexFormat.Style <- GraphvizVertexStyle.Filled
                structural.CommonVertexFormat.FillColor <- GraphvizColor(255uy, 255uy, 150uy, 255uy)

                structural.FormatVertex.Add(fun args ->
                    match args.Vertex with
                    | NodeId.Record _ -> args.VertexFormat.Shape <- GraphvizVertexShape.InvTrapezium
                    | NodeId.Rule _ -> args.VertexFormat.Shape <- GraphvizVertexShape.MSquare
                    | NodeId.Action _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Diamond
                    | NodeId.Ref _ ->
                        args.VertexFormat.Style <- GraphvizVertexStyle.Dashed
                        args.VertexFormat.Shape <- GraphvizVertexShape.Circle
                )

                structural.FormatEdge.Add(fun args -> args.EdgeFormat.StrokeColor <- GraphvizColor.Black)

                let semantic =
                    GraphvizAlgorithm<NodeId, TaggedUndirectedEdge<NodeId, float>>(graph.SemanticLayer)

                semantic.CommonVertexFormat.Style <- GraphvizVertexStyle.Filled
                semantic.CommonVertexFormat.FillColor <- GraphvizColor(255uy, 255uy, 150uy, 255uy)

                semantic.FormatVertex.Add(fun args ->
                    match args.Vertex with
                    | NodeId.Record _ -> args.VertexFormat.Shape <- GraphvizVertexShape.InvTrapezium
                    | NodeId.Rule _ -> args.VertexFormat.Shape <- GraphvizVertexShape.MSquare
                    | NodeId.Action _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Diamond
                    | NodeId.Ref _ ->
                        args.VertexFormat.Style <- GraphvizVertexStyle.Dashed
                        args.VertexFormat.Shape <- GraphvizVertexShape.Circle
                )

                semantic.FormatEdge.Add(fun args -> args.EdgeFormat.StrokeColor <- GraphvizColor.Black)

                return structural.Generate(), semantic.Generate()
            }
