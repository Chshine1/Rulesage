using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Shared.Repositories.Abstractions;

public interface IActionRepository: IDocumentRepository
{
    Task<IEnumerable<ActionExpr>> FindByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<(ActionExpr, float)>> FindOrderByCosineDistanceAsync(float[] queryVector, int skip, int take,
        CancellationToken cancellationToken = default);
    
    Task<bool> SaveAsync(IEnumerable<ActionExpr> actions, CancellationToken cancellationToken = default);
}