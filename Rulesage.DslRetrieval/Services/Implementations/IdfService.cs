using Microsoft.ML.Tokenizers;
using Rulesage.DslRetrieval.Services.Abstractions;

namespace Rulesage.DslRetrieval.Services.Implementations;

public class IdfService: IIdfService
{
    private readonly Tokenizer _tokenizer;
    private readonly Dictionary<int, float> _idfMap = new();
    private readonly float _defaultIdf;

    public IdfService(Tokenizer tokenizer, IEnumerable<string> corpus)
    {
        _tokenizer = tokenizer;
        
        var docs = corpus.ToList();
        var docCount = docs.Count;
        var termDocFreq = new Dictionary<int, int>();

        foreach (var term in docs.SelectMany(doc => _tokenizer.EncodeToIds(doc).Distinct()))
        {
            termDocFreq[term] = termDocFreq.GetValueOrDefault(term) + 1;
        }

        foreach (var kv in termDocFreq)
        {
            _idfMap[kv.Key] = MathF.Log((docCount + 1f) / (kv.Value + 1f));
        }

        _defaultIdf = MathF.Log(docCount + 1) + 1;
    }

    public float ComputeAverageIdf(string text)
    {
        var terms = _tokenizer.EncodeToIds(text);
        if (terms.Count == 0) return 0f;
        var sum = terms.Sum(term => _idfMap.GetValueOrDefault(term, _defaultIdf));
        return sum / terms.Count;
    }
}