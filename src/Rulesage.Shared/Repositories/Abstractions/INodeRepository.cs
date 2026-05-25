using Rulesage.Common.Grammar.Ast;

namespace Rulesage.Shared.Repositories.Abstractions;

public interface INodeRepository: IDocumentRepository
{
    Task<IEnumerable<RecordExpr>> FindByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<(RecordExpr, float)>> FindOrderByCosineDistanceAsync(double[] queryVector, int skip, int take,
        CancellationToken cancellationToken = default);
}