using Rulesage.Common.Types.Domain;

namespace Rulesage.Retrieval;


public interface IOperationRetrievalService
{
    Task<OperationSignature[]> RetrieveAsync(
        string nlTask,
        float? targetLevel = null,
        CancellationToken cancellationToken = default);
}