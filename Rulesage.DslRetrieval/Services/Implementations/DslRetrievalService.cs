using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using Rulesage.DslRetrieval.Database;
using Rulesage.DslRetrieval.Database.Entities;
using Rulesage.DslRetrieval.Options;
using Rulesage.DslRetrieval.Services.Abstractions;

namespace Rulesage.DslRetrieval.Services.Implementations;

public class DslRetrievalService(
    DslDbContext dbContext,
    IEmbeddingService embeddingService,
    IdfService idfService,
    IOptions<RetrievalOptions> options,
    ILogger<DslRetrievalService> logger)
    : IDslRetrievalService
{
    private readonly RetrievalOptions _options = options.Value;

    public async Task<IReadOnlyList<DslEntry>> RetrieveAsync(
        string query,
        float? targetLevel = null,
        CancellationToken cancellationToken = default)
    {
        var queryVector = new Vector(embeddingService.GetEmbedding(query));

        var coarseCandidates = await dbContext.DslEntries
            .OrderBy(e => e.Embedding.CosineDistance(queryVector))
            .Take(_options.CoarseRecallSize)
            .Select(e => new { Entry = e, CosineDistance = e.Embedding.CosineDistance(queryVector) })
            .ToListAsync(cancellationToken);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("Coarse recall returned {Count} candidates", coarseCandidates.Count);
        }
        
        var tau = targetLevel ?? 1.0f;
        var scoredCandidates = coarseCandidates
            .Select(c => new
            {
                c.Entry,
                CosineSimilarity = 1.0f - (float)c.CosineDistance,
                LevelFactor = ComputeLevelFactor(c.Entry.Level, tau),
                DecayFactor = ComputeDecayFactor(c.Entry)
            })
            .Select(x => new
            {
                x.Entry,
                FinalScore = x.CosineSimilarity * x.LevelFactor * x.DecayFactor
            })
            .OrderByDescending(x => x.FinalScore)
            .Take(_options.FinalTopK)
            .Select(x => x.Entry)
            .ToList();

        return scoredCandidates;
    }

    private float ComputeLevelFactor(float entryLevel, float targetLevel)
    {
        var diff = entryLevel - targetLevel;
        return MathF.Exp(-(diff * diff) / (2 * _options.LevelAlignmentSigma * _options.LevelAlignmentSigma));
    }

    private float ComputeDecayFactor(DslEntry entry)
    {
        var avgIdf = idfService.ComputeAverageIdf(entry.Description);
        return 1.0f / (1.0f + _options.IdfPenaltyBeta * avgIdf);
    }

    public float GetNextLevel(float parentLevel) => Math.Max(0, parentLevel * _options.LevelDecayGamma);
}