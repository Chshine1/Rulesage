using Rulesage.Common.Types.Domain;

namespace Rulesage.Shared.Repositories.Abstractions;

public interface IOperationRepository : IDocumentRepository
{
    Task<Rule?> FindByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IEnumerable<(RuleSignature, float)>> FindOrderByCosineDistanceAsync(float[] queryVector, int skip, int take,
        CancellationToken cancellationToken = default);
}