using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Common.Repositories.Abstractions;
using Rulesage.Retrieval.Options;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Retrieval;

internal class RuleRetrievalService(
    IEmbeddingService embeddingService,
    IRuleRepository ruleRepository,
    IOptions<RetrievalOptions> options,
    ILogger<RuleRetrievalService> logger)
    : IRuleRetrievalService
{
    private readonly RetrievalOptions _options = options.Value;

    public async Task<RuleExpr[]> RetrieveAsync(
        string nlTask,
        CancellationToken cancellationToken = default)
    {
        var queryVector = embeddingService.GetEmbedding(nlTask);

        var coarseCandidates =
            (await ruleRepository.FindOrderByCosineDistanceAsync("none", queryVector, 0, _options.CoarseRecallSize,
                cancellationToken)).ToArray();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Coarse recall returned {Count} candidates", coarseCandidates.Length);
        }

        return coarseCandidates
            .Select(t => new
            {
                Operation = t.Item1,
                CosineSimilarity = 1.0f - t.Item2
            })
            .OrderByDescending(x => x.CosineSimilarity)
            .Take(_options.FinalTopK)
            .Select(x => x.Operation)
            .ToArray();
    }
}