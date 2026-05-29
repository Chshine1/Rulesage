using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Shared.Repositories.Abstractions;

public interface IRuleRepository : IDocumentRepository
{
    Task<IEnumerable<RuleExpr>> FindByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    Task<IEnumerable<(RuleExpr, float)>> FindOrderByCosineDistanceAsync(string community, float[] queryVector, int skip, int take,
        CancellationToken cancellationToken = default);
    
    Task<bool> SaveAsync(IEnumerable<RuleExpr> rules, CancellationToken cancellationToken = default);
}