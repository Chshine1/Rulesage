namespace Rulesage.Graph

open System.Threading.Tasks
open QuikGraph
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
        dotExporter: IGraphDotExporter
    ) =

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

                return
                    dotExporter.ExportDirectional graph.StructuralLayer,
                    dotExporter.ExportUndirectional graph.SemanticLayer
            }
