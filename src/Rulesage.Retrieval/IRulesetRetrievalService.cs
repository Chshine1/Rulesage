using Rulesage.Common;

namespace Rulesage.Retrieval;

public interface IRulesetRetrievalService
{
    Task<RulesetSection> RetrieveAsync(
        string subject,
        CancellationToken cancellationToken = default);
}