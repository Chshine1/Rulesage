namespace Rulesage.DslRetrieval.Embedding.Abstractions;

public interface IEmbeddingService
{
    float[] GetEmbedding(string text, int chunkSize = 200, int overlapSize = 50);
    public float[][] GetBatchEmbeddings(IReadOnlyList<long[]> tokenizedTexts);
}