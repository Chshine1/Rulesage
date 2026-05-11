using Rulesage.Common.Types.Domain;

namespace Rulesage.Shared.Repositories.Abstractions;

public interface INodeRepository: IDocumentRepository
{
    Task<IEnumerable<Node>> FindByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);
    
    Task AddAsync(string id, string description, IReadOnlyDictionary<string, ParamType> paramsMap, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<(Node, float)>> FindOrderByCosineDistanceAsync(float[] queryVector, int skip, int take,
        CancellationToken cancellationToken = default);
}