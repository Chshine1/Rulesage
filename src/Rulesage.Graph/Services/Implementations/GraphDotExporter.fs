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
                | NodeId.Ref _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Circle
            )

            alg.FormatEdge.Add(fun args -> args.EdgeFormat.StrokeColor <- GraphvizColor.Black)
            alg.Generate()

        member this.ExportUndirectionalWithCommunities graph communities =
            let alg = GraphvizAlgorithm<NodeId, TaggedUndirectedEdge<NodeId, float>>(graph)

            alg.CommonVertexFormat.Style <- GraphvizVertexStyle.Filled
            alg.CommonVertexFormat.FillColor <- GraphvizColor(255uy, 255uy, 150uy, 255uy) // 淡黄

            let communityColors =
                [
                    GraphvizColor(135uy, 206uy, 250uy, 255uy)
                    GraphvizColor(144uy, 238uy, 144uy, 255uy)
                    GraphvizColor(255uy, 182uy, 193uy, 255uy)
                    GraphvizColor(221uy, 160uy, 221uy, 255uy)
                    GraphvizColor(255uy, 218uy, 185uy, 255uy)
                    GraphvizColor(176uy, 224uy, 230uy, 255uy)
                ]

            let mutable colorIndex = 0
            let labelToColor = System.Collections.Generic.Dictionary<string, GraphvizColor>()

            let getCommunityColor (label: string) =
                match labelToColor.TryGetValue(label) with
                | true, c -> c
                | false, _ ->
                    let c = communityColors[colorIndex % communityColors.Length]
                    colorIndex <- colorIndex + 1
                    labelToColor[label] <- c
                    c

            alg.FormatVertex.Add(fun args ->
                match args.Vertex with
                | NodeId.Record _ -> args.VertexFormat.Shape <- GraphvizVertexShape.InvTrapezium
                | NodeId.Rule _ -> args.VertexFormat.Shape <- GraphvizVertexShape.MSquare
                | NodeId.Action _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Diamond
                | NodeId.Ref _ -> args.VertexFormat.Shape <- GraphvizVertexShape.Circle

                match communities.TryFind args.Vertex with
                | Some(Some community) ->
                    let color = getCommunityColor community
                    args.VertexFormat.FillColor <- color
                | _ ->
                    args.VertexFormat.Style <- GraphvizVertexStyle.Dashed
                    args.VertexFormat.StrokeColor <- GraphvizColor.Gray
            )

            alg.FormatEdge.Add(fun args ->
                let srcCommunity = communities.TryFind args.Edge.Source |> Option.flatten
                let tgtCommunity = communities.TryFind args.Edge.Target |> Option.flatten

                match srcCommunity, tgtCommunity with
                | Some c1, Some c2 when c1 = c2 ->
                    let baseColor = getCommunityColor c1

                    let darken (c: GraphvizColor) =
                        GraphvizColor(byte (float c.R * 0.7), byte (float c.G * 0.7), byte (float c.B * 0.7), c.A)

                    args.EdgeFormat.StrokeColor <- darken baseColor
                    args.EdgeFormat.Style <- GraphvizEdgeStyle.Solid
                | _ ->
                    args.EdgeFormat.StrokeColor <- GraphvizColor(192uy, 192uy, 192uy, 255uy)
                    args.EdgeFormat.Style <- GraphvizEdgeStyle.Dashed
            )

            alg.Generate()
