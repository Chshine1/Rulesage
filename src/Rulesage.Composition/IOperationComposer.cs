using Rulesage.Common.Types.Domain;

namespace Rulesage.Composition;

public interface IOperationComposer
{
    Task<OperationBlueprint> ComposeAsync(
        string nlTask,
        OperationSignature[] prefetchedOperations,
        CancellationToken cancellationToken = default);
}