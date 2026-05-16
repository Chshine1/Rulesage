using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Shared.Repositories.Abstractions;

public interface INodeRepository: IDocumentRepository
{
    Task<IEnumerable<NodeExpr>> FindByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<(NodeExpr, float)>> FindOrderByCosineDistanceAsync(float[] queryVector, int skip, int take,
        CancellationToken cancellationToken = default);
}