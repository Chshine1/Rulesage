using Rulesage.Common.Grammar.Ast;
using Rulesage.Composition.Services.Abstractions;
using Rulesage.Composition.Types;

namespace Rulesage.Composition.Services.Implementations;

public class CompositionContextBuilder : ICompositionContextBuilder
{
    public Task<CompositionContext> BuildAsync(
        RuleExpr[] availableRules,
        RecordExpr[] availableNodes,
        ActionExpr[] availableActions,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CompositionContext
        {
            Rules = availableRules,
            Nodes = availableNodes,
            Actions = availableActions
        });
    }
}