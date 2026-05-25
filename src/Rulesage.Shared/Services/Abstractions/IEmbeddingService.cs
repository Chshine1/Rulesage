namespace Rulesage.Shared.Services.Abstractions;

public interface IEmbeddingService
{
    protected const int MaxSequenceLength = 256;
    protected const int OverlapSize = 50;
    
    double[] GetEmbedding(string text, int chunkSize = MaxSequenceLength, int overlapSize = OverlapSize);
    double[][] GetBatchEmbeddings(IEnumerable<string> texts, int chunkSize = MaxSequenceLength, int overlapSize = OverlapSize);
}