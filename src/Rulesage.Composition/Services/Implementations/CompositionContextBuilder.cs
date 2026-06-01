using Rulesage.Common.Repositories.Abstractions;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class CompositionContextBuilder(ICommunityRepository communityRepository, IRecordRepository recordRepository, IActionRepository actionRepository, IRuleRepository ruleRepository, IEmbeddingService embeddingService) : ICompositionContextBuilder
{
    public async Task<CompositionContext> BuildAsync(string contextCommunity, string nlStructure, CancellationToken cancellationToken = default)
    {
        var query = embeddingService.GetEmbedding(nlStructure);
        
        var communities = await communityRepository.FindOrderByCosineDistanceAsync(contextCommunity, query, 0, 10, cancellationToken);
        var records = await recordRepository.FindOrderByCosineDistanceAsync(contextCommunity, query, 0, 10, cancellationToken);
        var actions = await actionRepository.FindOrderByCosineDistanceAsync(contextCommunity, query, 0, 10, cancellationToken);
        var rules = await ruleRepository.FindOrderByCosineDistanceAsync(contextCommunity, query, 0, 10, cancellationToken);
        
        return new CompositionContext
        {
            Communities = communities.Select(t => t.Item1).ToArray(),
            Records = records.Select(t => t.Item1).ToArray(),
            Actions = actions.Select(t => t.Item1).ToArray(),
            Rules = rules.Select(t => t.Item1).ToArray()
        };
    }
}