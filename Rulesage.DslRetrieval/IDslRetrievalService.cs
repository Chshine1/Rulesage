using Rulesage.DslRetrieval.Database.Entities;

namespace Rulesage.DslRetrieval;


public interface IDslRetrievalService
{
    Task<IReadOnlyList<DslEntry>> RetrieveAsync(
        string nlTask,
        float? targetLevel = null,
        CancellationToken cancellationToken = default);
}