namespace Rulesage.Graph.Services.Abstractions

open System.Threading.Tasks

type ISimilarityService =
    abstract member ComputeSimilarityAsync: text1: string -> text2: string -> Task<float>
