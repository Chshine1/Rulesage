using Microsoft.FSharp.Collections;
using Rulesage.Common.Grammar;
using Rulesage.Common.Grammar.Ast;
using Rulesage.Common.Types.Composition;
using Rulesage.Composition.Services.Abstractions;

namespace Rulesage.Composition.Services.Implementations;

public class CompositionContextBuilder : ICompositionContextBuilder
{
    public Task<CompositionContext> BuildAsync(
        NodeSignature[] availableNodes,
        ActionSignature[] availableActions,
        RuleExpr[] prefetchedOperations,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CompositionContext(
            ListModule.OfSeq(availableNodes.Select(n => n.id)),
            ListModule.OfSeq(availableActions.Select(c => c.id)),
            ListModule.OfSeq(prefetchedOperations.Select(o => o.Id))
        ));
    }
}