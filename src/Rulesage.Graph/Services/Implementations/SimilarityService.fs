namespace Rulesage.Graph.Services.Implementations

open System
open Rulesage.Graph.Services.Abstractions
open Rulesage.Shared.Services.Abstractions

type SimilarityService(embeddingService: IEmbeddingService) =
    interface ISimilarityService with
        member _.ComputeSimilarityAsync text1 text2 =
            task {
                let embeddings = embeddingService.GetBatchEmbeddings [ text1; text2 ]
                let mutable sum = 0.0
                let mutable length1 = 0.0
                let mutable length2 = 0.0

                for i in 0 .. (embeddings[0].Length - 1) do
                    sum <- sum + (embeddings[0][i] * embeddings[1][i])
                    length1 <- length1 + (embeddings[0][i] * embeddings[0][i])
                    length2 <- length2 + (embeddings[1][i] * embeddings[1][i])

                return sum / Math.Sqrt (length1 * length2)
            }
