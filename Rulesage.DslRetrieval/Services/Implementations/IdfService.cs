using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML.Tokenizers;
using Rulesage.DslRetrieval.Database;
using Rulesage.DslRetrieval.Services.Abstractions;

namespace Rulesage.DslRetrieval.Services.Implementations;

internal class IdfService: IIdfService
{
    private readonly Tokenizer _tokenizer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Lazy<IdfData> _data;

    public IdfService(IServiceScopeFactory scopeFactory, Tokenizer tokenizer)
    {
        _tokenizer = tokenizer;
        _scopeFactory = scopeFactory;
        _data = new Lazy<IdfData>(LoadIdfData, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public float ComputeAverageIdf(string text)
    {
        var terms = _tokenizer.EncodeToIds(text);
        if (terms.Count == 0) return 0f;
        
        var data = _data.Value;
        var sum = terms.Sum(term => data.IdfMap.GetValueOrDefault(term, data.DefaultIdf));
        return sum / terms.Count;
    }
    
    private IdfData LoadIdfData()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DslDbContext>();

        var documents = db.DslEntries
            .AsNoTracking()
            .Select(d => d.Description)
            .ToArray();

        var docCount = documents.Length;
        var termDocFreq = new Dictionary<int, int>();
        var idfMap = new Dictionary<int, float>();

        foreach (var term in documents.SelectMany(doc => _tokenizer.EncodeToIds(doc).Distinct()))
        {
            termDocFreq[term] = termDocFreq.GetValueOrDefault(term) + 1;
        }

        foreach (var kv in termDocFreq)
        {
            idfMap[kv.Key] = MathF.Log((docCount + 1f) / (kv.Value + 1f));
        }

        var defaultIdf = MathF.Log(docCount + 1) + 1;

        return new IdfData(idfMap, defaultIdf);
    }
    
    private record IdfData(Dictionary<int, float> IdfMap, float DefaultIdf);
}