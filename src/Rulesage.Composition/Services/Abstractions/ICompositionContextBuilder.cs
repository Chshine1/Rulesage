using Rulesage.Common.Types.Composition;
using Rulesage.Common.Types.Domain;

namespace Rulesage.Composition.Services.Abstractions;

public interface ICompositionContextBuilder
{
    Task<CompositionContext> BuildAsync(
        Node[] availableNodes,
        Derivation[] availableConverters,
        RuleSignature[] prefetchedOperations,
        CancellationToken cancellationToken = default);
}