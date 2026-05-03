using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Shared.Services.Implementations;

internal class OnnxEmbeddingService(Tokenizer tokenizer, string modelPath): IEmbeddingService, IDisposable
{
    private readonly InferenceSession _inferenceSession = new(modelPath);
    private const int MaxSequenceLength = 256;
    private const int EmbeddingDimension = 384;

    public float[] GetEmbedding(string text, int chunkSize = MaxSequenceLength, int overlapSize = 50)
    {
        if (overlapSize >= chunkSize) throw new ArgumentException("Overlap size should be smaller than chunk size");
        if (chunkSize > MaxSequenceLength) throw new ArgumentException("Chunk size should be smaller than max sequence length");
        
        var tokenized = tokenizer.EncodeToIds(text).Select(x => (long)x).ToArray();
        if (tokenized.Length == 0) throw new ArgumentException("Tokenized text is empty");
        
        var batch = new List<long[]>();
        
        var step = chunkSize - overlapSize;
        for (var start = 0; start < tokenized.Length; start += step)
        {
            var length = Math.Min(chunkSize, tokenized.Length - start);
            var chunk = new long[length];
            Array.Copy(tokenized, start, chunk, 0, length);
            batch.Add(chunk);
        }
        
        var embeddings = GetBatchEmbeddings(batch);
        
        var result = new float[EmbeddingDimension];
        foreach (var embedding in embeddings)
        {
            for (var i = 0; i < EmbeddingDimension; i++)
            {
                result[i] += embedding[i];
            }
        }
        
        NormalizeL2(result);
        return result;
    }
    
    public float[][] GetBatchEmbeddings(IReadOnlyList<long[]> tokenizedTexts)
    {
        var batchSize = tokenizedTexts.Count;
        if (batchSize == 0) return [];
        
        var tokenIds = new long[batchSize, MaxSequenceLength];
        var attentionMasks = new long[batchSize, MaxSequenceLength];
        var tokenTypeIds = new long[batchSize, MaxSequenceLength];

        for (var i = 0; i < batchSize; i++)
        {
            var t = tokenizedTexts[i];
            if (t.Length > MaxSequenceLength) throw new ArgumentException("Each tokenized text size should be smaller than max sequence length");
            
            for (var j = 0; j < t.Length; j++)
            {
                tokenIds[i, j] = t[j];
                attentionMasks[i, j] = 1;
            }
        }

        var inputIdsTensor = tokenIds.ToTensor();
        var attentionMaskTensor = attentionMasks.ToTensor();
        var tokenTypeIdsTensor = tokenTypeIds.ToTensor();

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor),
            NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIdsTensor)
        };

        using var results = _inferenceSession.Run(inputs);
        var tokenEmbeddings = results[0].AsTensor<float>();

        var sentenceEmbeddings = new float[batchSize][];
        for (var i = 0; i < batchSize; i++)
        {
            var embeddings = MeanPoolingForSample(i, tokenEmbeddings, attentionMasks);
            NormalizeL2(embeddings);
            sentenceEmbeddings[i] = embeddings;
        }
        return sentenceEmbeddings;
    }

    private static float[] MeanPoolingForSample(int batchIndex, Tensor<float> tokenEmbeddings, long[,] attentionMasks)
    {
        var sum = new float[EmbeddingDimension];
        var count = 0;

        for (var i = 0; i < MaxSequenceLength; i++)
        {
            if (attentionMasks[batchIndex, i] != 1) continue;
            for (var j = 0; j < EmbeddingDimension; j++)
            {
                sum[j] += tokenEmbeddings[batchIndex, i, j];
            }
            count++;
        }

        for (var i = 0; i < EmbeddingDimension; i++)
        {
            sum[i] /= count;
        }
        return sum;
    }
    
    private static void NormalizeL2(float[] vector)
    {
        var sumOfSquares = vector.Sum(t => t * t);
        var norm = (float)Math.Sqrt(sumOfSquares);

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
    }

    public void Dispose()
    {
        _inferenceSession.Dispose();
        GC.SuppressFinalize(this);
    }
}