using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rulesage.Common.Types.Domain;
using Rulesage.Retrieval.Options;
using Rulesage.Retrieval.Utils;
using Rulesage.Shared.Repositories.Abstractions;
using Rulesage.Shared.Services.Abstractions;

namespace Rulesage.Retrieval;

internal class OperationRetrievalService(
    IEmbeddingService embeddingService,
    IOperationRepository operationRepository,
    IOperationIdfService idfService,
    IOptions<RetrievalOptions> options,
    ILogger<OperationRetrievalService> logger)
    : IOperationRetrievalService
{
    private readonly RetrievalOptions _options = options.Value;

    public async Task<OperationSignature[]> RetrieveAsync(
        string nlTask,
        float? targetLevel = null,
        CancellationToken cancellationToken = default)
    {
        var queryVector = embeddingService.GetEmbedding(nlTask);

        var coarseCandidates =
            (await operationRepository.FindOrderByCosineDistanceAsync(queryVector, 0, _options.CoarseRecallSize,
                cancellationToken)).ToArray();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Coarse recall returned {Count} candidates", coarseCandidates.Length);
        }

        var tau = targetLevel ?? 1.0f;
        var idfTasks = coarseCandidates
            .Select(c => idfService.ComputeAverageIdfAsync(c.Item1.description, cancellationToken));

        var idfResults = await Task.WhenAll(idfTasks);

        return coarseCandidates
            .Zip(idfResults, (t, averageIdf) => new
            {
                Operation = t.Item1,
                CosineSimilarity = 1.0f - t.Item2,
                LevelFactor = OperationRetrievalUtils.ComputeLevelFactor(
                    t.Item1.level, tau, _options.LevelAlignmentSigma),
                DecayFactor = OperationRetrievalUtils.ComputeDecayFactor(
                    averageIdf, _options.IdfPenaltyBeta)
            })
            .Select(x => new
            {
                x.Operation,
                FinalScore = x.CosineSimilarity * x.LevelFactor * x.DecayFactor
            })
            .OrderByDescending(x => x.FinalScore)
            .Take(_options.FinalTopK)
            .Select(x => x.Operation)
            .ToArray();
    }
}