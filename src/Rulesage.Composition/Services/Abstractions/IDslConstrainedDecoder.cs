using Rulesage.Common.Types.Composition;
using Rulesage.Common.Types.Domain;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Abstractions;

public interface IDslConstrainedDecoder
{
    Task<OperationBlueprint> DecodeAsync(
        SemanticOperation semanticOperation,
        CompositionContext compositionContext,
        Grammar grammar,
        CancellationToken cancellationToken = default);
}