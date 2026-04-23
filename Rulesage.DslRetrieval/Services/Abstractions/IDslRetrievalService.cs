using Rulesage.DslRetrieval.Database.Entities;

namespace Rulesage.DslRetrieval.Services.Abstractions;


public interface IDslRetrievalService
{
    Task<IReadOnlyList<DslEntry>> RetrieveAsync(
        string query,
        float? targetLevel = null,
        CancellationToken cancellationToken = default);
}