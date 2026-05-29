using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Shared.Repositories.Abstractions;

public interface ICommunityRepository : IDocumentRepository
{
    Task<IEnumerable<(CommunityExpr, float)>> FindOrderByCosineDistanceAsync(string contextCommunity, float[] queryVector, int skip, int take,
        CancellationToken cancellationToken = default);
    
    Task<bool> SaveAsync(IEnumerable<CommunityExpr> communities, CancellationToken cancellationToken = default);
}