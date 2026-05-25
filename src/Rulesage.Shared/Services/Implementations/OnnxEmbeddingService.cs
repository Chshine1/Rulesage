using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Shared.Services.Implementations;

internal class OnnxEmbeddingService(Tokenizer tokenizer, string modelPath) : IEmbeddingService, IDisposable
{
    private readonly InferenceSession _inferenceSession = new(modelPath);
    private const int EmbeddingDimension = 384;

    public float[] GetEmbedding(string text, int chunkSize = IEmbeddingService.MaxSequenceLength,
        int overlapSize = IEmbeddingService.OverlapSize)
    {
        return GetBatchEmbeddings([text], chunkSize, overlapSize)[0];
    }

    public float[][] GetBatchEmbeddings(
        IEnumerable<string> texts,
        int chunkSize = IEmbeddingService.MaxSequenceLength,
        int overlapSize = IEmbeddingService.OverlapSize)
    {
        if (overlapSize >= chunkSize)
            throw new ArgumentException("Overlap size should be smaller than chunk size");
        if (chunkSize > IEmbeddingService.MaxSequenceLength)
            throw new ArgumentException("Chunk size should be smaller than max sequence length");

        var textList = texts as IList<string> ?? texts.ToList();
        if (textList.Count == 0) return [];

        var allChunks = new List<long[]>();
        var chunkTextIndex = new List<int>();

        var step = chunkSize - overlapSize;

        for (var textIdx = 0; textIdx < textList.Count; textIdx++)
        {
            var tokenized = tokenizer.EncodeToIds(textList[textIdx])
                .Select(x => (long)x)
                .ToArray();

            if (tokenized.Length == 0)
                throw new ArgumentException($"Tokenized text is empty for text at index {textIdx}.");

            for (var start = 0; start < tokenized.Length; start += step)
            {
                var length = Math.Min(chunkSize, tokenized.Length - start);
                var chunk = new long[length];
                Array.Copy(tokenized, start, chunk, 0, length);
                allChunks.Add(chunk);
                chunkTextIndex.Add(textIdx);
            }
        }

        var allEmbeddings = GetBatchEmbeddings(allChunks);

        var sumVectors = new float[textList.Count][];
        var chunkCounts = new int[textList.Count];

        for (var i = 0; i < textList.Count; i++)
        {
            sumVectors[i] = new float[EmbeddingDimension];
        }

        for (var i = 0; i < allEmbeddings.Length; i++)
        {
            var textIdx = chunkTextIndex[i];
            var emb = allEmbeddings[i];
            var sum = sumVectors[textIdx];
            for (var j = 0; j < EmbeddingDimension; j++)
            {
                sum[j] += emb[j];
            }

            chunkCounts[textIdx]++;
        }

        var results = new float[textList.Count][];
        for (var i = 0; i < textList.Count; i++)
        {
            float count = chunkCounts[i];
            var avg = new float[EmbeddingDimension];
            for (var j = 0; j < EmbeddingDimension; j++)
            {
                avg[j] = sumVectors[i][j] / count;
            }

            NormalizeL2(avg);
            results[i] = avg;
        }

        return results;
    }

    private float[][] GetBatchEmbeddings(List<long[]> tokenizedTexts)
    {
        var batchSize = tokenizedTexts.Count;
        if (batchSize == 0) return [];

        var tokenIds = new long[batchSize, IEmbeddingService.MaxSequenceLength];
        var attentionMasks = new long[batchSize, IEmbeddingService.MaxSequenceLength];
        var tokenTypeIds = new long[batchSize, IEmbeddingService.MaxSequenceLength];

        for (var i = 0; i < batchSize; i++)
        {
            var t = tokenizedTexts[i];
            if (t.Length > IEmbeddingService.MaxSequenceLength)
                throw new ArgumentException("Each tokenized text size should be smaller than max sequence length");

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
        if (results == null || results.Count == 0)
            throw new InvalidOperationException("The ONNX model returned no results.");

        var tokenEmbeddings = results[0].AsTensor<float>() ??
                              throw new InvalidOperationException("The token embeddings tensor is null.");

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

        for (var i = 0; i < IEmbeddingService.MaxSequenceLength; i++)
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
        var norm = MathF.Sqrt(sumOfSquares);

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