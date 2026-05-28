using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Shared.Services.Implementations;

[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.Members)]
public class IdfConfig
{
    public float IdfThreshold { get; init; }
}

internal class IdfService: IIdfService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IdfConfig _config;
    private readonly Lazy<Task<IdfData>> _data;

    protected IdfService(IServiceScopeFactory scopeFactory, IOptions<IdfConfig> config)
    {
        _scopeFactory = scopeFactory;
        _config = config.Value;
        _data = new Lazy<Task<IdfData>>(LoadIdfData, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<string> CleanAsync(string text, CancellationToken cancellationToken = default)
    {
        var words = text.Split([' ', '\n', '\t', '.', ',', ':', '\"', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
            
        var k = Math.Max(5, (int)(_config.IdfThreshold * words.Length));
        var data = await _data.Value;
        var dWords = words
            .Distinct()
            .OrderByDescending(w => data.IdfMap.GetValueOrDefault(w, data.DefaultIdf))
            .Take(k)
            .ToArray();

        return string.Concat(words.Where(w => dWords.Contains(w)));
    }
    
    private async Task<IdfData> LoadIdfData()
    {
        using var scope = _scopeFactory.CreateScope();
        var recordRepository = scope.ServiceProvider.GetRequiredService<IRecordRepository>();
        var actionRepository = scope.ServiceProvider.GetRequiredService<IActionRepository>();
        var ruleRepository = scope.ServiceProvider.GetRequiredService<IRuleRepository>();

        var recordDocuments = await recordRepository.GetDocumentsAsync();
        var actionDocuments = await actionRepository.GetDocumentsAsync();
        var ruleDocuments = await ruleRepository.GetDocumentsAsync();
        
        var documents = recordDocuments.Concat(actionDocuments).Concat(ruleDocuments);

        var docCount = 0;
        var termDocFreq = new Dictionary<string, int>();
        var idfMap = new Dictionary<string, float>();

        foreach (var term in documents.SelectMany(doc => doc.Split([' ', '\n', '\t', '.', ',', ':', '\"', '(', ')'], StringSplitOptions.RemoveEmptyEntries)))
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
    
    private record IdfData(Dictionary<string, float> IdfMap, float DefaultIdf);
}