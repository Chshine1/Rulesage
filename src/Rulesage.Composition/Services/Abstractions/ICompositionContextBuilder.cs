using Rulesage.Common.Types.Composition;
using Rulesage.Common.Types.Domain;

namespace Rulesage.Composition.Services.Abstractions;

public interface ICompositionContextBuilder
{
    Task<CompositionContext> BuildAsync(
        Node[] availableNodes,
        Converter[] availableConverters,
        OperationSignature[] prefetchedOperations,
        CancellationToken cancellationToken = default);
}