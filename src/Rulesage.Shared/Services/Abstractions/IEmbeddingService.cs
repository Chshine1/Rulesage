namespace Rulesage.Shared.Services.Abstractions;

public interface IEmbeddingService
{
    float[] GetEmbedding(string text, int chunkSize = 200, int overlapSize = 50);
    float[][] GetBatchEmbeddings(IReadOnlyList<long[]> tokenizedTexts);
}