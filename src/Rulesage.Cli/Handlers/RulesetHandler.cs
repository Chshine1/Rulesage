using Rulesage.Common;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;
using Rulesage.Shared.Services.Abstractions.TextCleaner;

namespace Rulesage.Cli.Handlers;

public class RulesetHandler(
    IRecordRepository recordRepository,
    IActionRepository actionRepository,
    IRuleRepository ruleRepository,
    IDocumentSpaceProvider documentSpaceProvider,
    ITextCleaner textCleaner,
    IEmbeddingService embeddingService,
    IEmbeddingManager embeddingManager)
{
    public async Task SaveAsync(string documentPath, CancellationToken cancellationToken = default)
    {
        var document = DocumentParser.Parse(await File.ReadAllTextAsync(documentPath, cancellationToken));

        await recordRepository.SaveAsync(document.Records, cancellationToken);
        await actionRepository.SaveAsync(document.Actions, cancellationToken);
        await ruleRepository.SaveAsync(document.Rules, cancellationToken);
        
        await embeddingManager.GenerateEmbeddings(cancellationToken);
    }

    public async Task SearchAsync(string community, string query, int take, CancellationToken cancellationToken = default)
    {
        var documentSpace = await documentSpaceProvider.GetDocumentSpaceFromDbAsync(cancellationToken);
        var cleaned = textCleaner.Clean(documentSpace, [query]).First();
        
        var embedding = embeddingService.GetEmbedding(cleaned);

        var records = await recordRepository.FindOrderByCosineDistanceAsync(community, embedding, 0, take, cancellationToken);
        var actions = await actionRepository.FindOrderByCosineDistanceAsync(community, embedding, 0, take, cancellationToken);
        var rules = await ruleRepository.FindOrderByCosineDistanceAsync(community, embedding, 0, take, cancellationToken);

        var ids = records.Select(r => ("Record", r.Item1.Id, r.Item2)).Concat(
                actions.Select(r => ("Action", r.Item1.Id, r.Item2))).Concat(
                rules.Select(r => ("Rule", r.Item1.Id, r.Item2)))
            .OrderByDescending(t => t.Item3)
            .Take(take);

        foreach (var tp in ids)
        {
            Console.WriteLine($"- {tp.Item1,-8} Id: {tp.Id,-24}  Similarity: {tp.Item3:F4}");
        }
    }
}