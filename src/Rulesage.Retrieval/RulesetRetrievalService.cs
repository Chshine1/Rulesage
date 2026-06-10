using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FSharp.Collections;
using Rulesage.Common;
using Rulesage.Common.Repositories.Abstractions;
using Rulesage.Retrieval.Options;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Retrieval;

internal class RulesetRetrievalService(
    IEmbeddingService embeddingService,
    IRuleRepository ruleRepository,
    IOptions<RetrievalOptions> options,
    ILogger<RulesetRetrievalService> logger)
    : IRulesetRetrievalService
{
    private readonly RetrievalOptions _options = options.Value;

    public async Task<RulesetSection> RetrieveAsync(
        string subject,
        CancellationToken cancellationToken = default)
    {
        var queryVector = embeddingService.GetEmbedding(subject);

        var coarseCandidates =
            (await ruleRepository.FindOrderByCosineDistanceAsync("none", queryVector, 0, _options.CoarseRecallSize,
                cancellationToken)).ToArray();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Coarse recall returned {Count} candidates", coarseCandidates.Length);
        }

        var rules = coarseCandidates
            .Select(t => new
            {
                Operation = t.Item1,
                CosineSimilarity = 1.0f - t.Item2
            })
            .OrderByDescending(x => x.CosineSimilarity)
            .Take(_options.FinalTopK)
            .Select(x => x.Operation);

        return new RulesetSection(ListModule.OfSeq(rules), [], []);
    }
}