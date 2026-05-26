namespace Rulesage.Graph

open System.Threading.Tasks
open Rulesage.Common.Grammar.Ast
open Rulesage.Graph.Services.Abstractions
open Rulesage.Shared.Services.Abstractions

type GraphBuilder
    (
        embeddingService: IEmbeddingService,
        structureBuilder: IStructureBuilder,
        descriptionCleaner: IDescriptionCleaner,
        semanticGraphBuilder: ISemanticGraphBuilder
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
                            yield (nodesMap |> Map.find nodeIds[i - 1]).Description
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
