using Rulesage.Common.Types.Domain;

namespace Rulesage.Shared.Repositories.Abstractions;

public interface INodeRepository: IDocumentRepository
{
    Task<IEnumerable<Node>> FindByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<Node>> FindByIrsAsync(IEnumerable<string> irs, CancellationToken cancellationToken = default);
    
    Task AddAsync(string ir, string description, IReadOnlyDictionary<string, ParamType> paramsMap, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<(Node, float)>> FindOrderByCosineDistanceAsync(float[] queryVector, int skip, int take,
        CancellationToken cancellationToken = default);
}