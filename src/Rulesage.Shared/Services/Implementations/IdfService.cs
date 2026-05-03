using Microsoft.Extensions.DependencyInjection;
using Microsoft.ML.Tokenizers;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Repositories.Implementations;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Shared.Services.Implementations;

internal class IdfService<TRepository>: IIdfService where TRepository : IDocumentRepository
{
    private readonly Tokenizer _tokenizer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Lazy<Task<IdfData>> _data;

    protected IdfService(IServiceScopeFactory scopeFactory, Tokenizer tokenizer)
    {
        _tokenizer = tokenizer;
        _scopeFactory = scopeFactory;
        _data = new Lazy<Task<IdfData>>(LoadIdfData, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<float> ComputeAverageIdfAsync(string text, CancellationToken cancellationToken = default)
    {
        var terms = _tokenizer.EncodeToIds(text);
        if (terms.Count == 0) return 0f;
        
        var data = await _data.Value;
        var sum = terms.Sum(term => data.IdfMap.GetValueOrDefault(term, data.DefaultIdf));
        return sum / terms.Count;
    }
    
    private async Task<IdfData> LoadIdfData()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TRepository>();

        var documents = await db.GetDocumentsAsync();

        var docCount = 0;
        var termDocFreq = new Dictionary<int, int>();
        var idfMap = new Dictionary<int, float>();

        foreach (var term in documents.SelectMany(doc => _tokenizer.EncodeToIds(doc).Distinct()))
        {
            docCount++;
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

internal class OperationIdfService(IServiceScopeFactory scopeFactory, Tokenizer tokenizer)
    : IdfService<OperationRepository>(scopeFactory, tokenizer), IOperationIdfService;