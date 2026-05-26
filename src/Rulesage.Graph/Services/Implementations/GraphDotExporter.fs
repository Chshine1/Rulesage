namespace Rulesage.Graph.Services.Implementations

open QuikGraph
open QuikGraph.Graphviz
open QuikGraph.Graphviz.Dot
open Rulesage.Graph.Services.Abstractions

type GraphDotExporter() =
    interface IGraphDotExporter with
        member this.ExportDirectional(graph) =
            let alg = GraphvizAlgorithm<NodeId, Edge<NodeId>>(graph)
            alg.CommonVertexFormat.Style <- GraphvizVertexStyle.Filled
            alg.CommonVertexFormat.FillColor <- GraphvizColor(255uy, 255uy, 150uy, 255uy)

            alg.FormatVertex.Add(fun args ->
                match args.Vertex with
                | NodeId.Record _ -> args.VertexFormat.Shape <- GraphvizVertexShape.InvTrapezium
                | NodeId.Rule _ -> args.VertexFormat.Shape <- GraphvizVertexShape.MSquare
                | NodeId.Action _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Diamond
                | NodeId.Ref _ ->
                    args.VertexFormat.Style <- GraphvizVertexStyle.Dashed
                    args.VertexFormat.Shape <- GraphvizVertexShape.Circle
            )

            alg.FormatEdge.Add(fun args -> args.EdgeFormat.StrokeColor <- GraphvizColor.Black)
            alg.Generate()

        member this.ExportUndirectional(graph) =
            let alg = GraphvizAlgorithm<NodeId, TaggedUndirectedEdge<NodeId, float>>(graph)
            alg.CommonVertexFormat.Style <- GraphvizVertexStyle.Filled
            alg.CommonVertexFormat.FillColor <- GraphvizColor(255uy, 255uy, 150uy, 255uy)

            alg.FormatVertex.Add(fun args ->
                match args.Vertex with
                | NodeId.Record _ -> args.VertexFormat.Shape <- GraphvizVertexShape.InvTrapezium
                | NodeId.Rule _ -> args.VertexFormat.Shape <- GraphvizVertexShape.MSquare
                | NodeId.Action _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Diamond
                | NodeId.Ref _ ->
                    args.VertexFormat.Style <- GraphvizVertexStyle.Dashed
                    args.VertexFormat.Shape <- GraphvizVertexShape.Circle
            )

            alg.FormatEdge.Add(fun args -> args.EdgeFormat.StrokeColor <- GraphvizColor.Black)
            alg.Generate()
