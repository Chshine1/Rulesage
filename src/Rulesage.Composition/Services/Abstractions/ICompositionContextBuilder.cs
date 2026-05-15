using Rulesage.Common.Grammar;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Common.Types.Composition;

namespace Rulesage.Composition.Services.Abstractions;

public interface ICompositionContextBuilder
{
    Task<CompositionContext> BuildAsync(
        NodeSignature[] availableNodes,
        ActionSignature[] availableActions,
        RuleExpr[] prefetchedOperations,
        CancellationToken cancellationToken = default);
}